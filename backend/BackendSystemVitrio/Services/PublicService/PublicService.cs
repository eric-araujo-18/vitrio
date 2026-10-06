using System.Linq.Expressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.OrderPaymentService;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.PublicService
{
    public class PublicService : IPublicService
    {
        private const string NotFound = "Loja não encontrada ou indisponível.";
        // Produtos por página na vitrine (PRODUCTS_PAGE_SIZE no frontend).
        public const int ProductsPageSize = 200;

        private readonly AppDbContext _context;
        private readonly IOrderPaymentService _payments;

        public PublicService(AppDbContext context, IOrderPaymentService payments)
        {
            _context = context;
            _payments = payments;
        }

        public async Task<Response<PublicStoreDto>> GetStoreAsync(string slug)
        {
            var found = await _context.FindPublicStoreAsync(slug);
            if (found is null)
                return Response<PublicStoreDto>.Fail(NotFound);

            var store = found.Store;

            return Response<PublicStoreDto>.Ok(new PublicStoreDto
            {
                Name = store.Name,
                Slug = store.Slug,
                Description = store.Description,
                LogoUrl = store.LogoUrl,
                Phone = store.Phone,
                OnlinePayment = await _payments.IsAvailableAsync(store.Id, found.Plan),
                PrimaryColor = store.PrimaryColor,
                SecondaryColor = store.SecondaryColor,
                TertiaryColor = store.TertiaryColor,
            });
        }

        public async Task<Response<List<PublicCategoryDto>>> GetCategoriesAsync(string slug)
        {
            var found = await _context.FindPublicStoreAsync(slug);
            if (found is null)
                return Response<List<PublicCategoryDto>>.Fail(NotFound);

            // Só categorias ativas que tenham pelo menos um produto visível (o que já deixa de fora
            // as que estão dentro de uma categoria desativada).
            var visibleIds = (await _context.VisibleProductsAsync(found)).Select(p => p.Id);
            var categories = await _context.Category
                .Where(c => c.StoreId == found.Store.Id && c.IsActive && c.DeletionDate == null &&
                            c.Products.Any(p => visibleIds.Contains(p.Id)))
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

        public async Task<Response<List<PublicProductDto>>> GetProductsAsync(string slug, string? categorySlug, string? search, int page = 1)
        {
            var found = await _context.FindPublicStoreAsync(slug);
            if (found is null)
                return Response<List<PublicProductDto>>.Fail(NotFound);

            var query = await _context.VisibleProductsAsync(found);

            if (!string.IsNullOrWhiteSpace(categorySlug))
                query = query.Where(p => p.Category != null && p.Category.Slug == categorySlug);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, term) ||
                    (p.Description != null && EF.Functions.ILike(p.Description, term)));
            }

            // Em páginas: a vitrine busca a próxima enquanto a anterior vier cheia. O desempate
            // pelo id deixa a ordem fixa, então uma página não repete produto da outra.
            var products = await query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreationDate)
                .ThenByDescending(p => p.Id)
                .Skip((Math.Max(page, 1) - 1) * ProductsPageSize)
                .Take(ProductsPageSize)
                .Select(ToDtoExpression)
                .ToListAsync();

            return Response<List<PublicProductDto>>.Ok(products);
        }

        public async Task<Response<PublicProductDto>> GetProductAsync(string slug, string productSlug)
        {
            var found = await _context.FindPublicStoreAsync(slug);
            if (found is null)
                return Response<PublicProductDto>.Fail(NotFound);

            var product = await (await _context.VisibleProductsAsync(found))
                .Where(p => p.Slug == productSlug)
                .Select(ToDtoExpression)
                .FirstOrDefaultAsync();

            return product is null
                ? Response<PublicProductDto>.Fail("Produto não encontrado.")
                : Response<PublicProductDto>.Ok(product);
        }

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