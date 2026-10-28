using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.AdminService
{
    // Painel do Admin (/menu/admin): números gerais e busca de contas para o suporte. A troca de
    // plano fica em SubscriptionService.AdminSetPlanAsync.
    public class AdminService : IAdminService
    {
        private const int MaxResults = 50;

        private readonly AppDbContext _context;
        private readonly ILogger<AdminService> _logger;

        public AdminService(AppDbContext context, ILogger<AdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Response<AdminOverviewDto>> GetOverviewAsync()
        {
            try
            {
                var users = _context.User.Where(u => u.DeletionDate == null);
                var since = DateTime.UtcNow.AddDays(-30);

                // Assinaturas pagas: com cobrança no Mercado Pago e ainda valendo.
                var paid = await _context.Subscription
                    .Where(s => s.GatewaySubscriptionId != null &&
                                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue ||
                                 s.Status == SubscriptionStatus.Trial))
                    .GroupBy(s => new { s.Plan!.Code, s.Plan.Name, s.Plan.PriceMonthly, s.Plan.SortOrder })
                    .Select(g => new { g.Key.Code, g.Key.Name, g.Key.PriceMonthly, g.Key.SortOrder, Count = g.Count() })
                    .OrderBy(g => g.SortOrder)
                    .ToListAsync();

                return Response<AdminOverviewDto>.Ok(new AdminOverviewDto
                {
                    Shopkeepers = await users.CountAsync(u => u.Role == Role.Shopkeeper),
                    UnconfirmedShopkeepers = await users.CountAsync(u => u.Role == Role.Shopkeeper && u.EmailConfirmedAt == null),
                    Clients = await users.CountAsync(u => u.Role == Role.Client),
                    ActiveStores = await _context.Store.CountAsync(s => s.IsActive && s.DeletionDate == null),
                    OrdersLast30Days = await _context.Order.CountAsync(o =>
                        o.CreationDate >= since && o.Status != OrderStatus.Canceled && o.Status != OrderStatus.AwaitingPayment),
                    PaidSubscriptions = paid
                        .Select(p => new AdminPlanCountDto { PlanCode = p.Code, PlanName = p.Name, Count = p.Count })
                        .ToList(),
                    MonthlyRecurringRevenue = paid.Sum(p => p.PriceMonthly * p.Count),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao montar o resumo do Admin");
                return Response<AdminOverviewDto>.Fail("Erro ao carregar os números. Tente novamente.");
            }
        }

        // Busca por nome, e-mail, CPF ou id. Sem busca, as contas mais recentes.
        // role: Shopkeeper (padrão), Client, Admin ou "all".
        public async Task<Response<List<AdminUserDto>>> SearchUsersAsync(string? search, string? role)
        {
            try
            {
                var query = _context.User.Where(u => u.DeletionDate == null);

                if (!string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
                {
                    var parsed = string.IsNullOrWhiteSpace(role) ? Role.Shopkeeper
                        : System.Enum.TryParse<Role>(role, ignoreCase: true, out var r) ? r : (Role?)null;
                    if (parsed is null)
                        return Response<List<AdminUserDto>>.Fail("Tipo de conta inválido.");
                    query = query.Where(u => u.Role == parsed);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    // % e _ digitados valem como texto, não como curinga do LIKE.
                    var like = "%" + term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
                    var digits = SlugHelper.OnlyDigits(term);
                    var cpf = digits is { Length: >= 3 } ? digits : null;
                    var id = int.TryParse(term, out var parsedId) ? parsedId : (int?)null;

                    query = query.Where(u =>
                        EF.Functions.ILike(u.Email, like) ||
                        EF.Functions.ILike(u.Name, like) ||
                        (cpf != null && u.Cpf != null && u.Cpf.Contains(cpf)) ||
                        (id != null && u.Id == id));
                }

                var users = await query
                    .OrderByDescending(u => u.CreationDate)
                    .Take(MaxResults)
                    .ToListAsync();

                return Response<List<AdminUserDto>>.Ok(await ToDtosAsync(users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na busca de contas do Admin");
                return Response<List<AdminUserDto>>.Fail("Erro ao buscar as contas. Tente novamente.");
            }
        }

        // Suporte: o lojista não recebeu o e-mail de confirmação (spam, endereço com bloqueio...).
        public async Task<Response<AdminUserDto>> ConfirmEmailAsync(int userId)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null)
                    return Response<AdminUserDto>.Fail("Conta não encontrada.");

                if (user.EmailConfirmedAt is null)
                {
                    user.EmailConfirmedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Admin confirmou manualmente o e-mail do usuário {UserId}", user.Id);
                }

                return Response<AdminUserDto>.Ok((await ToDtosAsync([user]))[0], "E-mail confirmado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao confirmar e-mail pelo Admin");
                return Response<AdminUserDto>.Fail("Erro ao confirmar o e-mail. Tente novamente.");
            }
        }

        private async Task<List<AdminUserDto>> ToDtosAsync(List<User> users)
        {
            var ids = users.Select(u => u.Id).ToList();

            var stores = await _context.Store
                .Where(s => ids.Contains(s.UserId) && s.DeletionDate == null)
                .OrderBy(s => s.CreationDate)
                .Select(s => new { s.UserId, Dto = new AdminStoreDto { Id = s.Id, Name = s.Name, Slug = s.Slug, IsActive = s.IsActive } })
                .ToListAsync();

            var subscriptions = await _context.Subscription
                .Where(s => ids.Contains(s.UserId))
                .ToDictionaryAsync(s => s.UserId);

            var result = new List<AdminUserDto>();
            foreach (var user in users)
            {
                subscriptions.TryGetValue(user.Id, out var sub);
                // Mesma regra do resto do sistema para o plano que vale agora.
                var plan = await _context.GetEffectivePlanAsync(user.Id);

                result.Add(new AdminUserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role.ToString(),
                    EmailConfirmed = user.EmailConfirmedAt is not null,
                    CreationDate = user.CreationDate,
                    PlanCode = plan.Code,
                    PlanName = plan.Name,
                    SubscriptionStatus = sub?.Status,
                    CurrentPeriodEnd = sub?.CurrentPeriodEnd,
                    ManualPlan = sub is { Status: SubscriptionStatus.Active, GatewaySubscriptionId: null, CurrentPeriodEnd: null }
                                 && sub.PlanId != Plan.FreeId,
                    Stores = stores.Where(s => s.UserId == user.Id).Select(s => s.Dto).ToList(),
                });
            }

            return result;
        }
    }
}
