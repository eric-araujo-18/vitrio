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

        public StoreService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<List<StoreDto>>> GetStoresByUserAsync(int userId)
        {
            try
            {
                var stores = await _context.Store
                    .Where(s => s.UserId == userId && s.DeletionDate == null)
                    .OrderByDescending(s => s.CreationDate)
                    .ToListAsync();

                return Response<List<StoreDto>>.Ok(
                    stores.Select(ToDto).ToList(),
                    "Lojas do usuário recuperadas com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<List<StoreDto>>.Fail($"Erro ao recuperar lojas do usuário: {ex.Message}");
            }
        }

        public async Task<Response<StoreDto>> GetByIdAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<StoreDto>.Fail("Loja não encontrada.");

                return Response<StoreDto>.Ok(ToDto(store), "Loja recuperada com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<StoreDto>.Fail($"Erro ao recuperar loja: {ex.Message}");
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

                    if (await _context.Store.AnyAsync(s => s.Cnpj == normalizedCnpj))
                        return Response<StoreDto>.Fail("CNPJ já cadastrado.");
                }

                if (await _context.Store.AnyAsync(s => s.Name == name))
                    return Response<StoreDto>.Fail("Nome da loja já cadastrado.");

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

                return Response<StoreDto>.Ok(ToDto(store), "Loja criada com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<StoreDto>.Fail($"Erro ao criar loja: {ex.Message}");
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

                    if (name != store.Name &&
                        await _context.Store.AnyAsync(s => s.Name == name && s.Id != store.Id))
                        return Response<StoreDto>.Fail("Nome da loja já cadastrado.");

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

                        if (await _context.Store.AnyAsync(s => s.Cnpj == cnpj && s.Id != store.Id))
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
                if (dto.IsActive.HasValue) store.IsActive = dto.IsActive.Value;

                await _context.SaveChangesAsync();

                return Response<StoreDto>.Ok(ToDto(store), "Loja atualizada com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<StoreDto>.Fail($"Erro ao atualizar loja: {ex.Message}");
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
                return Response<string>.Fail($"Erro ao excluir loja: {ex.Message}");
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
                return Response<StoreDashboardDto>.Fail($"Erro ao carregar o painel: {ex.Message}");
            }
        }

        // ===== Helpers =====

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

        private static StoreDto ToDto(Store s) => new()
        {
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