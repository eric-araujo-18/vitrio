using System.Globalization;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.Payments;
using BackendSystemVitrio.Services.StorePaymentService;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.OrderPaymentService
{
    /*
      PAGAMENTO ONLINE DO PEDIDO

      1. O cliente escolhe "pagar agora": o pedido nasce AwaitingPayment, com o estoque reservado
         até PaymentDeadline, e ganha uma preferência do Checkout Pro criada COM O TOKEN DA LOJA
         (o dinheiro cai na conta dela; MarketplaceFeePercent > 0 desconta a comissão do Vitrio).
      2. Quem confirma o pagamento: a notificação do Mercado Pago, a página da vitrine quando o
         cliente volta do checkout e a manutenção a cada minuto. Nenhum deles confia no que
         recebe: o pagamento é sempre lido na API do Mercado Pago, com o token da loja, e precisa
         ter a external_reference do pedido e o valor certo.
      3. Pago: AwaitingPayment -> Pending (só então o lojista é avisado, como num pedido comum).
         Prazo vencido sem pagamento: Canceled, e o estoque volta.
      4. As mudanças de status são UPDATEs condicionais ("só se ainda estiver AwaitingPayment"),
         porque a notificação, a vitrine e a manutenção podem chegar ao mesmo tempo.
      5. Pagamento aprovado de um pedido que já foi cancelado (pagou no último segundo) ou um
         segundo pagamento do mesmo pedido: estorno automático.
    */
    public class OrderPaymentService : IOrderPaymentService
    {
        // Folga depois do prazo antes de cancelar (o pagamento pode estar sendo aprovado agora).
        private static readonly TimeSpan ExpireGrace = TimeSpan.FromMinutes(2);
        // A vitrine confere no Mercado Pago no máximo a cada 5s por pedido.
        private static readonly TimeSpan PublicSyncInterval = TimeSpan.FromSeconds(5);
        // A manutenção confere cada pedido esperando pagamento no máximo a cada ~1 min.
        private static readonly TimeSpan MaintenanceSyncInterval = TimeSpan.FromSeconds(50);
        // Se nem assim der para conferir (ex.: Mercado Pago fora do ar), desiste depois de 1 dia.
        private static readonly TimeSpan GiveUpSyncAfter = TimeSpan.FromDays(1);

        // O Mercado Pago só aceita um Pix que vença pelo menos 30 minutos depois de gerado; com
        // menos, o Pix nem aparece no checkout. Por isso o cliente só pode começar a pagar até 30
        // minutos antes do prazo do pedido, e todo Pix vence no prazo: pago dentro da validade,
        // sempre chega antes de o pedido expirar.
        public static readonly TimeSpan PixMinimumValidity = TimeSpan.FromMinutes(30);
        public static DateTime CheckoutClosesAt(DateTime deadline) => deadline - PixMinimumValidity;

        private readonly AppDbContext _context;
        private readonly IMercadoPagoMarketplaceClient _mercadoPago;
        private readonly IStorePaymentService _storePayments;
        private readonly MercadoPagoOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrderPaymentService> _logger;

        public OrderPaymentService(
            AppDbContext context,
            IMercadoPagoMarketplaceClient mercadoPago,
            IStorePaymentService storePayments,
            IOptions<MercadoPagoOptions> options,
            IConfiguration configuration,
            ILogger<OrderPaymentService> logger)
        {
            _context = context;
            _mercadoPago = mercadoPago;
            _storePayments = storePayments;
            _options = options.Value;
            _configuration = configuration;
            _logger = logger;
        }

        private static string Reference(int orderId) => $"vitrio-order-{orderId}";

        private static bool TryReadReference(string? reference, out int orderId)
        {
            orderId = 0;
            return reference is not null && reference.StartsWith("vitrio-order-", StringComparison.Ordinal) &&
                   int.TryParse(reference["vitrio-order-".Length..], out orderId);
        }

        // ISO 8601 em UTC. Com "Z" e não "+00:00": o System.Text.Json escreve o "+" como "+",
        // e o Mercado Pago não reconhece a data assim (error_parsing_date).
        private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        public async Task<bool> IsAvailableAsync(int storeId, Plan plan)
            => _options.OnlinePaymentConfigured &&
               plan.AllowsOnlinePayment &&
               await _context.StorePaymentAccount.AnyAsync(a => a.StoreId == storeId);

        public async Task<string> StartCheckoutAsync(Order order)
        {
            var token = await _storePayments.GetAccessTokenAsync(order.StoreId)
                        ?? throw new InvalidOperationException($"A loja {order.StoreId} não tem conta do Mercado Pago conectada.");
            var store = await _context.Store.AsNoTracking()
                .Where(s => s.Id == order.StoreId)
                .Select(s => new { s.Slug, LiveMode = _context.StorePaymentAccount.Where(a => a.StoreId == s.Id).Select(a => a.LiveMode).FirstOrDefault() })
                .FirstAsync();

            var api = _options.PublicApiUrl.TrimEnd('/');
            var returnUrl = $"{api}/api/Public/stores/{store.Slug}/orders/{order.Code}/payment-return";
            var deadline = order.PaymentDeadline ?? DateTime.UtcNow.AddMinutes(_options.EffectiveOrderPaymentMinutes);
            var fee = Math.Round(order.Total * _options.MarketplaceFeePercent / 100m, 2);

            // E-mail sugerido no checkout. Nos testes (TestPayerEmail ou TestOrderPayerEmail preenchido),
            // o do cliente não vai: um e-mail real com uma conta de teste o Mercado Pago recusa. E o
            // TestPayerEmail também não, porque é o do lojista (quem recebe): ninguém paga a si mesmo.
            // Em produção os dois ficam vazios e vale o e-mail do cliente.
            var testing = !string.IsNullOrWhiteSpace(_options.TestPayerEmail) || !string.IsNullOrWhiteSpace(_options.TestOrderPayerEmail);
            var payerEmail = testing
                ? (string.IsNullOrWhiteSpace(_options.TestOrderPayerEmail) ? null : _options.TestOrderPayerEmail)
                : order.CustomerEmail;

            var preference = await _mercadoPago.CreatePreferenceAsync(token, new MpPreferenceRequest
            {
                Items = order.Items.Select(i => new MpPreferenceItem
                {
                    Id = i.ProductId?.ToString(),
                    Title = Describe(i),
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    PictureUrl = i.ImageUrl,
                }).ToList(),
                Payer = new { name = order.CustomerName, email = payerEmail },
                ExternalReference = Reference(order.Id),
                NotificationUrl = $"{api}/api/Webhooks/mercadopago/orders/{order.StoreId}?source_news=webhooks",
                BackUrls = new { success = returnUrl, pending = returnUrl, failure = returnUrl },
                AutoReturn = "approved",
                // Dá para começar a pagar até CheckoutClosesAt; o Pix gerado vence no prazo do pedido.
                Expires = true,
                ExpirationDateFrom = Iso(DateTime.UtcNow.AddMinutes(-1)),
                ExpirationDateTo = Iso(CheckoutClosesAt(deadline)),
                DateOfExpiration = Iso(deadline),
                // Boleto demora dias para compensar: não cabe no prazo do pedido.
                PaymentMethods = new { excluded_payment_types = new[] { new { id = "ticket" } }, installments = 12 },
                MarketplaceFee = fee > 0 ? fee : null,
                Metadata = new { order_id = order.Id, store_id = order.StoreId },
            }, idempotencyKey: $"vitrio-preference-{order.Id}");

            var checkoutUrl = store.LiveMode
                ? preference.InitPoint
                : preference.SandboxInitPoint ?? preference.InitPoint;
            if (string.IsNullOrWhiteSpace(checkoutUrl))
                throw new MercadoPagoException(200, "O Mercado Pago não devolveu o link de pagamento.");

            await _context.Order.Where(o => o.Id == order.Id).ExecuteUpdateAsync(set => set
                .SetProperty(o => o.PaymentPreferenceId, preference.Id)
                .SetProperty(o => o.PaymentCheckoutUrl, checkoutUrl));

            return checkoutUrl;
        }

        public async Task<bool> CancelUnpaidAsync(int orderId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;

            var affected = await _context.Order
                .Where(o => o.Id == orderId && o.Status == OrderStatus.AwaitingPayment)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(o => o.Status, OrderStatus.Canceled)
                    .SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Canceled)
                    .SetProperty(o => o.UpdatedDate, now));

            if (affected == 0)
                return false;

            var items = await _context.OrderItem.AsNoTracking().Where(i => i.OrderId == orderId).ToListAsync();
            await _context.RestoreStockAsync(items);
            await transaction.CommitAsync();
            return true;
        }

        public async Task<bool> RefundAsync(Order order)
        {
            if (order.GatewayPaymentId is null)
                return false;

            var token = await _storePayments.GetAccessTokenAsync(order.StoreId);
            if (token is null)
            {
                _logger.LogWarning("Pedido {OrderId}: sem conta do Mercado Pago conectada para estornar", order.Id);
                return false;
            }

            return await TryRefundAsync(token, order.GatewayPaymentId);
        }

        public async Task HandleNotificationAsync(int storeId, string paymentId)
        {
            var token = await _storePayments.GetAccessTokenAsync(storeId);
            if (token is null)
            {
                _logger.LogInformation("Aviso de pagamento {PaymentId} para a loja {StoreId}, que não tem conta conectada; ignorado", paymentId, storeId);
                return;
            }

            MpPayment payment;
            try
            {
                payment = await _mercadoPago.GetPaymentAsync(token, paymentId);
            }
            catch (MercadoPagoException ex) when (ex.StatusCode is 400 or 401 or 403 or 404)
            {
                // Pagamento que não existe ou não é desta loja (aviso falso ou de outro vendedor).
                _logger.LogWarning("Aviso de pagamento {PaymentId} da loja {StoreId} não confere no Mercado Pago ({Status})", paymentId, storeId, ex.StatusCode);
                return;
            }

            if (!TryReadReference(payment.ExternalReference, out var orderId) ||
                !await _context.Order.AnyAsync(o => o.Id == orderId && o.StoreId == storeId))
                return;

            await ApplyAsync(orderId, payment, token);
        }

        public async Task<Response<OrderPaymentInfoDto>> GetPublicStatusAsync(string storeSlug, string code)
        {
            try
            {
                code = code.Trim().ToUpperInvariant();
                var order = await FindByCodeAsync(storeSlug, code);
                if (order is null)
                    return Response<OrderPaymentInfoDto>.Fail("Pedido não encontrado.");

                if (order.Status == OrderStatus.AwaitingPayment &&
                    (order.PaymentLastSyncedAt is null || order.PaymentLastSyncedAt < DateTime.UtcNow - PublicSyncInterval))
                {
                    try
                    {
                        await SyncAsync(order.Id, order.StoreId);
                        order = (await FindByCodeAsync(storeSlug, code))!;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao conferir o pagamento do pedido {OrderId}", order.Id);
                        _context.ChangeTracker.Clear();
                    }
                }

                var closesAt = order.PaymentDeadline is null ? (DateTime?)null : CheckoutClosesAt(order.PaymentDeadline.Value);
                var stillPayable = order.Status == OrderStatus.AwaitingPayment && closesAt > DateTime.UtcNow;
                return Response<OrderPaymentInfoDto>.Ok(new OrderPaymentInfoDto
                {
                    Code = order.Code,
                    Status = order.Status,
                    PaymentMethod = order.PaymentMethod,
                    PaymentStatus = order.PaymentStatus,
                    Total = order.Total,
                    PaymentDeadline = closesAt,
                    CheckoutUrl = stillPayable ? order.PaymentCheckoutUrl : null,
                    StorePhone = order.Store?.Phone,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar o pagamento do pedido {Code}", code);
                return Response<OrderPaymentInfoDto>.Fail("Erro ao buscar o pagamento. Tente novamente.");
            }
        }

        public async Task<string> GetReturnUrlAsync(string storeSlug, string code)
        {
            var frontend = (_configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
            code = code.Trim().ToUpperInvariant();

            // Slug e código saem do banco (nunca da URL): o redirecionamento não vira "open redirect".
            var found = await _context.Order.AsNoTracking()
                .Where(o => o.Code == code && o.Store!.Slug == storeSlug)
                .Select(o => new { o.Code, o.Store!.Slug })
                .FirstOrDefaultAsync();
            if (found is not null)
                return $"{frontend}/store/{found.Slug}/client?pedido={found.Code}";

            var slug = await _context.Store.AsNoTracking().Where(s => s.Slug == storeSlug).Select(s => s.Slug).FirstOrDefaultAsync();
            return slug is null ? frontend : $"{frontend}/store/{slug}/client";
        }

        public async Task RunMaintenanceAsync()
        {
            var now = DateTime.UtcNow;

            // 1) Prazo vencido: confere uma última vez e, sem pagamento, cancela e devolve o estoque.
            var overdue = await _context.Order.AsNoTracking()
                .Where(o => o.Status == OrderStatus.AwaitingPayment && o.PaymentDeadline < now - ExpireGrace)
                .OrderBy(o => o.PaymentDeadline)
                .Select(o => new { o.Id, o.StoreId, Deadline = o.PaymentDeadline!.Value })
                .Take(50)
                .ToListAsync();

            foreach (var order in overdue)
            {
                try
                {
                    await SyncAsync(order.Id, order.StoreId);
                }
                catch (Exception ex)
                {
                    _context.ChangeTracker.Clear();
                    // Sem conferir não dá para saber se foi pago: tenta de novo na próxima rodada.
                    if (order.Deadline > now - GiveUpSyncAfter)
                    {
                        _logger.LogWarning(ex, "Falha ao conferir o pedido vencido {OrderId}; tenta de novo depois", order.Id);
                        continue;
                    }
                    _logger.LogError(ex, "Pedido {OrderId} vencido há mais de 1 dia sem conseguir conferir o pagamento; cancelando", order.Id);
                }

                try
                {
                    if (await CancelUnpaidAsync(order.Id))
                        _logger.LogInformation("Pedido {OrderId} cancelado: o prazo para pagar acabou", order.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao cancelar o pedido vencido {OrderId}", order.Id);
                    _context.ChangeTracker.Clear();
                }
            }

            // 2) Ainda no prazo: confere, para o pedido mudar logo mesmo sem a notificação.
            var waiting = await _context.Order.AsNoTracking()
                .Where(o => o.Status == OrderStatus.AwaitingPayment && o.PaymentDeadline >= now - ExpireGrace &&
                            (o.PaymentLastSyncedAt == null || o.PaymentLastSyncedAt < now - MaintenanceSyncInterval))
                .OrderBy(o => o.PaymentLastSyncedAt)
                .Select(o => new { o.Id, o.StoreId })
                .Take(50)
                .ToListAsync();

            foreach (var order in waiting)
            {
                try { await SyncAsync(order.Id, order.StoreId); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao conferir o pagamento do pedido {OrderId}", order.Id);
                    _context.ChangeTracker.Clear();
                }
            }
        }

        // ===== Helpers =====

        private Task<Order?> FindByCodeAsync(string storeSlug, string code)
            => _context.Order.AsNoTracking()
                .Include(o => o.Store)
                .FirstOrDefaultAsync(o => o.Code == code && o.Store!.Slug == storeSlug);

        // Procura no Mercado Pago os pagamentos do pedido e aplica o que encontrar.
        private async Task SyncAsync(int orderId, int storeId)
        {
            var token = await _storePayments.GetAccessTokenAsync(storeId);
            if (token is null)
                return;

            var payments = await _mercadoPago.SearchPaymentsAsync(token, Reference(orderId));
            // Primeiro os aprovados (o que muda o pedido); depois estornos de um já aprovado.
            foreach (var payment in payments.Where(p => p.Status == "approved")
                         .Concat(payments.Where(p => p.Status is "refunded" or "charged_back")))
                await ApplyAsync(orderId, payment, token);

            await _context.Order.Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.PaymentLastSyncedAt, DateTime.UtcNow));
        }

        private async Task ApplyAsync(int orderId, MpPayment payment, string token)
        {
            if (payment.ExternalReference != Reference(orderId))
                return;

            var paymentId = payment.Id.ToString();
            var now = DateTime.UtcNow;

            switch (payment.Status)
            {
                case "approved":
                {
                    var order = await _context.Order.AsNoTracking().FirstAsync(o => o.Id == orderId);
                    if ((payment.CurrencyId is not null && payment.CurrencyId != "BRL") || payment.TransactionAmount < order.Total - 0.01m)
                    {
                        _logger.LogError("Pagamento {PaymentId} do pedido {OrderId} com valor {Amount} {Currency}, esperado {Total} BRL; ignorado",
                            paymentId, orderId, payment.TransactionAmount, payment.CurrencyId, order.Total);
                        return;
                    }

                    var paidAt = payment.DateApproved?.ToUniversalTime() ?? now;
                    var affected = await _context.Order
                        .Where(o => o.Id == orderId && o.Status == OrderStatus.AwaitingPayment)
                        .ExecuteUpdateAsync(set => set
                            .SetProperty(o => o.Status, OrderStatus.Pending)
                            .SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Approved)
                            .SetProperty(o => o.GatewayPaymentId, paymentId)
                            .SetProperty(o => o.PaidAt, paidAt)
                            .SetProperty(o => o.UpdatedDate, now));

                    if (affected == 1)
                    {
                        _logger.LogInformation("Pedido {OrderId} pago (pagamento {PaymentId})", orderId, paymentId);
                        return;
                    }

                    // Não esperava mais pagamento: veja por quê.
                    order = await _context.Order.AsNoTracking().FirstAsync(o => o.Id == orderId);
                    if (order.GatewayPaymentId == paymentId)
                        return; // este pagamento já foi registrado

                    if (order.PaymentStatus is OrderPaymentStatus.Approved or OrderPaymentStatus.Refunded)
                    {
                        // Segundo pagamento do mesmo pedido (ex.: pagou duas vezes): devolve este.
                        if (await TryRefundAsync(token, paymentId))
                            _logger.LogWarning("Pagamento duplicado {PaymentId} do pedido {OrderId} estornado", paymentId, orderId);
                        else
                            _logger.LogError("Pagamento duplicado {PaymentId} do pedido {OrderId} NÃO foi estornado: estorne no Mercado Pago", paymentId, orderId);
                        return;
                    }

                    if (order.Status == OrderStatus.Canceled)
                    {
                        // Pago depois de o pedido ser cancelado (prazo vencido ou cancelado pelo lojista).
                        var refunded = await TryRefundAsync(token, paymentId);
                        await _context.Order.Where(o => o.Id == orderId && o.Status == OrderStatus.Canceled)
                            .ExecuteUpdateAsync(set => set
                                .SetProperty(o => o.PaymentStatus, refunded ? OrderPaymentStatus.Refunded : OrderPaymentStatus.Approved)
                                .SetProperty(o => o.GatewayPaymentId, paymentId)
                                .SetProperty(o => o.PaidAt, paidAt)
                                .SetProperty(o => o.UpdatedDate, now));

                        if (refunded)
                            _logger.LogWarning("Pedido {OrderId} já estava cancelado quando o pagamento {PaymentId} foi aprovado; estornado", orderId, paymentId);
                        else
                            _logger.LogError("Pedido {OrderId} cancelado foi pago ({PaymentId}) e o estorno falhou: estorne no Mercado Pago", orderId, paymentId);
                    }
                    return;
                }

                case "refunded":
                case "charged_back":
                    // Estornado (ou contestado) depois de aprovado, por exemplo pelo painel do Mercado Pago.
                    await _context.Order
                        .Where(o => o.Id == orderId && o.GatewayPaymentId == paymentId && o.PaymentStatus == OrderPaymentStatus.Approved)
                        .ExecuteUpdateAsync(set => set
                            .SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Refunded)
                            .SetProperty(o => o.UpdatedDate, now));
                    return;

                // pending, in_process, rejected, cancelled...: o cliente ainda pode tentar de novo até o prazo.
            }
        }

        private async Task<bool> TryRefundAsync(string token, string paymentId)
        {
            try
            {
                await _mercadoPago.RefundPaymentAsync(token, paymentId, idempotencyKey: $"vitrio-refund-{paymentId}");
                return true;
            }
            catch (MercadoPagoException ex)
            {
                // Já estornado (pelo painel do Mercado Pago, por exemplo) conta como feito.
                try
                {
                    var current = await _mercadoPago.GetPaymentAsync(token, paymentId);
                    if (current.Status is "refunded" or "charged_back")
                        return true;
                }
                catch (MercadoPagoException) { }

                _logger.LogError(ex, "Não foi possível estornar o pagamento {PaymentId}", paymentId);
                return false;
            }
        }

        private static string Describe(OrderItem item)
        {
            var extras = new[] { item.Color, item.Size is null ? null : $"Tam. {item.Size}" }.Where(x => !string.IsNullOrWhiteSpace(x));
            var title = extras.Any() ? $"{item.ProductName} ({string.Join(", ", extras)})" : item.ProductName;
            return title.Length > 250 ? title[..250] : title;
        }
    }
}
