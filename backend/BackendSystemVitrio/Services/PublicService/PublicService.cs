using System.Linq.Expressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.PublicService
{
    public class PublicService : IPublicService
    {
        private const string NotFound = "Loja não encontrada ou indisponível.";

        private readonly AppDbContext _context;

        public PublicService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<PublicStoreDto>> GetStoreAsync(string slug)
        {
            var store = await _context.FindPublicStoreAsync(slug);
            if (store is null)
                return Response<PublicStoreDto>.Fail(NotFound);

            return Response<PublicStoreDto>.Ok(new PublicStoreDto
            {
                Name = store.Name,
                Slug = store.Slug,
                Description = store.Description,
                LogoUrl = store.LogoUrl,
                Phone = store.Phone,
                PrimaryColor = store.PrimaryColor,
                SecondaryColor = store.SecondaryColor,
                TertiaryColor = store.TertiaryColor,
            });
        }

        public async Task<Response<List<PublicCategoryDto>>> GetCategoriesAsync(string slug)
        {
            var store = await _context.FindPublicStoreAsync(slug);
            if (store is null)
                return Response<List<PublicCategoryDto>>.Fail(NotFound);

            // Só categorias ativas que tenham pelo menos um produto visível.
            var categories = await _context.Category
                .Where(c => c.StoreId == store.Id && c.IsActive && c.DeletionDate == null &&
                            c.Products.Any(p => p.IsActive && p.DeletionDate == null))
                .OrderBy(c => c.Name)
                .Select(c => new PublicCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    ParentCategoryId = c.ParentCategoryId,
                })
                .ToListAsync();

            return Response<List<PublicCategoryDto>>.Ok(categories);
        }

        public async Task<Response<List<PublicProductDto>>> GetProductsAsync(string slug, string? categorySlug, string? search)
        {
            var store = await _context.FindPublicStoreAsync(slug);
            if (store is null)
                return Response<List<PublicProductDto>>.Fail(NotFound);

            var query = VisibleProducts(store.Id);

            if (!string.IsNullOrWhiteSpace(categorySlug))
                query = query.Where(p => p.Category != null && p.Category.Slug == categorySlug);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, term) ||
                    (p.Description != null && EF.Functions.ILike(p.Description, term)));
            }

            var products = await query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreationDate)
                .Take(500)
                .Select(ToDtoExpression)
                .ToListAsync();

            return Response<List<PublicProductDto>>.Ok(products);
        }

        public async Task<Response<PublicProductDto>> GetProductAsync(string slug, string productSlug)
        {
            var store = await _context.FindPublicStoreAsync(slug);
            if (store is null)
                return Response<PublicProductDto>.Fail(NotFound);

            var product = await VisibleProducts(store.Id)
                .Where(p => p.Slug == productSlug)
                .Select(ToDtoExpression)
                .FirstOrDefaultAsync();

            return product is null
                ? Response<PublicProductDto>.Fail("Produto não encontrado.")
                : Response<PublicProductDto>.Ok(product);
        }

        // Produto aparece na vitrine se estiver ativo, não excluído e,
        // caso tenha categoria, se ela também estiver ativa.
        private IQueryable<Product> VisibleProducts(int storeId)
            => _context.Product.Where(p =>
                p.StoreId == storeId &&
                p.IsActive &&
                p.DeletionDate == null &&
                (p.Category == null || (p.Category.IsActive && p.Category.DeletionDate == null)));

        private static readonly Expression<Func<Product, PublicProductDto>> ToDtoExpression = p => new PublicProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description,
            Price = p.Price,
            PromotionalPrice = p.PromotionalPrice,
            StockQuantity = p.StockQuantity,
            IsFeatured = p.IsFeatured,
            ColorName = p.ColorName,
            ColorHex = p.ColorHex,
            ColorGroupId = p.ColorGroupId,
            Category = p.Category == null ? null : new ProductCategoryDto
            {
                Id = p.Category.Id,
                Name = p.Category.Name,
                Slug = p.Category.Slug,
            },
            Images = p.Images
                .OrderBy(i => i.Order)
                .Select(i => new ProductImageResponseDto { Id = i.Id, Url = i.Url, Order = i.Order })
                .ToList(),
            Variants = p.Variants
                .OrderBy(v => v.SortOrder)
                .Select(v => new ProductVariantResponseDto { Id = v.Id, Size = v.Size, StockQuantity = v.StockQuantity })
                .ToList(),
        };
    }
}