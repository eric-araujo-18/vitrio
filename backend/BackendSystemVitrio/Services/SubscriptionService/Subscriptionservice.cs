using System.Linq.Expressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.Payments;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.SubscriptionService
{
    /*
      REGRAS DA ASSINATURA (todas decididas em DecideAction)

      Situação do lojista               | Plano mais caro | Mesmo plano        | Plano mais barato        | Grátis
      ----------------------------------|-----------------|--------------------|--------------------------|----------------------
      Grátis (nunca assinou / expirou)  | Assinar         | -                  | Assinar                  | atual
      Assinatura ativa                  | Upgrade (paga)  | atual / Manter     | Mudar no próximo ciclo   | Cancelar
      Pagamento atrasado                | bloqueado       | atual              | bloqueado                | Cancelar
      Cancelada, ainda no período pago  | Upgrade (paga)  | atual até dd/mm    | bloqueado até dd/mm      | começa em dd/mm

      - Nada é liberado sem pagamento: assinar e fazer upgrade só valem depois que o
        Mercado Pago confirma (status "authorized").
      - Upgrade: quando o novo pagamento é confirmado, a assinatura antiga é cancelada
        no Mercado Pago (nunca há duas cobranças).
      - Downgrade: o valor da próxima cobrança já muda; o plano atual (já pago) vale até
        o fim do período e só então o menor entra.
      - Cancelar no Mercado Pago é definitivo: depois de cancelar, só dá para assinar o
        mesmo plano ou um menor quando o período pago acabar (upgrade pode na hora, pagando).
      - Só existe um pagamento aberto por vez: abrir outro descarta o anterior.
    */
    public class SubscriptionService : ISubscriptionService
    {
        private const int GraceDays = 7;
        private static readonly TimeSpan ActiveResyncInterval = TimeSpan.FromHours(6);

        private readonly AppDbContext _context;
        private readonly IMercadoPagoClient _mercadoPago;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly string? _testPayerEmail;

        public SubscriptionService(
            AppDbContext context,
            IMercadoPagoClient mercadoPago,
            ILogger<SubscriptionService> logger,
            IOptions<MercadoPagoOptions> options)
        {
            _context = context;
            _mercadoPago = mercadoPago;
            _logger = logger;
            _testPayerEmail = options.Value.TestPayerEmail;
        }

        // ===================== Regras =====================

        private record Decision(PlanAction Action, string Label);

        private static Decision DecideAction(Subscription? sub, Plan effective, Plan target, DateTime now)
        {
            var hasActive = sub?.GatewaySubscriptionId is not null
                            && sub.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial;
            var pastDue = sub?.GatewaySubscriptionId is not null && sub.Status == SubscriptionStatus.PastDue;
            var canceledInPeriod = sub is not null
                                   && sub.Status == SubscriptionStatus.Canceled
                                   && sub.CurrentPeriodEnd > now;
            var until = sub?.CurrentPeriodEnd?.ToString("dd/MM/yyyy") ?? "";

            var isFree = target.PriceMonthly <= 0;
            var isCurrent = target.Id == effective.Id;
            var isPending = sub?.PendingPlanId == target.Id && sub.PendingGatewaySubscriptionId is not null;

            // Grátis
            if (isFree)
            {
                if (hasActive || pastDue) return new(PlanAction.CancelToFree, "Voltar ao Grátis");
                if (canceledInPeriod) return new(PlanAction.Unavailable, $"Começa em {until}");
                return new(PlanAction.Current, "Plano atual");
            }

            // Pagamento já aberto para este plano
            if (isPending) return new(PlanAction.Checkout, "Abrir o pagamento de novo");

            if (pastDue)
                return isCurrent
                    ? new(PlanAction.Current, "Plano atual")
                    : new(PlanAction.Unavailable, "Regularize o pagamento primeiro");

            if (hasActive)
            {
                if (isCurrent)
                    return sub!.ScheduledPlanId is not null
                        ? new(PlanAction.KeepCurrent, "Manter este plano")
                        : new(PlanAction.Current, "Plano atual");

                if (target.PriceMonthly > effective.PriceMonthly)
                    return new(PlanAction.Checkout, "Fazer upgrade");

                return sub!.ScheduledPlanId == target.Id
                    ? new(PlanAction.Unavailable, $"Começa em {until}")
                    : new(PlanAction.ScheduleDowngrade, "Mudar no próximo ciclo");
            }

            if (canceledInPeriod)
            {
                if (isCurrent) return new(PlanAction.Current, $"Plano atual até {until}");
                if (target.PriceMonthly > effective.PriceMonthly) return new(PlanAction.Checkout, "Fazer upgrade");
                return new(PlanAction.Unavailable, $"Disponível a partir de {until}");
            }

            // No grátis (nunca assinou, suspensa ou período encerrado)
            return new(PlanAction.Checkout, "Assinar");
        }

        // ===================== Consulta =====================

        public async Task<Response<List<PlanDto>>> GetPlansAsync()
        {
            try
            {
                var plans = await _context.Plan.Where(p => p.IsActive).OrderBy(p => p.SortOrder).Select(ToPlanDto).ToListAsync();
                return Response<List<PlanDto>>.Ok(plans);
            }
            catch (Exception ex)
            {
                return Response<List<PlanDto>>.Fail($"Erro ao buscar planos: {ex.Message}");
            }
        }

        public async Task<Response<MySubscriptionDto>> GetMySubscriptionAsync(int userId)
        {
            try
            {
                var now = DateTime.UtcNow;
                var sub = await _context.Subscription
                    .AsNoTracking()
                    .Include(s => s.PendingPlan)
                    .Include(s => s.ScheduledPlan)
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                var effective = await _context.GetEffectivePlanAsync(userId);
                var plans = await _context.Plan.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();

                var stores = await _context.Store
                    .Where(s => s.UserId == userId && s.DeletionDate == null)
                    .OrderBy(s => s.CreationDate)
                    .Select(s => new StoreUsageDto
                    {
                        StoreId = s.Id,
                        StoreName = s.Name,
                        ProductCount = _context.Product.Count(p => p.StoreId == s.Id && p.DeletionDate == null),
                    })
                    .ToListAsync();

                var toDto = ToPlanDto.Compile();

                return Response<MySubscriptionDto>.Ok(new MySubscriptionDto
                {
                    Plan = toDto(effective),
                    Status = sub?.Status,
                    CurrentPeriodEnd = sub?.CurrentPeriodEnd,
                    CanceledAt = sub?.CanceledAt,
                    PastDueSince = sub?.PastDueSince,
                    PendingPlan = sub?.PendingPlan is null ? null : toDto(sub.PendingPlan),
                    ScheduledPlan = sub?.ScheduledPlan is null ? null : toDto(sub.ScheduledPlan),
                    CanCancel = CanCancel(sub),
                    Options = plans.Select(p =>
                    {
                        var d = DecideAction(sub, effective, p, now);
                        return new PlanOptionDto { Plan = toDto(p), Action = d.Action, Label = d.Label };
                    }).ToList(),
                    StoreCount = stores.Count,
                    Stores = stores,
                });
            }
            catch (Exception ex)
            {
                return Response<MySubscriptionDto>.Fail($"Erro ao buscar assinatura: {ex.Message}");
            }
        }

        // ===================== Ações do lojista =====================

        public async Task<Response<CheckoutResultDto>> StartCheckoutAsync(int userId, string planCode)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null)
                    return Response<CheckoutResultDto>.Fail("Usuário não encontrado.");

                var target = await _context.Plan.FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
                if (target is null)
                    return Response<CheckoutResultDto>.Fail("Plano não encontrado.");

                var sub = await _context.Subscription.FirstOrDefaultAsync(s => s.UserId == userId);
                var effective = await _context.GetEffectivePlanAsync(userId);

                // A MESMA regra que decide o botão decide o que pode ser feito aqui.
                var decision = DecideAction(sub, effective, target, DateTime.UtcNow);

                switch (decision.Action)
                {
                    case PlanAction.Checkout:
                        return await OpenCheckoutAsync(user, sub, target);

                    case PlanAction.ScheduleDowngrade:
                        await _mercadoPago.UpdatePreapprovalAmountAsync(sub!.GatewaySubscriptionId!, target.PriceMonthly);
                        sub.ScheduledPlanId = target.Id;
                        sub.UpdatedDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        return Response<CheckoutResultDto>.Ok(new CheckoutResultDto(),
                            $"O plano {target.Name} começa em {sub.CurrentPeriodEnd:dd/MM/yyyy}. Até lá, você continua com o {effective.Name}.");

                    case PlanAction.KeepCurrent:
                        await _mercadoPago.UpdatePreapprovalAmountAsync(sub!.GatewaySubscriptionId!, effective.PriceMonthly);
                        sub.ScheduledPlanId = null;
                        sub.UpdatedDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        return Response<CheckoutResultDto>.Ok(new CheckoutResultDto(), $"Pronto, você continua no plano {effective.Name}.");

                    case PlanAction.CancelToFree:
                        return Response<CheckoutResultDto>.Fail("Para voltar ao Grátis, use \"Cancelar assinatura\".");

                    case PlanAction.Current:
                        return Response<CheckoutResultDto>.Fail($"Você já está no plano {target.Name}.");

                    default:
                        return Response<CheckoutResultDto>.Fail($"Não é possível escolher o plano {target.Name} agora ({decision.Label.ToLowerInvariant()}).");
                }
            }
            catch (MercadoPagoException ex)
            {
                _logger.LogError(ex, "Erro do Mercado Pago ao trocar o plano do usuário {UserId}", userId);
                return Response<CheckoutResultDto>.Fail("Não foi possível falar com o Mercado Pago. Tente de novo em instantes.");
            }
            catch (Exception ex)
            {
                return Response<CheckoutResultDto>.Fail($"Erro ao trocar de plano: {ex.Message}");
            }
        }

        // Cria uma assinatura nova no Mercado Pago (cobra já no checkout).
        // O plano só passa a valer quando o pagamento for confirmado.
        private async Task<Response<CheckoutResultDto>> OpenCheckoutAsync(User user, Subscription? sub, Plan target)
        {
            // Um pagamento aberto por vez: descarta o anterior.
            if (sub?.PendingGatewaySubscriptionId is not null)
                await TryCancelAtGatewayAsync(sub.PendingGatewaySubscriptionId);

            var payerEmail = string.IsNullOrWhiteSpace(_testPayerEmail) ? user.Email : _testPayerEmail;

            var preapproval = await _mercadoPago.CreatePreapprovalAsync(
                reason: $"Vitrio - Plano {target.Name}",
                externalReference: $"vitrio-user-{user.Id}",
                payerEmail: payerEmail,
                amount: target.PriceMonthly);

            if (string.IsNullOrWhiteSpace(preapproval.InitPoint))
                return Response<CheckoutResultDto>.Fail("O Mercado Pago não devolveu o link de pagamento. Tente de novo.");

            if (sub is null)
            {
                sub = new Subscription { UserId = user.Id, PlanId = Plan.FreeId, Status = SubscriptionStatus.Active };
                _context.Subscription.Add(sub);
            }

            sub.PendingPlanId = target.Id;
            sub.PendingGatewaySubscriptionId = preapproval.Id;
            sub.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Response<CheckoutResultDto>.Ok(new CheckoutResultDto { CheckoutUrl = preapproval.InitPoint });
        }

        public async Task<Response<MySubscriptionDto>> CancelAsync(int userId)
        {
            try
            {
                var sub = await _context.Subscription.FirstOrDefaultAsync(s => s.UserId == userId);
                if (!CanCancel(sub))
                    return Response<MySubscriptionDto>.Fail("Você não tem uma assinatura paga para cancelar.");

                await _mercadoPago.CancelPreapprovalAsync(sub!.GatewaySubscriptionId!);
                if (sub.PendingGatewaySubscriptionId is not null)
                    await TryCancelAtGatewayAsync(sub.PendingGatewaySubscriptionId);

                // O plano continua valendo até o fim do período já pago (ver GetEffectivePlanAsync).
                MarkCanceled(sub, DateTime.UtcNow);
                ClearPending(sub);
                await _context.SaveChangesAsync();

                return await GetMySubscriptionAsync(userId);
            }
            catch (MercadoPagoException ex)
            {
                _logger.LogError(ex, "Erro do Mercado Pago ao cancelar assinatura do usuário {UserId}", userId);
                return Response<MySubscriptionDto>.Fail("Não foi possível cancelar no Mercado Pago. Tente de novo em instantes.");
            }
            catch (Exception ex)
            {
                return Response<MySubscriptionDto>.Fail($"Erro ao cancelar assinatura: {ex.Message}");
            }
        }

        public async Task<Response<MySubscriptionDto>> SyncMineAsync(int userId, bool force)
        {
            try
            {
                var sub = await _context.Subscription
                    .AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .Select(s => new { s.GatewaySubscriptionId, s.PendingGatewaySubscriptionId, s.LastSyncedAt })
                    .FirstOrDefaultAsync();

                var recentlySynced = sub?.LastSyncedAt is not null && sub.LastSyncedAt > DateTime.UtcNow.AddMinutes(-1);

                if (sub is not null && (force || !recentlySynced))
                {
                    if (sub.PendingGatewaySubscriptionId is not null)
                        await SyncPreapprovalAsync(sub.PendingGatewaySubscriptionId);
                    if (sub.GatewaySubscriptionId is not null)
                        await SyncPreapprovalAsync(sub.GatewaySubscriptionId);
                }

                return await GetMySubscriptionAsync(userId);
            }
            catch (MercadoPagoException ex)
            {
                _logger.LogError(ex, "Erro do Mercado Pago ao conferir a assinatura do usuário {UserId}", userId);
                return await GetMySubscriptionAsync(userId); // não trava a página
            }
            catch (Exception ex)
            {
                return Response<MySubscriptionDto>.Fail($"Erro ao verificar a assinatura: {ex.Message}");
            }
        }

        public async Task<Response<MySubscriptionDto>> AdminSetPlanAsync(int userId, string planCode)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null || user.Role == Role.Client)
                    return Response<MySubscriptionDto>.Fail("Lojista não encontrado.");

                var plan = await _context.Plan.FirstOrDefaultAsync(p => p.Code == planCode);
                if (plan is null)
                    return Response<MySubscriptionDto>.Fail("Plano não encontrado.");

                var sub = await _context.Subscription.FirstOrDefaultAsync(s => s.UserId == userId);
                if (sub is null)
                {
                    sub = new Subscription { UserId = userId, PlanId = plan.Id };
                    _context.Subscription.Add(sub);
                }

                // Troca manual: não mexe no Mercado Pago.
                sub.PlanId = plan.Id;
                ClearPending(sub);
                sub.ScheduledPlanId = null;
                sub.Status = SubscriptionStatus.Active;
                sub.PastDueSince = null;
                sub.CanceledAt = null;
                sub.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return await GetMySubscriptionAsync(userId);
            }
            catch (Exception ex)
            {
                return Response<MySubscriptionDto>.Fail($"Erro ao alterar plano: {ex.Message}");
            }
        }

        // ===================== Mercado Pago -> Vitrio =====================

        public async Task SyncPreapprovalAsync(string preapprovalId)
        {
            // Nunca confia no conteúdo da notificação: busca o estado atual na API.
            var preapproval = await _mercadoPago.GetPreapprovalAsync(preapprovalId);
            _logger.LogInformation("Assinatura {Id} no Mercado Pago está com status {Status}", preapproval.Id, preapproval.Status);

            var sub = await _context.Subscription.FirstOrDefaultAsync(s =>
                s.GatewaySubscriptionId == preapproval.Id || s.PendingGatewaySubscriptionId == preapproval.Id);

            if (sub is null)
            {
                _logger.LogInformation("Assinatura {Id} não está ligada a nenhum lojista; ignorada", preapproval.Id);
                return;
            }

            var now = DateTime.UtcNow;

            if (sub.PendingGatewaySubscriptionId == preapproval.Id)
                await ApplyPendingStatusAsync(sub, preapproval, now);
            else
                ApplyActiveStatus(sub, preapproval, now);

            sub.LastSyncedAt = now;
            sub.UpdatedDate = now;
            await _context.SaveChangesAsync();
        }

        // Pagamento aberto (assinatura nova ou upgrade)
        private async Task ApplyPendingStatusAsync(Subscription sub, MpPreapproval preapproval, DateTime now)
        {
            if (preapproval.Status == "cancelled")
            {
                ClearPending(sub); // desistiu: o plano atual não muda
                return;
            }

            if (preapproval.Status != "authorized")
                return; // "pending": ainda não pagou

            // Pagou: o plano novo vale a partir de agora.
            var previousGatewayId = sub.GatewaySubscriptionId;

            sub.GatewaySubscriptionId = preapproval.Id;
            sub.PlanId = sub.PendingPlanId ?? sub.PlanId;
            sub.ScheduledPlanId = null;
            sub.Status = SubscriptionStatus.Active;
            sub.PastDueSince = null;
            sub.CanceledAt = null;
            sub.CurrentPeriodEnd = ValidNextPaymentDate(preapproval, now) ?? now.AddMonths(1);
            ClearPending(sub);

            // Upgrade: cancela a assinatura antiga para nunca haver duas cobranças.
            if (previousGatewayId is not null && previousGatewayId != preapproval.Id)
                await TryCancelAtGatewayAsync(previousGatewayId);
        }

        // Assinatura paga que já estava valendo
        private static void ApplyActiveStatus(Subscription sub, MpPreapproval preapproval, DateTime now)
        {
            switch (preapproval.Status)
            {
                case "authorized":
                    if (sub.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Suspended)
                    {
                        sub.Status = SubscriptionStatus.Active;
                        sub.PastDueSince = null;
                    }
                    // Só avança a data (renovação). Datas de hoje/passado do sandbox são ignoradas.
                    var next = ValidNextPaymentDate(preapproval, now);
                    if (next is not null && (sub.CurrentPeriodEnd is null || next > sub.CurrentPeriodEnd))
                        sub.CurrentPeriodEnd = next;
                    ApplyScheduledPlanIfDue(sub, now);
                    break;

                case "paused":
                    if (sub.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial)
                    {
                        sub.Status = SubscriptionStatus.PastDue;
                        sub.PastDueSince ??= now;
                    }
                    break;

                case "cancelled":
                    // Inclui cancelamento feito pelo lojista direto no Mercado Pago.
                    if (sub.Status != SubscriptionStatus.Canceled)
                        MarkCanceled(sub, now);
                    break;
            }
        }

        public async Task HandleAuthorizedPaymentAsync(string authorizedPaymentId)
        {
            var charge = await _mercadoPago.GetAuthorizedPaymentAsync(authorizedPaymentId);
            if (string.IsNullOrWhiteSpace(charge.PreapprovalId))
                return;

            if (charge.Payment?.Status == "approved")
            {
                await SyncPreapprovalAsync(charge.PreapprovalId);
                return;
            }

            if (charge.Payment?.Status is "rejected" or "cancelled" || charge.Status == "recycling")
            {
                // Só conta para a assinatura que já estava valendo.
                var sub = await _context.Subscription.FirstOrDefaultAsync(s => s.GatewaySubscriptionId == charge.PreapprovalId);
                if (sub?.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial)
                {
                    sub.Status = SubscriptionStatus.PastDue;
                    sub.PastDueSince ??= DateTime.UtcNow;
                    sub.UpdatedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task RunMaintenanceAsync()
        {
            var now = DateTime.UtcNow;

            // 1) Passou da tolerância sem pagar: suspende (volta ao Grátis, sem apagar nada).
            var limit = now.AddDays(-GraceDays);
            foreach (var s in await _context.Subscription
                         .Where(s => s.Status == SubscriptionStatus.PastDue && s.PastDueSince != null && s.PastDueSince < limit)
                         .ToListAsync())
            {
                s.Status = SubscriptionStatus.Suspended;
                s.UpdatedDate = now;
            }

            // 2) Downgrades agendados cujo período acabou.
            foreach (var s in await _context.Subscription
                         .Where(s => s.ScheduledPlanId != null && s.CurrentPeriodEnd != null && s.CurrentPeriodEnd <= now)
                         .ToListAsync())
            {
                ApplyScheduledPlanIfDue(s, now);
            }

            await _context.SaveChangesAsync();

            // 3) Pagamentos abertos há mais de 10 min: confere; abandonados há 2 dias: descarta.
            var pending = await _context.Subscription
                .Where(s => s.PendingGatewaySubscriptionId != null && (s.UpdatedDate ?? s.CreationDate) < now.AddMinutes(-10))
                .Select(s => new { s.Id, GatewayId = s.PendingGatewaySubscriptionId!, Since = s.UpdatedDate ?? s.CreationDate })
                .Take(50)
                .ToListAsync();

            foreach (var item in pending)
            {
                try
                {
                    await SyncPreapprovalAsync(item.GatewayId);

                    if (item.Since < now.AddDays(-2))
                    {
                        var sub = await _context.Subscription.FirstAsync(s => s.Id == item.Id);
                        if (sub.PendingGatewaySubscriptionId == item.GatewayId)
                        {
                            await TryCancelAtGatewayAsync(item.GatewayId);
                            ClearPending(sub);
                            sub.UpdatedDate = now;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao conferir pagamento aberto {Id}", item.GatewayId);
                }
            }

            // 4) Assinaturas pagas não conferidas há 6h: pega mudanças feitas direto no
            //    Mercado Pago (ex: cancelamento pelo app), mesmo sem webhook.
            var activeIds = await _context.Subscription
                .Where(s => s.GatewaySubscriptionId != null &&
                            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue || s.Status == SubscriptionStatus.Trial) &&
                            (s.LastSyncedAt == null || s.LastSyncedAt < now - ActiveResyncInterval))
                .Select(s => s.GatewaySubscriptionId!)
                .Take(50)
                .ToListAsync();

            foreach (var id in activeIds)
            {
                try { await SyncPreapprovalAsync(id); }
                catch (Exception ex) { _logger.LogWarning(ex, "Falha ao conferir assinatura {Id}", id); }
            }
        }

        // ===================== Helpers =====================

        private static bool CanCancel(Subscription? s)
            => s?.GatewaySubscriptionId is not null
               && s.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Trial;

        // Próxima cobrança informada pelo Mercado Pago, só se for uma data futura de verdade
        // (no sandbox ela às vezes vem com a data de hoje ou do passado).
        private static DateTime? ValidNextPaymentDate(MpPreapproval p, DateTime now)
        {
            var next = p.NextPaymentDate?.ToUniversalTime();
            return next is not null && next > now.AddDays(1) ? next : null;
        }

        private static void ClearPending(Subscription s)
        {
            s.PendingPlanId = null;
            s.PendingGatewaySubscriptionId = null;
        }

        private static void MarkCanceled(Subscription s, DateTime now)
        {
            s.Status = SubscriptionStatus.Canceled;
            s.CanceledAt ??= now;
            s.ScheduledPlanId = null;
            s.UpdatedDate = now;
        }

        private static void ApplyScheduledPlanIfDue(Subscription s, DateTime now)
        {
            if (s.ScheduledPlanId is null || s.CurrentPeriodEnd is null || s.CurrentPeriodEnd > now)
                return;
            s.PlanId = s.ScheduledPlanId.Value;
            s.ScheduledPlanId = null;
            s.UpdatedDate = now;
        }

        private async Task TryCancelAtGatewayAsync(string gatewayId)
        {
            try { await _mercadoPago.CancelPreapprovalAsync(gatewayId); }
            catch (Exception ex) { _logger.LogWarning(ex, "Não foi possível cancelar a assinatura {Id} no Mercado Pago", gatewayId); }
        }

        private static readonly Expression<Func<Plan, PlanDto>> ToPlanDto = p => new PlanDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            PriceMonthly = p.PriceMonthly,
            MaxStores = p.MaxStores,
            MaxProductsPerStore = p.MaxProductsPerStore,
            AllowsOnlinePayment = p.AllowsOnlinePayment,
        };
    }
}