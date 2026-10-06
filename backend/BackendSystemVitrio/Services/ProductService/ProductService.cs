using System.Linq.Expressions;
using System.Text.RegularExpressions;
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
        private const int MaxVariantsPerProduct = 30;
        private const int MaxSizeLength = 20;
        private const int MaxColorNameLength = 40;
        private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        private readonly AppDbContext _context;
        private readonly ILogger<ProductService> _logger;

        public ProductService(AppDbContext context, ILogger<ProductService> logger)
        {
            _context = context;
            _logger = logger;
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

                await MarkHiddenByPlanAsync(storeId, userId, products);

                return Response<List<ProductResponseDto>>.Ok(products, "Produtos recuperados com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar produtos");
                return Response<List<ProductResponseDto>>.Fail("Erro ao recuperar produtos. Tente novamente.");
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

                if (product is null)
                    return Response<ProductResponseDto>.Fail("Produto não encontrado.");

                await MarkHiddenByPlanAsync(product.StoreId, userId, [product]);
                return Response<ProductResponseDto>.Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar produto");
                return Response<ProductResponseDto>.Fail("Erro ao recuperar produto. Tente novamente.");
            }
        }

        public async Task<Response<ProductResponseDto>> CreateProductAsync(int userId, CreateProductDto dto)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(dto.StoreId, userId);
                if (store is null)
                    return Response<ProductResponseDto>.Fail("Loja não encontrada.");

                // Limite de produtos por loja do plano (null = ilimitado; excluídos não contam).
                var plan = await _context.GetEffectivePlanAsync(userId);
                if (plan.MaxProductsPerStore.HasValue)
                {
                    var productCount = await _context.Product
                        .CountAsync(p => p.StoreId == store.Id && p.DeletionDate == null);
                    if (productCount >= plan.MaxProductsPerStore.Value)
                        return Response<ProductResponseDto>.Fail(
                            $"Seu plano {plan.Name} permite até {plan.MaxProductsPerStore.Value} produtos por loja. " +
                            "Veja os planos em Assinatura para cadastrar mais.");
                }

                var validationError = ValidateFields(dto.Name, dto.Price, dto.PromotionalPrice, dto.StockQuantity, dto.Images)
                                      ?? ValidateVariants(dto.Variants)
                                      ?? ValidateColor(dto.ColorName, dto.ColorHex, dto.ColorLinkedProductId);
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
                    Variants = BuildVariants(dto.Variants),
                    ColorName = EmptyToNull(dto.ColorName),
                    ColorHex = NormalizeHex(dto.ColorHex),
                };

                var colorError = await ApplyColorGroupAsync(product, dto.ColorLinkedProductId);
                if (colorError is not null)
                    return Response<ProductResponseDto>.Fail(colorError);

                // Com tamanhos, o estoque do produto é a soma deles.
                if (product.Variants.Count > 0)
                    product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);

                _context.Product.Add(product);
                await _context.SaveChangesAsync();

                return await ReloadAsDtoAsync(product.Id, userId, "Produto criado com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar produto");
                return Response<ProductResponseDto>.Fail("Erro ao criar produto. Tente novamente.");
            }
        }

        public async Task<Response<ProductResponseDto>> UpdateProductAsync(int productId, int userId, UpdateProductDto dto)
        {
            try
            {
                if (!await OwnedProducts(userId).AnyAsync(p => p.Id == productId))
                    return Response<ProductResponseDto>.Fail("Produto não encontrado.");

                var validationError = ValidateFields(dto.Name, dto.Price, dto.PromotionalPrice, dto.StockQuantity, dto.Images)
                                      ?? ValidateVariants(dto.Variants)
                                      ?? ValidateColor(dto.ColorName, dto.ColorHex, dto.ColorLinkedProductId);
                if (validationError is not null)
                    return Response<ProductResponseDto>.Fail(validationError);

                // Trava o estoque deste produto até salvar: um pedido feito agora espera, e o
                // estoque lido abaixo é o atual (com as vendas feitas enquanto o lojista editava).
                await using var transaction = await _context.Database.BeginTransactionAsync();
                await LockStockAsync(productId);

                var product = await _context.Product
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .FirstAsync(p => p.Id == productId);

                var slug = SlugHelper.Slugify(string.IsNullOrWhiteSpace(dto.Slug) ? dto.Name : dto.Slug, "produto");
                if (slug != product.Slug && await SlugInUseAsync(product.StoreId, slug, product.Id))
                    return Response<ProductResponseDto>.Fail("Já existe um produto com esse slug nesta loja.");

                if (dto.CategoryId.HasValue && !await CategoryExistsAsync(dto.CategoryId.Value, product.StoreId))
                    return Response<ProductResponseDto>.Fail("Categoria não encontrada nesta loja.");

                if (dto.IsActive && !product.IsActive)
                {
                    // Reativar conta no limite de produtos visíveis do plano.
                    var plan = await _context.GetEffectivePlanAsync(userId);
                    if (plan.MaxProductsPerStore is int max)
                    {
                        var activeCount = await _context.Product.CountAsync(p =>
                            p.StoreId == product.StoreId && p.IsActive && p.DeletionDate == null);
                        if (activeCount >= max)
                            return Response<ProductResponseDto>.Fail(
                                $"Seu plano {plan.Name} permite até {max} produtos visíveis por loja. " +
                                "Oculte outro produto antes de ativar este, ou veja os planos em Assinatura.");
                    }
                }

                product.CategoryId = dto.CategoryId;
                product.Name = dto.Name.Trim();
                product.Slug = slug;
                product.Description = EmptyToNull(dto.Description);
                product.Sku = EmptyToNull(dto.Sku);
                product.Price = dto.Price;
                product.PromotionalPrice = dto.PromotionalPrice;
                product.StockQuantity = AdjustStock(product.StockQuantity, dto.StockQuantity, dto.OriginalStockQuantity);
                product.IsActive = dto.IsActive;
                product.IsFeatured = dto.IsFeatured;
                product.ColorName = EmptyToNull(dto.ColorName);
                product.ColorHex = NormalizeHex(dto.ColorHex);
                product.UpdatedDate = DateTime.UtcNow;

                var colorError = await ApplyColorGroupAsync(product, dto.ColorLinkedProductId);
                if (colorError is not null)
                    return Response<ProductResponseDto>.Fail(colorError);

                if (dto.Images is not null)
                {
                    _context.ProductImage.RemoveRange(product.Images);
                    product.Images = BuildImages(dto.Images);
                }

                if (dto.Variants is not null)
                    SyncVariants(product, dto.Variants);

                // Com tamanhos, o estoque do produto é sempre a soma deles.
                if (product.Variants.Count > 0)
                    product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await ReloadAsDtoAsync(product.Id, userId, "Produto atualizado com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar produto");
                return Response<ProductResponseDto>.Fail("Erro ao atualizar produto. Tente novamente.");
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
                _logger.LogError(ex, "Erro ao excluir produto");
                return Response<string>.Fail("Erro ao excluir produto. Tente novamente.");
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

        private static string? ValidateVariants(List<ProductVariantInputDto>? variants)
        {
            if (variants is null || variants.Count == 0)
                return null;

            if (variants.Count > MaxVariantsPerProduct)
                return $"Cada produto pode ter no máximo {MaxVariantsPerProduct} tamanhos.";

            if (variants.Any(v => string.IsNullOrWhiteSpace(v.Size)))
                return "Informe o nome de todos os tamanhos.";

            if (variants.Any(v => v.Size.Trim().Length > MaxSizeLength))
                return $"O tamanho pode ter no máximo {MaxSizeLength} caracteres.";

            if (variants.Any(v => v.StockQuantity < 0))
                return "O estoque de um tamanho não pode ser negativo.";

            var duplicated = variants
                .GroupBy(v => v.Size.Trim().ToUpperInvariant())
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicated is not null)
                return $"O tamanho \"{duplicated.First().Size.Trim()}\" está repetido.";

            return null;
        }

        private static string? ValidateColor(string? colorName, string? colorHex, int? linkedProductId)
        {
            if (colorName is not null && colorName.Trim().Length > MaxColorNameLength)
                return $"O nome da cor pode ter no máximo {MaxColorNameLength} caracteres.";

            if (!string.IsNullOrWhiteSpace(colorHex) && !HexColor.IsMatch(colorHex.Trim()))
                return "Cor inválida. Use o formato #RRGGBB.";

            // Sem nome, a vitrine não teria como mostrar qual cor é qual.
            if (linkedProductId.HasValue && string.IsNullOrWhiteSpace(colorName))
                return "Informe o nome da cor para ligar este produto a outra cor da mesma peça.";

            return null;
        }

        private static string? NormalizeHex(string? hex)
            => string.IsNullOrWhiteSpace(hex) ? null : hex.Trim().ToUpperInvariant();

        // Coloca o produto no mesmo grupo de cores do produto escolhido.
        // Se o escolhido ainda não tem grupo, cria um e coloca os dois nele.
        // linkedProductId null = o produto sai do grupo em que estava.
        private async Task<string?> ApplyColorGroupAsync(Product product, int? linkedProductId)
        {
            if (!linkedProductId.HasValue)
            {
                product.ColorGroupId = null;
                return null;
            }

            if (linkedProductId.Value == product.Id)
                return "Um produto não pode ser ligado a ele mesmo.";

            var linked = await _context.Product.FirstOrDefaultAsync(p =>
                p.Id == linkedProductId.Value &&
                p.StoreId == product.StoreId &&
                p.DeletionDate == null);

            if (linked is null)
                return "O produto escolhido como outra cor não foi encontrado nesta loja.";

            if (linked.ColorGroupId is null)
            {
                linked.ColorGroupId = Guid.NewGuid();
                linked.UpdatedDate = DateTime.UtcNow;
            }

            product.ColorGroupId = linked.ColorGroupId;
            return null;
        }

        private static List<ProductVariant> BuildVariants(List<ProductVariantInputDto>? variants)
            => (variants ?? new())
                .Select((v, index) => new ProductVariant
                {
                    ProductId = 0, // preenchido pelo EF via navigation
                    Size = v.Size.Trim().ToUpperInvariant(),
                    StockQuantity = v.StockQuantity,
                    SortOrder = index,
                })
                .ToList();

        // Atualiza os tamanhos pelo nome em vez de apagar e recriar tudo:
        // assim o Id de um tamanho que continua existindo não muda, e os
        // itens de pedidos antigos continuam ligados a ele.
        private void SyncVariants(Product product, List<ProductVariantInputDto> incoming)
        {
            var wanted = BuildVariants(incoming);
            var wantedSizes = wanted.Select(v => v.Size).ToHashSet();

            var removed = product.Variants.Where(v => !wantedSizes.Contains(v.Size)).ToList();
            foreach (var variant in removed)
            {
                product.Variants.Remove(variant);
                _context.ProductVariant.Remove(variant);
            }

            foreach (var item in wanted)
            {
                var existing = product.Variants.FirstOrDefault(v => v.Size == item.Size);
                if (existing is null)
                {
                    product.Variants.Add(item);
                }
                else
                {
                    var original = incoming.First(v => v.Size.Trim().ToUpperInvariant() == item.Size).OriginalStockQuantity;
                    existing.StockQuantity = AdjustStock(existing.StockQuantity, item.StockQuantity, original);
                    existing.SortOrder = item.SortOrder;
                }
            }
        }

        // Estoque novo de um produto ou tamanho na edição. Com o valor que o formulário mostrava
        // ao abrir (original), aplica só a diferença que o lojista digitou sobre o estoque atual:
        // se ele não mexeu, as vendas feitas enquanto editava continuam descontadas; se mudou de
        // 10 para 15 e saíram 2 nesse meio-tempo, fica 13.
        private static int AdjustStock(int current, int edited, int? original)
            => original is int before ? Math.Max(0, current + (edited - before)) : edited;

        // Trava as linhas de estoque do produto (tamanhos, depois o total) até o fim da transação.
        // É a mesma ordem em que os pedidos e cancelamentos mexem no estoque (ver
        // AppDbContextExtensions.RestoreStockAsync), então um não fica esperando o outro para sempre.
        private async Task LockStockAsync(int productId)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"ProductVariant\" WHERE \"ProductId\" = {productId} ORDER BY \"Id\" FOR UPDATE");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Product\" WHERE \"Id\" = {productId} FOR UPDATE");
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

        private async Task<Response<ProductResponseDto>> ReloadAsDtoAsync(int productId, int userId, string message)
        {
            var dto = await _context.Product
                .Where(p => p.Id == productId)
                .Select(ToDtoExpression)
                .FirstAsync();

            await MarkHiddenByPlanAsync(dto.StoreId, userId, [dto]);
            return Response<ProductResponseDto>.Ok(dto, message);
        }

        // Marca os produtos ativos que estão fora da vitrine por passarem do limite do plano
        // (ver ProductsWithinPlan).
        private async Task MarkHiddenByPlanAsync(int storeId, int userId, IEnumerable<ProductResponseDto> products)
        {
            var plan = await _context.GetEffectivePlanAsync(userId);
            if (plan.MaxProductsPerStore is null)
                return;

            var visibleIds = (await _context.ProductsWithinPlan(storeId, plan).Select(p => p.Id).ToListAsync()).ToHashSet();
            foreach (var p in products)
                p.HiddenByPlan = p.IsActive && !visibleIds.Contains(p.Id);
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
            ColorName = p.ColorName,
            ColorHex = p.ColorHex,
            ColorGroupId = p.ColorGroupId,
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
            Variants = p.Variants
                .OrderBy(v => v.SortOrder)
                .Select(v => new ProductVariantResponseDto
                {
                    Id = v.Id,
                    Size = v.Size,
                    StockQuantity = v.StockQuantity,
                })
                .ToList(),
        };
    }
}