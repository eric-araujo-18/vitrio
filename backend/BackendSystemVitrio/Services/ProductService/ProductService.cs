using System.Linq.Expressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.ProductService
{
    public class ProductService : IProductService
    {
        private const int MaxImagesPerProduct = 8;

        private readonly AppDbContext _context;

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<List<ProductResponseDto>>> GetProductsByStoreAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<List<ProductResponseDto>>.Fail("Loja não encontrada.");

                var products = await _context.Product
                    .Where(p => p.StoreId == storeId && p.DeletionDate == null)
                    .OrderByDescending(p => p.CreationDate)
                    .Select(ToDtoExpression)
                    .ToListAsync();

                return Response<List<ProductResponseDto>>.Ok(products, "Produtos recuperados com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<List<ProductResponseDto>>.Fail($"Erro ao recuperar produtos: {ex.Message}");
            }
        }

        public async Task<Response<ProductResponseDto>> GetProductByIdAsync(int productId, int userId)
        {
            try
            {
                var product = await OwnedProducts(userId)
                    .Where(p => p.Id == productId)
                    .Select(ToDtoExpression)
                    .FirstOrDefaultAsync();

                return product is null
                    ? Response<ProductResponseDto>.Fail("Produto não encontrado.")
                    : Response<ProductResponseDto>.Ok(product);
            }
            catch (Exception ex)
            {
                return Response<ProductResponseDto>.Fail($"Erro ao recuperar produto: {ex.Message}");
            }
        }

        public async Task<Response<ProductResponseDto>> CreateProductAsync(int userId, CreateProductDto dto)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(dto.StoreId, userId);
                if (store is null)
                    return Response<ProductResponseDto>.Fail("Loja não encontrada.");

                var validationError = ValidateFields(dto.Name, dto.Price, dto.PromotionalPrice, dto.StockQuantity, dto.Images);
                if (validationError is not null)
                    return Response<ProductResponseDto>.Fail(validationError);

                var slug = SlugHelper.Slugify(string.IsNullOrWhiteSpace(dto.Slug) ? dto.Name : dto.Slug, "produto");

                if (await SlugInUseAsync(dto.StoreId, slug))
                    return Response<ProductResponseDto>.Fail("Já existe um produto com esse slug nesta loja.");

                if (dto.CategoryId.HasValue && !await CategoryExistsAsync(dto.CategoryId.Value, dto.StoreId))
                    return Response<ProductResponseDto>.Fail("Categoria não encontrada nesta loja.");

                var product = new Product
                {
                    StoreId = dto.StoreId,
                    CategoryId = dto.CategoryId,
                    Name = dto.Name.Trim(),
                    Slug = slug,
                    Description = EmptyToNull(dto.Description),
                    Sku = EmptyToNull(dto.Sku),
                    Price = dto.Price,
                    PromotionalPrice = dto.PromotionalPrice,
                    StockQuantity = dto.StockQuantity,
                    IsActive = dto.IsActive,
                    IsFeatured = dto.IsFeatured,
                    Images = BuildImages(dto.Images),
                };

                _context.Product.Add(product);
                await _context.SaveChangesAsync();

                return await ReloadAsDtoAsync(product.Id, "Produto criado com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<ProductResponseDto>.Fail($"Erro ao criar produto: {ex.Message}");
            }
        }

        public async Task<Response<ProductResponseDto>> UpdateProductAsync(int productId, int userId, UpdateProductDto dto)
        {
            try
            {
                var product = await OwnedProducts(userId)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product is null)
                    return Response<ProductResponseDto>.Fail("Produto não encontrado.");

                var validationError = ValidateFields(dto.Name, dto.Price, dto.PromotionalPrice, dto.StockQuantity, dto.Images);
                if (validationError is not null)
                    return Response<ProductResponseDto>.Fail(validationError);

                var slug = SlugHelper.Slugify(string.IsNullOrWhiteSpace(dto.Slug) ? dto.Name : dto.Slug, "produto");
                if (slug != product.Slug && await SlugInUseAsync(product.StoreId, slug, product.Id))
                    return Response<ProductResponseDto>.Fail("Já existe um produto com esse slug nesta loja.");

                if (dto.CategoryId.HasValue && !await CategoryExistsAsync(dto.CategoryId.Value, product.StoreId))
                    return Response<ProductResponseDto>.Fail("Categoria não encontrada nesta loja.");

                product.CategoryId = dto.CategoryId;
                product.Name = dto.Name.Trim();
                product.Slug = slug;
                product.Description = EmptyToNull(dto.Description);
                product.Sku = EmptyToNull(dto.Sku);
                product.Price = dto.Price;
                product.PromotionalPrice = dto.PromotionalPrice;
                product.StockQuantity = dto.StockQuantity;
                product.IsActive = dto.IsActive;
                product.IsFeatured = dto.IsFeatured;
                product.UpdatedDate = DateTime.UtcNow;

                if (dto.Images is not null)
                {
                    _context.ProductImage.RemoveRange(product.Images);
                    product.Images = BuildImages(dto.Images);
                }

                await _context.SaveChangesAsync();

                return await ReloadAsDtoAsync(product.Id, "Produto atualizado com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<ProductResponseDto>.Fail($"Erro ao atualizar produto: {ex.Message}");
            }
        }

        public async Task<Response<string>> DeleteProductAsync(int productId, int userId)
        {
            try
            {
                var product = await OwnedProducts(userId).FirstOrDefaultAsync(p => p.Id == productId);
                if (product is null)
                    return Response<string>.Fail("Produto não encontrado.");

                // Soft delete: pedidos antigos continuam apontando pro produto.
                product.DeletionDate = DateTime.UtcNow;
                product.IsActive = false;
                await _context.SaveChangesAsync();

                return Response<string>.Ok("", "Produto excluído com sucesso.");
            }
            catch (Exception ex)
            {
                return Response<string>.Fail($"Erro ao excluir produto: {ex.Message}");
            }
        }

        // ===== Helpers =====

        // Produtos não excluídos de lojas (não excluídas) do usuário.
        private IQueryable<Product> OwnedProducts(int userId)
            => _context.Product.Where(p =>
                p.DeletionDate == null &&
                p.Store!.UserId == userId &&
                p.Store.DeletionDate == null);

        private Task<bool> SlugInUseAsync(int storeId, string slug, int? ignoreId = null)
            => _context.Product.AnyAsync(p =>
                p.StoreId == storeId && p.Slug == slug && p.DeletionDate == null && p.Id != ignoreId);

        private Task<bool> CategoryExistsAsync(int categoryId, int storeId)
            => _context.Category.AnyAsync(c => c.Id == categoryId && c.StoreId == storeId && c.DeletionDate == null);

        private static string? ValidateFields(
            string? name, decimal price, decimal? promotionalPrice, int stock, List<CreateProductImageDto>? images)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "O nome do produto é obrigatório.";

            if (price <= 0)
                return "Informe um preço válido.";

            if (promotionalPrice.HasValue && (promotionalPrice.Value <= 0 || promotionalPrice.Value >= price))
                return "O preço promocional precisa ser maior que zero e menor que o preço normal.";

            if (stock < 0)
                return "A quantidade em estoque não pode ser negativa.";

            if (images is not null)
            {
                if (images.Count > MaxImagesPerProduct)
                    return $"Cada produto pode ter no máximo {MaxImagesPerProduct} imagens.";

                if (images.Any(i => !Uri.TryCreate(i.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                    return "Uma das imagens tem uma URL inválida.";
            }

            return null;
        }

        // Normaliza a ordem (0..n-1) conforme a posição enviada pelo frontend.
        // ProductId não é setado: o EF preenche ao salvar pela navigation.
        private static List<ProductImage> BuildImages(List<CreateProductImageDto>? images)
            => (images ?? new())
                .OrderBy(i => i.Order)
                .Select((img, index) => new ProductImage { ProductId = 0, Url = img.Url, Order = index })
                .ToList();

        private static string? EmptyToNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<Response<ProductResponseDto>> ReloadAsDtoAsync(int productId, string message)
        {
            var dto = await _context.Product
                .Where(p => p.Id == productId)
                .Select(ToDtoExpression)
                .FirstAsync();

            return Response<ProductResponseDto>.Ok(dto, message);
        }

        // Expressão reaproveitável: o EF traduz direto pra SQL (sem carregar a entidade inteira).
        private static readonly Expression<Func<Product, ProductResponseDto>> ToDtoExpression = p => new ProductResponseDto
        {
            Id = p.Id,
            StoreId = p.StoreId,
            CategoryId = p.CategoryId,
            Category = p.Category == null ? null : new ProductCategoryDto
            {
                Id = p.Category.Id,
                Name = p.Category.Name,
                Slug = p.Category.Slug,
            },
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price,
            PromotionalPrice = p.PromotionalPrice,
            StockQuantity = p.StockQuantity,
            IsActive = p.IsActive,
            IsFeatured = p.IsFeatured,
            CreationDate = p.CreationDate,
            UpdatedDate = p.UpdatedDate,
            Images = p.Images
                .OrderBy(img => img.Order)
                .Select(img => new ProductImageResponseDto
                {
                    Id = img.Id,
                    Url = img.Url,
                    Order = img.Order,
                })
                .ToList(),
        };
    }
}
