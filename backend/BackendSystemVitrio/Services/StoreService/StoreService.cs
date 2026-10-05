using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.StoreService
{
    public class StoreService : IStoreService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StoreService> _logger;

        public StoreService(AppDbContext context, ILogger<StoreService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Response<List<StoreDto>>> GetStoresByUserAsync(int userId)
        {
            try
            {
                var stores = await _context.Store
                    .Where(s => s.UserId == userId && s.DeletionDate == null)
                    .OrderByDescending(s => s.CreationDate)
                    .ToListAsync();

                var limits = await GetPlanLimitsAsync(userId);

                return Response<List<StoreDto>>.Ok(
                    stores.Select(s => ToDto(s, limits)).ToList(),
                    "Lojas do usuário recuperadas com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar lojas do usuário");
                return Response<List<StoreDto>>.Fail("Erro ao recuperar lojas do usuário. Tente novamente.");
            }
        }

        public async Task<Response<StoreDto>> GetByIdAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StoreDto>.Fail("Loja não encontrada.");

                return Response<StoreDto>.Ok(ToDto(store, await GetPlanLimitsAsync(userId)), "Loja recuperada com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar loja");
                return Response<StoreDto>.Fail("Erro ao recuperar loja. Tente novamente.");
            }
        }

        public async Task<Response<StoreDto>> CreateAsync(int userId, CreateStoreDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Response<StoreDto>.Fail("Informe o nome da loja.");

                var name = dto.Name.Trim();

                // Limite de lojas do plano (lojas excluídas não contam).
                var plan = await _context.GetEffectivePlanAsync(userId);
                var storeCount = await _context.Store.CountAsync(s => s.UserId == userId && s.DeletionDate == null);
                if (storeCount >= plan.MaxStores)
                    return Response<StoreDto>.Fail(
                        $"Seu plano {plan.Name} permite até {plan.MaxStores} {(plan.MaxStores == 1 ? "loja" : "lojas")}. " +
                        "Veja os planos em Assinatura para criar mais.");

                var normalizedCnpj = SlugHelper.OnlyDigits(dto.Cnpj);
                if (normalizedCnpj is not null)
                {
                    if (normalizedCnpj.Length != 14)
                        return Response<StoreDto>.Fail("CNPJ inválido.");

                    if (await _context.Store.AnyAsync(s => s.Cnpj == normalizedCnpj && s.DeletionDate == null))
                        return Response<StoreDto>.Fail("CNPJ já cadastrado.");
                }

                if (await NameInUseAsync(userId, name, exceptStoreId: null))
                    return Response<StoreDto>.Fail("Você já tem uma loja com esse nome.");

                var colorError = ValidateColors(dto.PrimaryColor, dto.SecondaryColor, dto.TertiaryColor);
                if (colorError is not null)
                    return Response<StoreDto>.Fail(colorError);

                var store = new Store
                {
                    Name = name,
                    Slug = await GenerateUniqueSlugAsync(name),
                    Cnpj = normalizedCnpj,
                    Description = EmptyToNull(dto.Description),
                    LogoUrl = EmptyToNull(dto.LogoUrl),
                    Phone = EmptyToNull(dto.Phone),
                    PrimaryColor = dto.PrimaryColor ?? "#2563eb",
                    SecondaryColor = dto.SecondaryColor ?? "#1d4ed8",
                    TertiaryColor = dto.TertiaryColor ?? "#111827",
                    UserId = userId
                };

                _context.Store.Add(store);
                await _context.SaveChangesAsync();

                return Response<StoreDto>.Ok(ToDto(store, await GetPlanLimitsAsync(userId)), "Loja criada com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar loja");
                return Response<StoreDto>.Fail("Erro ao criar loja. Tente novamente.");
            }
        }

        public async Task<Response<StoreDto>> UpdateAsync(int storeId, int userId, UpdateStoreDto dto)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StoreDto>.Fail("Loja não encontrada.");

                if (dto.Name is not null)
                {
                    var name = dto.Name.Trim();
                    if (name.Length == 0)
                        return Response<StoreDto>.Fail("O nome da loja não pode ficar vazio.");

                    if (name != store.Name && await NameInUseAsync(userId, name, exceptStoreId: store.Id))
                        return Response<StoreDto>.Fail("Você já tem uma loja com esse nome.");

                    // O slug NÃO muda ao renomear: links já compartilhados continuam funcionando.
                    store.Name = name;
                }

                if (dto.Cnpj is not null)
                {
                    var cnpj = SlugHelper.OnlyDigits(dto.Cnpj);
                    if (cnpj is not null)
                    {
                        if (cnpj.Length != 14)
                            return Response<StoreDto>.Fail("CNPJ inválido.");

                        if (await _context.Store.AnyAsync(s => s.Cnpj == cnpj && s.Id != store.Id && s.DeletionDate == null))
                            return Response<StoreDto>.Fail("CNPJ já cadastrado.");
                    }
                    store.Cnpj = cnpj;
                }

                var colorError = ValidateColors(dto.PrimaryColor, dto.SecondaryColor, dto.TertiaryColor);
                if (colorError is not null)
                    return Response<StoreDto>.Fail(colorError);

                if (dto.Description is not null) store.Description = EmptyToNull(dto.Description);
                if (dto.LogoUrl is not null) store.LogoUrl = EmptyToNull(dto.LogoUrl);
                if (dto.Phone is not null) store.Phone = EmptyToNull(dto.Phone);
                if (dto.PrimaryColor is not null) store.PrimaryColor = dto.PrimaryColor;
                if (dto.SecondaryColor is not null) store.SecondaryColor = dto.SecondaryColor;
                if (dto.TertiaryColor is not null) store.TertiaryColor = dto.TertiaryColor;
                if (dto.IsActive == true && !store.IsActive)
                {
                    // Reativar conta no limite de lojas no ar do plano.
                    var plan = await _context.GetEffectivePlanAsync(userId);
                    var activeCount = await _context.Store.CountAsync(s =>
                        s.UserId == userId && s.IsActive && s.DeletionDate == null);
                    if (activeCount >= plan.MaxStores)
                        return Response<StoreDto>.Fail(
                            $"Seu plano {plan.Name} permite {plan.MaxStores} {(plan.MaxStores == 1 ? "loja" : "lojas")} no ar. " +
                            "Pause outra loja antes de reativar esta, ou veja os planos em Assinatura.");
                }

                if (dto.IsActive.HasValue) store.IsActive = dto.IsActive.Value;

                await _context.SaveChangesAsync();

                return Response<StoreDto>.Ok(ToDto(store, await GetPlanLimitsAsync(userId)), "Loja atualizada com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar loja");
                return Response<StoreDto>.Fail("Erro ao atualizar loja. Tente novamente.");
            }
        }

        public async Task<Response<string>> DeleteAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<string>.Fail("Loja não encontrada.");

                // Soft delete: some do painel e da vitrine, mas os dados (e pedidos) ficam.
                store.DeletionDate = DateTime.UtcNow;
                store.IsActive = false;

                await _context.SaveChangesAsync();

                return Response<string>.Ok("", "Loja excluída com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir loja");
                return Response<string>.Fail("Erro ao excluir loja. Tente novamente.");
            }
        }

        // É como o lojista escolhe qual loja fica no ar quando o plano permite menos lojas do
        // que ele tem: esta loja vai para o ar e as outras ativas que passarem do limite são
        // pausadas (ficam as mais antigas, na mesma ordem de StoresWithinPlan).
        public async Task<Response<List<StoreDto>>> GoOnlineAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<List<StoreDto>>.Fail("Loja não encontrada.");

                var plan = await _context.GetEffectivePlanAsync(userId);
                var others = await _context.Store
                    .Where(s => s.UserId == userId && s.Id != storeId && s.IsActive && s.DeletionDate == null)
                    .OrderBy(s => s.CreationDate)
                    .ThenBy(s => s.Id)
                    .ToListAsync();

                foreach (var other in others.Skip(plan.MaxStores - 1))
                    other.IsActive = false;

                store.IsActive = true;
                await _context.SaveChangesAsync();

                return await GetStoresByUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao colocar a loja no ar");
                return Response<List<StoreDto>>.Fail("Erro ao colocar a loja no ar. Tente novamente.");
            }
        }

        public async Task<Response<StoreDashboardDto>> GetDashboardAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StoreDashboardDto>.Fail("Loja não encontrada.");

                var products = _context.Product.Where(p => p.StoreId == storeId && p.DeletionDate == null);
                var since = DateTime.UtcNow.AddDays(-30);
                var recentValidOrders = _context.Order.Where(o =>
                    o.StoreId == storeId && o.CreationDate >= since && o.Status != OrderStatus.Canceled);

                var dashboard = new StoreDashboardDto
                {
                    TotalProducts = await products.CountAsync(),
                    ActiveProducts = await products.CountAsync(p => p.IsActive),
                    OutOfStockProducts = await products.CountAsync(p => p.StockQuantity <= 0),
                    TotalCategories = await _context.Category.CountAsync(c => c.StoreId == storeId && c.DeletionDate == null),
                    PendingOrders = await _context.Order.CountAsync(o => o.StoreId == storeId && o.Status == OrderStatus.Pending),
                    OrdersLast30Days = await recentValidOrders.CountAsync(),
                    RevenueLast30Days = await recentValidOrders.SumAsync(o => (decimal?)o.Total) ?? 0,
                    RecentOrders = await _context.Order
                        .Where(o => o.StoreId == storeId)
                        .OrderByDescending(o => o.CreationDate)
                        .Take(5)
                        .Select(o => new OrderSummaryDto
                        {
                            Id = o.Id,
                            Code = o.Code,
                            CustomerName = o.CustomerName,
                            Status = o.Status,
                            Total = o.Total,
                            ItemCount = o.Items.Sum(i => i.Quantity),
                            CreationDate = o.CreationDate
                        })
                        .ToListAsync()
                };

                return Response<StoreDashboardDto>.Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar o painel");
                return Response<StoreDashboardDto>.Fail("Erro ao carregar o painel. Tente novamente.");
            }
        }

        // ===== Helpers =====

        // O nome só se repete entre lojas do mesmo lojista que não foram excluídas
        // (mesma regra do índice). Ignora maiúsculas/minúsculas: "Loja" e "loja" contam como iguais.
        private Task<bool> NameInUseAsync(int userId, string name, int? exceptStoreId)
        {
            var lower = name.ToLower();
            return _context.Store.AnyAsync(s =>
                s.UserId == userId && s.DeletionDate == null && s.Id != exceptStoreId && s.Name.ToLower() == lower);
        }

        private async Task<string> GenerateUniqueSlugAsync(string name)
        {
            var baseSlug = SlugHelper.Slugify(name, "loja");
            var slug = baseSlug;
            var counter = 2;

            // Considera também lojas excluídas: evita que um link antigo
            // passe a apontar pra uma loja nova de outra pessoa.
            while (await _context.Store.AnyAsync(s => s.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private static string? ValidateColors(params string?[] colors)
        {
            foreach (var color in colors)
            {
                if (color is not null && !SlugHelper.IsHexColor(color))
                    return "Cor inválida. Use o formato #RRGGBB.";
            }
            return null;
        }

        private static string? EmptyToNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private record PlanLimits(HashSet<int> StoresWithinPlan, bool StoreLimitReached, int? MaxProductsPerStore);

        private async Task<PlanLimits> GetPlanLimitsAsync(int userId)
        {
            var plan = await _context.GetEffectivePlanAsync(userId);
            var ids = await _context.StoresWithinPlan(userId, plan).Select(s => s.Id).ToListAsync();
            return new PlanLimits(ids.ToHashSet(), ids.Count >= plan.MaxStores, plan.MaxProductsPerStore);
        }

        private static StoreDto ToDto(Store s, PlanLimits limits) => new()
        {
            BlockedByPlan = s.IsActive && !limits.StoresWithinPlan.Contains(s.Id),
            StoreLimitReached = limits.StoreLimitReached,
            MaxProductsPerStore = limits.MaxProductsPerStore,
            Id = s.Id,
            Name = s.Name,
            Slug = s.Slug,
            Cnpj = s.Cnpj,
            Description = s.Description,
            LogoUrl = s.LogoUrl,
            Phone = s.Phone,
            PrimaryColor = s.PrimaryColor,
            SecondaryColor = s.SecondaryColor,
            TertiaryColor = s.TertiaryColor,
            IsActive = s.IsActive,
            CreationDate = s.CreationDate
        };
    }
}