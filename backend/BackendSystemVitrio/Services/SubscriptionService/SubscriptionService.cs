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
        // Com checkout pendente o lojista está esperando a confirmação: confere com mais frequência.
        private static readonly TimeSpan PendingSyncInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan IdleSyncInterval = TimeSpan.FromMinutes(1);
        // Botão "Já paguei" e conferência rápida depois do checkout (a página chama a cada 5s).
        private static readonly TimeSpan ForcedSyncInterval = TimeSpan.FromSeconds(3);

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
                if (isCurrent) return new(PlanAction.Current, "Plano atual");
                // Plano pago sem cobrança, definido pelo Admin (AdminSetPlanAsync)
                return new(PlanAction.Unavailable, "Fale com o suporte");
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

            // Plano pago sem cobrança, definido pelo Admin: é o atual, não precisa assinar.
            if (isCurrent) return new(PlanAction.Current, "Plano atual");

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
                _logger.LogError(ex, "Erro ao buscar planos");
                return Response<List<PlanDto>>.Fail("Erro ao buscar planos. Tente novamente.");
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
                        StoreSlug = s.Slug,
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
                _logger.LogError(ex, "Erro ao buscar assinatura");
                return Response<MySubscriptionDto>.Fail("Erro ao buscar assinatura. Tente novamente.");
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
                _logger.LogError(ex, "Erro ao trocar de plano");
                return Response<CheckoutResultDto>.Fail("Erro ao trocar de plano. Tente novamente.");
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
            sub.PendingSince = DateTime.UtcNow;
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
                _logger.LogError(ex, "Erro ao cancelar assinatura");
                return Response<MySubscriptionDto>.Fail("Erro ao cancelar assinatura. Tente novamente.");
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

                // force encurta o intervalo, mas não zera: cliques ou chamadas em série não viram
                // uma consulta ao Mercado Pago cada (o limite de requisições fica no controller).
                var interval = force ? ForcedSyncInterval
                    : sub?.PendingGatewaySubscriptionId is not null ? PendingSyncInterval : IdleSyncInterval;
                var recentlySynced = sub?.LastSyncedAt is not null && sub.LastSyncedAt > DateTime.UtcNow - interval;

                if (sub is not null && !recentlySynced)
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
                _logger.LogError(ex, "Erro ao verificar a assinatura");
                return Response<MySubscriptionDto>.Fail("Erro ao verificar a assinatura. Tente novamente.");
            }
        }

        public async Task<Response<SubscriptionCheckDto>> CheckPendingAsync(int userId)
        {
            try
            {
                var sub = await _context.Subscription
                    .AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .Select(s => new { s.PendingGatewaySubscriptionId, s.LastSyncedAt })
                    .FirstOrDefaultAsync();

                // Só fala com o Mercado Pago se houver checkout pendente (e não conferido há pouco).
                if (sub?.PendingGatewaySubscriptionId is not null &&
                    (sub.LastSyncedAt is null || sub.LastSyncedAt < DateTime.UtcNow - PendingSyncInterval))
                {
                    try { await SyncPreapprovalAsync(sub.PendingGatewaySubscriptionId); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao conferir o checkout pendente do usuário {UserId}", userId);
                        _context.ChangeTracker.Clear();
                    }
                }

                var plan = await _context.GetEffectivePlanAsync(userId);
                var pendingPlanCode = await _context.Subscription
                    .Where(s => s.UserId == userId && s.PendingPlan != null)
                    .Select(s => s.PendingPlan!.Code)
                    .FirstOrDefaultAsync();

                return Response<SubscriptionCheckDto>.Ok(new SubscriptionCheckDto
                {
                    PlanCode = plan.Code,
                    PlanName = plan.Name,
                    PendingPlanCode = pendingPlanCode,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar a assinatura");
                return Response<SubscriptionCheckDto>.Fail("Erro ao verificar a assinatura. Tente novamente.");
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

                // Troca manual = plano sem cobrança, que vale até o Admin mudar de novo.
                // A cobrança no Mercado Pago (e o pagamento aberto, se houver) precisa ser cancelada:
                // senão o lojista continuaria pagando, e a próxima conferência ou webhook trocaria
                // o plano de volta. Se o Mercado Pago falhar, nada muda aqui e o Admin tenta de novo.
                foreach (var gatewayId in new[] { sub.GatewaySubscriptionId, sub.PendingGatewaySubscriptionId })
                {
                    if (gatewayId is not null && !await EnsureCanceledAtGatewayAsync(gatewayId))
                        return Response<MySubscriptionDto>.Fail(
                            "Não foi possível cancelar a cobrança no Mercado Pago. O plano não foi alterado; tente de novo em instantes.");
                }

                sub.PlanId = plan.Id;
                sub.GatewaySubscriptionId = null;
                ClearPending(sub);
                sub.ScheduledPlanId = null;
                sub.Status = SubscriptionStatus.Active;
                sub.CurrentPeriodEnd = null;
                sub.PastDueSince = null;
                sub.CanceledAt = null;
                sub.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return await GetMySubscriptionAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar plano");
                return Response<MySubscriptionDto>.Fail("Erro ao alterar plano. Tente novamente.");
            }
        }

        // ===================== Mercado Pago -> Vitrio =====================

        public Task SyncPreapprovalAsync(string preapprovalId) => SyncPreapprovalAsync(preapprovalId, paymentApproved: false);

        // paymentApproved: veio de uma cobrança aprovada (webhook de pagamento). É a única prova
        // de pagamento que tira uma assinatura de "atrasada"/"suspensa" (ver ApplyActiveStatus).
        private async Task SyncPreapprovalAsync(string preapprovalId, bool paymentApproved)
        {
            // Nunca confia no conteúdo da notificação: busca o estado atual na API.
            var preapproval = await _mercadoPago.GetPreapprovalAsync(preapprovalId);
            _logger.LogInformation("Assinatura {Id} no Mercado Pago está com status {Status}", preapproval.Id, preapproval.Status);

            var sub = await _context.Subscription.FirstOrDefaultAsync(s =>
                s.GatewaySubscriptionId == preapproval.Id ||
                s.PendingGatewaySubscriptionId == preapproval.Id ||
                s.GatewaySubscriptionIdToCancel == preapproval.Id);

            if (sub is null)
            {
                _logger.LogInformation("Assinatura {Id} não está ligada a nenhum lojista; ignorada", preapproval.Id);
                return;
            }

            var now = DateTime.UtcNow;

            if (sub.GatewaySubscriptionIdToCancel == preapproval.Id)
                await CancelLeftoverAsync(sub, preapproval.Status);
            else if (sub.PendingGatewaySubscriptionId == preapproval.Id)
                await ApplyPendingStatusAsync(sub, preapproval, now);
            else
                ApplyActiveStatus(sub, preapproval, now, paymentApproved);

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
            // Se o Mercado Pago falhar agora, guarda o id e a manutenção tenta de novo até conseguir.
            if (previousGatewayId is not null && previousGatewayId != preapproval.Id &&
                !await TryCancelAtGatewayAsync(previousGatewayId))
            {
                if (sub.GatewaySubscriptionIdToCancel is not null && sub.GatewaySubscriptionIdToCancel != previousGatewayId)
                    _logger.LogError(
                        "Assinatura {Old} ainda não foi cancelada no Mercado Pago e agora {New} também precisa ser. " +
                        "Cancele {Old} manualmente no painel do Mercado Pago.",
                        sub.GatewaySubscriptionIdToCancel, previousGatewayId, sub.GatewaySubscriptionIdToCancel);
                sub.GatewaySubscriptionIdToCancel = previousGatewayId;
            }
        }

        // Assinatura antiga de um upgrade: só precisa terminar cancelada no Mercado Pago.
        private async Task CancelLeftoverAsync(Subscription sub, string? status)
        {
            if (sub.GatewaySubscriptionIdToCancel is null)
                return;
            if (status == "cancelled" || await TryCancelAtGatewayAsync(sub.GatewaySubscriptionIdToCancel))
                sub.GatewaySubscriptionIdToCancel = null;
        }

        // Assinatura paga que já estava valendo
        private static void ApplyActiveStatus(Subscription sub, MpPreapproval preapproval, DateTime now, bool paymentApproved)
        {
            switch (preapproval.Status)
            {
                case "authorized":
                    var previousEnd = sub.CurrentPeriodEnd;
                    // Datas de hoje/passado do sandbox são ignoradas.
                    var next = ValidNextPaymentDate(preapproval, now);

                    // Começou um período novo: a próxima cobrança ficou cerca de um mês depois do fim
                    // do período anterior (pequenos ajustes de data não contam).
                    var newPeriod = next is not null && previousEnd is not null && next > previousEnd.Value.AddDays(20);

                    // Downgrade agendado vale a partir do período novo. Precisa ser aplicado ANTES de
                    // avançar a data: depois disso, "o período acabou?" nunca seria verdade.
                    if (newPeriod)
                        ApplyScheduledPlan(sub, now);
                    else
                        ApplyScheduledPlanIfDue(sub, now);

                    // Só avança a data (renovação).
                    if (next is not null && (previousEnd is null || next > previousEnd))
                        sub.CurrentPeriodEnd = next;

                    // "authorized" NÃO quer dizer que a última cobrança foi paga: a assinatura continua
                    // autorizada enquanto o Mercado Pago tenta cobrar de novo. Por isso só sai de
                    // atrasada/suspensa com uma cobrança aprovada (webhook de pagamento).
                    if ((sub.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Suspended) && paymentApproved)
                    {
                        sub.Status = SubscriptionStatus.Active;
                        sub.PastDueSince = null;
                    }
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
                await SyncPreapprovalAsync(charge.PreapprovalId, paymentApproved: true);
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

            // 3) Checkouts abertos há mais de 10 min (e não conferidos nos últimos 10): confere.
            //    Os abertos há mais de 2 dias são descartados. A idade vem de PendingSince, que as
            //    conferências não alteram (UpdatedDate muda a cada conferência).
            var pending = await _context.Subscription
                .Where(s => s.PendingGatewaySubscriptionId != null &&
                            (s.PendingSince ?? s.CreationDate) < now.AddMinutes(-10) &&
                            (s.LastSyncedAt == null || s.LastSyncedAt < now.AddMinutes(-10)))
                .OrderBy(s => s.LastSyncedAt)
                .Select(s => new { s.Id, GatewayId = s.PendingGatewaySubscriptionId!, Since = s.PendingSince ?? s.CreationDate })
                .Take(50)
                .ToListAsync();

            foreach (var item in pending)
            {
                // Confere antes: pode ter sido pago agora.
                try { await SyncPreapprovalAsync(item.GatewayId); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao conferir pagamento aberto {Id}", item.GatewayId);
                    _context.ChangeTracker.Clear(); // não deixa uma falha contaminar os próximos
                }

                if (item.Since >= now.AddDays(-2))
                    continue;

                try
                {
                    var sub = await _context.Subscription.FirstAsync(s => s.Id == item.Id);
                    if (sub.PendingGatewaySubscriptionId != item.GatewayId)
                        continue; // foi pago ou trocado nesse meio-tempo

                    // Só descarta se conseguir cancelar no Mercado Pago; senão o link continuaria
                    // pagável e o pagamento não seria ligado a ninguém. Tenta de novo na próxima rodada.
                    if (!await TryCancelAtGatewayAsync(item.GatewayId))
                        continue;

                    ClearPending(sub);
                    sub.UpdatedDate = now;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao descartar pagamento abandonado {Id}", item.GatewayId);
                    _context.ChangeTracker.Clear();
                }
            }

            // 4) Assinaturas antigas de upgrades cujo cancelamento no Mercado Pago falhou: tenta de novo.
            var leftoverIds = await _context.Subscription
                .Where(s => s.GatewaySubscriptionIdToCancel != null)
                .Select(s => s.Id)
                .Take(50)
                .ToListAsync();

            foreach (var subId in leftoverIds)
            {
                try
                {
                    var s = await _context.Subscription.FirstAsync(x => x.Id == subId);
                    if (s.GatewaySubscriptionIdToCancel is null)
                        continue;

                    string? status;
                    try { status = (await _mercadoPago.GetPreapprovalAsync(s.GatewaySubscriptionIdToCancel)).Status; }
                    catch (MercadoPagoException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
                    {
                        status = "cancelled"; // não existe mais no Mercado Pago
                    }

                    await CancelLeftoverAsync(s, status);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao cancelar a assinatura antiga da assinatura {SubId}", subId);
                    _context.ChangeTracker.Clear();
                }
            }

            // 5) Assinaturas pagas não conferidas há 6h: pega mudanças feitas direto no
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao conferir assinatura {Id}", id);
                    _context.ChangeTracker.Clear();
                }
            }
        }

        // Checkouts abertos nas últimas 2 horas: confere a cada minuto (chamado pelo
        // SubscriptionMaintenanceService). Assim o plano muda logo depois do pagamento mesmo
        // que o webhook não chegue e nenhuma tela esteja aberta.
        public async Task SyncRecentCheckoutsAsync()
        {
            var now = DateTime.UtcNow;
            var ids = await _context.Subscription
                .Where(s => s.PendingGatewaySubscriptionId != null &&
                            s.PendingSince != null && s.PendingSince > now.AddHours(-2) &&
                            (s.LastSyncedAt == null || s.LastSyncedAt < now.AddSeconds(-50)))
                .OrderBy(s => s.LastSyncedAt)
                .Select(s => s.PendingGatewaySubscriptionId!)
                .Take(50)
                .ToListAsync();

            foreach (var id in ids)
            {
                try { await SyncPreapprovalAsync(id); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao conferir checkout recente {Id}", id);
                    _context.ChangeTracker.Clear();
                }
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
            s.PendingSince = null;
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
            if (s.CurrentPeriodEnd is null || s.CurrentPeriodEnd > now)
                return;
            ApplyScheduledPlan(s, now);
        }

        private static void ApplyScheduledPlan(Subscription s, DateTime now)
        {
            if (s.ScheduledPlanId is null)
                return;
            s.PlanId = s.ScheduledPlanId.Value;
            s.ScheduledPlanId = null;
            s.UpdatedDate = now;
        }

        // true = a assinatura ficou cancelada no Mercado Pago (ou nem existe mais lá).
        private async Task<bool> TryCancelAtGatewayAsync(string gatewayId)
        {
            try
            {
                await _mercadoPago.CancelPreapprovalAsync(gatewayId);
                return true;
            }
            catch (MercadoPagoException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
            {
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível cancelar a assinatura {Id} no Mercado Pago", gatewayId);
                return false;
            }
        }

        // Como TryCancelAtGatewayAsync, mas confere antes: uma assinatura que já está cancelada
        // no Mercado Pago (ex.: o lojista cancelou e ainda está no período pago) conta como feita.
        private async Task<bool> EnsureCanceledAtGatewayAsync(string gatewayId)
        {
            try
            {
                if ((await _mercadoPago.GetPreapprovalAsync(gatewayId)).Status == "cancelled")
                    return true;
            }
            catch (MercadoPagoException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
            {
                return true; // não existe mais no Mercado Pago
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível consultar a assinatura {Id} no Mercado Pago", gatewayId);
                return false;
            }

            return await TryCancelAtGatewayAsync(gatewayId);
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