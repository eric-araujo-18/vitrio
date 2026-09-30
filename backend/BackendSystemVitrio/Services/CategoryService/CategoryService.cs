using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.CategoryService
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;

        public CategoryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<List<CategoryResponseDto>>> GetCategoriesByStoreAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<List<CategoryResponseDto>>.Fail("Loja não encontrada.");

                var categories = await _context.Category
                    .Where(c => c.StoreId == storeId && c.DeletionDate == null)
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryResponseDto
                    {
                        Id = c.Id,
                        StoreId = c.StoreId,
                        Name = c.Name,
                        Slug = c.Slug,
                        ParentCategoryId = c.ParentCategoryId,
                        IsActive = c.IsActive,
                        ProductCount = c.Products.Count(p => p.DeletionDate == null),
                    })
                    .ToListAsync();

                return Response<List<CategoryResponseDto>>.Ok(categories, "Categorias recuperadas com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<List<CategoryResponseDto>>.Fail($"Erro ao recuperar categorias: {ex.Message}");
            }
        }

        public async Task<Response<CategoryResponseDto>> CreateCategoryAsync(int userId, CreateCategoryDto dto)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(dto.StoreId, userId);
                if (store is null)
                    return Response<CategoryResponseDto>.Fail("Loja não encontrada.");

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Response<CategoryResponseDto>.Fail("O nome da categoria é obrigatório.");

                if (dto.ParentCategoryId.HasValue && !await ParentExistsAsync(dto.ParentCategoryId.Value, dto.StoreId))
                    return Response<CategoryResponseDto>.Fail("Categoria pai não encontrada nesta loja.");

                // Antes o Slug ia direto do DTO (podia ser null) e estourava no banco,
                // já que a coluna é obrigatória.
                var slug = SlugHelper.Slugify(string.IsNullOrWhiteSpace(dto.Slug) ? dto.Name : dto.Slug, "categoria");

                if (await SlugInUseAsync(dto.StoreId, slug))
                    return Response<CategoryResponseDto>.Fail("Já existe uma categoria com esse nome/slug nesta loja.");

                var category = new Category
                {
                    StoreId = dto.StoreId,
                    Name = dto.Name.Trim(),
                    Slug = slug,
                    ParentCategoryId = dto.ParentCategoryId,
                };

                _context.Category.Add(category);
                await _context.SaveChangesAsync();

                return Response<CategoryResponseDto>.Ok(ToDto(category, 0), "Categoria criada com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<CategoryResponseDto>.Fail($"Erro ao criar categoria: {ex.Message}");
            }
        }

        public async Task<Response<CategoryResponseDto>> UpdateCategoryAsync(int categoryId, int userId, UpdateCategoryDto dto)
        {
            try
            {
                var category = await FindOwnedCategoryAsync(categoryId, userId);
                if (category is null)
                    return Response<CategoryResponseDto>.Fail("Categoria não encontrada.");

                if (dto.Name is not null)
                {
                    if (string.IsNullOrWhiteSpace(dto.Name))
                        return Response<CategoryResponseDto>.Fail("O nome da categoria não pode ficar vazio.");
                    category.Name = dto.Name.Trim();
                }

                if (dto.Slug is not null)
                {
                    var slug = SlugHelper.Slugify(dto.Slug, "categoria");
                    if (slug != category.Slug && await SlugInUseAsync(category.StoreId, slug, category.Id))
                        return Response<CategoryResponseDto>.Fail("Já existe uma categoria com esse slug nesta loja.");
                    category.Slug = slug;
                }

                if (dto.RemoveParent)
                {
                    category.ParentCategoryId = null;
                }
                else if (dto.ParentCategoryId.HasValue)
                {
                    if (dto.ParentCategoryId.Value == category.Id)
                        return Response<CategoryResponseDto>.Fail("Uma categoria não pode ser pai dela mesma.");

                    if (!await ParentExistsAsync(dto.ParentCategoryId.Value, category.StoreId))
                        return Response<CategoryResponseDto>.Fail("Categoria pai não encontrada nesta loja.");

                    if (await CreatesCycleAsync(category.Id, dto.ParentCategoryId.Value))
                        return Response<CategoryResponseDto>.Fail("Essa escolha criaria um ciclo entre categorias.");

                    category.ParentCategoryId = dto.ParentCategoryId.Value;
                }

                if (dto.IsActive.HasValue)
                    category.IsActive = dto.IsActive.Value;

                await _context.SaveChangesAsync();

                var productCount = await _context.Product.CountAsync(p => p.CategoryId == category.Id && p.DeletionDate == null);
                return Response<CategoryResponseDto>.Ok(ToDto(category, productCount), "Categoria atualizada com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<CategoryResponseDto>.Fail($"Erro ao atualizar categoria: {ex.Message}");
            }
        }

        public async Task<Response<string>> DeleteCategoryAsync(int categoryId, int userId)
        {
            try
            {
                var category = await FindOwnedCategoryAsync(categoryId, userId);
                if (category is null)
                    return Response<string>.Fail("Categoria não encontrada.");

                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Produtos da categoria ficam "sem categoria" (não são apagados).
                await _context.Product
                    .Where(p => p.CategoryId == category.Id)
                    .ExecuteUpdateAsync(set => set.SetProperty(p => p.CategoryId, (int?)null));

                // Subcategorias sobem um nível (passam a apontar pro pai da excluída).
                await _context.Category
                    .Where(c => c.ParentCategoryId == category.Id)
                    .ExecuteUpdateAsync(set => set.SetProperty(c => c.ParentCategoryId, category.ParentCategoryId));

                category.DeletionDate = DateTime.UtcNow;
                category.IsActive = false;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Response<string>.Ok("", "Categoria excluída com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<string>.Fail($"Erro ao excluir categoria: {ex.Message}");
            }
        }

        // ===== Helpers =====

        private Task<Category?> FindOwnedCategoryAsync(int categoryId, int userId)
            => _context.Category.FirstOrDefaultAsync(c =>
                c.Id == categoryId &&
                c.DeletionDate == null &&
                c.Store!.UserId == userId &&
                c.Store.DeletionDate == null);

        private Task<bool> ParentExistsAsync(int parentId, int storeId)
            => _context.Category.AnyAsync(c => c.Id == parentId && c.StoreId == storeId && c.DeletionDate == null);

        private Task<bool> SlugInUseAsync(int storeId, string slug, int? ignoreId = null)
            => _context.Category.AnyAsync(c =>
                c.StoreId == storeId && c.Slug == slug && c.DeletionDate == null && c.Id != ignoreId);

        // Sobe a árvore a partir do novo pai: se encontrar a própria categoria, é ciclo.
        private async Task<bool> CreatesCycleAsync(int categoryId, int newParentId)
        {
            int? current = newParentId;
            var guard = 0;

            while (current.HasValue && guard++ < 50)
            {
                if (current.Value == categoryId) return true;
                current = await _context.Category
                    .Where(c => c.Id == current.Value)
                    .Select(c => c.ParentCategoryId)
                    .FirstOrDefaultAsync();
            }

            return false;
        }

        private static CategoryResponseDto ToDto(Category c, int productCount) => new()
        {
            Id = c.Id,
            StoreId = c.StoreId,
            Name = c.Name,
            Slug = c.Slug,
            ParentCategoryId = c.ParentCategoryId,
            IsActive = c.IsActive,
            ProductCount = productCount,
        };
    }
}
