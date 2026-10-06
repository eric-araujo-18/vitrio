using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.Payments;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.StorePaymentService
{
    public class StorePaymentService : IStorePaymentService
    {
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);
        // Renova o token na hora de usar se faltar menos que isso para vencer.
        private static readonly TimeSpan RefreshOnUseWithin = TimeSpan.FromDays(7);
        // A manutenção renova com folga (o token vale ~180 dias).
        private static readonly TimeSpan RefreshAheadWithin = TimeSpan.FromDays(30);

        private readonly AppDbContext _context;
        private readonly IMercadoPagoMarketplaceClient _mercadoPago;
        private readonly PaymentTokenProtector _protector;
        private readonly MercadoPagoOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StorePaymentService> _logger;

        public StorePaymentService(
            AppDbContext context,
            IMercadoPagoMarketplaceClient mercadoPago,
            PaymentTokenProtector protector,
            IOptions<MercadoPagoOptions> options,
            IConfiguration configuration,
            ILogger<StorePaymentService> logger)
        {
            _context = context;
            _mercadoPago = mercadoPago;
            _protector = protector;
            _options = options.Value;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Response<StorePaymentStatusDto>> GetStatusAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StorePaymentStatusDto>.Fail("Loja não encontrada.");

                return Response<StorePaymentStatusDto>.Ok(await BuildStatusAsync(store));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar o pagamento online da loja {StoreId}", storeId);
                return Response<StorePaymentStatusDto>.Fail("Erro ao buscar o pagamento online. Tente novamente.");
            }
        }

        public async Task<Response<StorePaymentConnectDto>> StartConnectAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StorePaymentConnectDto>.Fail("Loja não encontrada.");

                if (!_options.OnlinePaymentConfigured)
                    return Response<StorePaymentConnectDto>.Fail("O pagamento online ainda não está disponível no Vitrio.");

                var plan = await _context.GetEffectivePlanAsync(userId);
                if (!plan.AllowsOnlinePayment)
                    return Response<StorePaymentConnectDto>.Fail(
                        $"O plano {plan.Name} não inclui pagamento online. Veja os planos em Assinatura.");

                var state = _protector.CreateState(store.Id, userId, StateLifetime);
                return Response<StorePaymentConnectDto>.Ok(new StorePaymentConnectDto
                {
                    AuthorizationUrl = _mercadoPago.BuildAuthorizationUrl(state),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar a conexão com o Mercado Pago da loja {StoreId}", storeId);
                return Response<StorePaymentConnectDto>.Fail("Não foi possível conectar ao Mercado Pago agora. Tente novamente.");
            }
        }

        public async Task<string> CompleteConnectAsync(string? code, string? state, string? error)
        {
            if (!_protector.TryReadState(state, out var storeId, out var userId))
            {
                _logger.LogWarning("Volta do OAuth do Mercado Pago com state inválido ou vencido");
                return Frontend("/menu/initialpage");
            }

            var store = await _context.FindOwnedStoreAsync(storeId, userId);
            if (store is null)
                return Frontend("/menu/initialpage");

            // O lojista desistiu na tela do Mercado Pago.
            if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
                return SettingsPage(store, "cancelado");

            try
            {
                var token = await _mercadoPago.ExchangeCodeAsync(code);
                var now = DateTime.UtcNow;

                var account = await _context.StorePaymentAccount.FirstOrDefaultAsync(a => a.StoreId == store.Id);
                if (account is null)
                {
                    account = new StorePaymentAccount
                    {
                        StoreId = store.Id,
                        MercadoPagoUserId = "",
                        AccessTokenEncrypted = "",
                        RefreshTokenEncrypted = "",
                    };
                    _context.StorePaymentAccount.Add(account);
                }
                else
                {
                    account.UpdatedDate = now;
                }

                account.MercadoPagoUserId = token.UserId.ToString();
                account.AccessTokenEncrypted = _protector.Encrypt(token.AccessToken);
                account.RefreshTokenEncrypted = _protector.Encrypt(token.RefreshToken);
                account.ExpiresAt = now.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 180 * 24 * 3600);
                account.LiveMode = token.LiveMode;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Loja {StoreId} conectou a conta {MpUserId} do Mercado Pago", store.Id, account.MercadoPagoUserId);
                return SettingsPage(store, "conectado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao concluir a conexão com o Mercado Pago da loja {StoreId}", store.Id);
                return SettingsPage(store, "erro");
            }
        }

        public async Task<Response<StorePaymentStatusDto>> DisconnectAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StorePaymentStatusDto>.Fail("Loja não encontrada.");

                // Sem o token, o Vitrio não consegue conferir nem estornar esses pagamentos.
                if (await _context.Order.AnyAsync(o => o.StoreId == store.Id && o.Status == OrderStatus.AwaitingPayment))
                    return Response<StorePaymentStatusDto>.Fail(
                        $"Há pedidos esperando pagamento. Espere eles serem pagos ou expirarem (até {_options.EffectiveOrderPaymentMinutes} minutos) para desconectar.");

                await _context.StorePaymentAccount.Where(a => a.StoreId == store.Id).ExecuteDeleteAsync();
                return Response<StorePaymentStatusDto>.Ok(await BuildStatusAsync(store), "Conta do Mercado Pago desconectada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao desconectar o Mercado Pago da loja {StoreId}", storeId);
                return Response<StorePaymentStatusDto>.Fail("Erro ao desconectar. Tente novamente.");
            }
        }

        public async Task<string?> GetAccessTokenAsync(int storeId)
        {
            var account = await _context.StorePaymentAccount.FirstOrDefaultAsync(a => a.StoreId == storeId);
            if (account is null)
                return null;

            if (account.ExpiresAt < DateTime.UtcNow.Add(RefreshOnUseWithin))
            {
                try
                {
                    await RefreshAsync(account);
                }
                catch (Exception ex) when (account.ExpiresAt > DateTime.UtcNow)
                {
                    // Ainda vale: usa o atual e tenta renovar de novo depois.
                    _logger.LogWarning(ex, "Não foi possível renovar o token do Mercado Pago da loja {StoreId}", storeId);
                }
            }

            return _protector.Decrypt(account.AccessTokenEncrypted);
        }

        public async Task RefreshExpiringTokensAsync()
        {
            var limit = DateTime.UtcNow.Add(RefreshAheadWithin);
            var ids = await _context.StorePaymentAccount
                .Where(a => a.ExpiresAt < limit)
                .OrderBy(a => a.ExpiresAt)
                .Select(a => a.Id)
                .Take(50)
                .ToListAsync();

            foreach (var id in ids)
            {
                try
                {
                    var account = await _context.StorePaymentAccount.FirstAsync(a => a.Id == id);
                    await RefreshAsync(account);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Não foi possível renovar o token do Mercado Pago da conta {AccountId}", id);
                    _context.ChangeTracker.Clear();
                }
            }
        }

        // ===== Helpers =====

        private async Task RefreshAsync(StorePaymentAccount account)
        {
            var token = await _mercadoPago.RefreshTokenAsync(_protector.Decrypt(account.RefreshTokenEncrypted));
            var now = DateTime.UtcNow;
            account.AccessTokenEncrypted = _protector.Encrypt(token.AccessToken);
            if (!string.IsNullOrEmpty(token.RefreshToken))
                account.RefreshTokenEncrypted = _protector.Encrypt(token.RefreshToken);
            account.ExpiresAt = now.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 180 * 24 * 3600);
            account.UpdatedDate = now;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Token do Mercado Pago da loja {StoreId} renovado", account.StoreId);
        }

        private async Task<StorePaymentStatusDto> BuildStatusAsync(Store store)
        {
            var plan = await _context.GetEffectivePlanAsync(store.UserId);
            var account = await _context.StorePaymentAccount.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == store.Id);
            var configured = _options.OnlinePaymentConfigured;

            return new StorePaymentStatusDto
            {
                Configured = configured,
                PlanAllows = plan.AllowsOnlinePayment,
                Connected = account is not null,
                Available = configured && plan.AllowsOnlinePayment && account is not null,
                MercadoPagoUserId = account?.MercadoPagoUserId,
                LiveMode = account?.LiveMode ?? false,
                ConnectedAt = account is null ? null : account.UpdatedDate ?? account.CreationDate,
                AwaitingPaymentOrders = await _context.Order.CountAsync(o =>
                    o.StoreId == store.Id && o.Status == OrderStatus.AwaitingPayment),
            };
        }

        private string Frontend(string path)
            => (_configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/') + path;

        // O slug vem do banco (nunca da URL), então pode ir direto no endereço.
        private string SettingsPage(Store store, string result)
            => Frontend($"/store/{store.Slug}/shopkeeper/configuracoes?mercadopago={result}");
    }
}
