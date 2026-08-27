using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.ProductService
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<List<ProductResponseDto>>> GetProductsByStore(int storeId, int userId)
        {
            Response<List<ProductResponseDto>> response = new Response<List<ProductResponseDto>>();

            try
            {
                var store = await _context.Store
                    .FirstOrDefaultAsync(s => s.Id == storeId && s.DeletionDate == null);

                if (store is null)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "Loja não encontrada.";
                    return response;
                }

                if (store.UserId != userId)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "Você não tem permissão para acessar esta loja.";
                    return response;
                }

                var products = await _context.Product
                    .Where(p => p.StoreId == storeId && p.DeletionDate == null)
                    .OrderByDescending(p => p.CreationDate)
                    .Select(p => new ProductResponseDto
                    {
                        Id = p.Id,
                        StoreId = p.StoreId,
                        CategoryId = p.CategoryId,
                        Category = p.Category == null ? null : new ProductCategoryDto
                        {
                            Id = p.Category.Id,
                            Name = p.Category.Name,
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
                    })
                    .ToListAsync();

                response.Dados = products;
                response.Status = true;
                response.Mensagem = "Produtos recuperados com sucesso.";
            }
            catch (Exception ex)
            {
                response.Dados = null;
                response.Status = false;
                response.Mensagem = $"Erro ao recuperar produtos: {ex.Message}";
            }

            return response;
        }

        public async Task<Response<ProductResponseDto>> CreateProduct(int userId, CreateProductDto dto)
        {
            Response<ProductResponseDto> response = new Response<ProductResponseDto>();

            try
            {
                var store = await _context.Store
                    .FirstOrDefaultAsync(s => s.Id == dto.StoreId && s.DeletionDate == null);

                if (store is null)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "Loja não encontrada.";
                    return response;
                }

                if (store.UserId != userId)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "Você não tem permissão para acessar esta loja.";
                    return response;
                }

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "O nome do produto é obrigatório.";
                    return response;
                }

                if (string.IsNullOrWhiteSpace(dto.Slug))
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "O slug do produto é obrigatório.";
                    return response;
                }

                if (dto.Price <= 0)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "Informe um preço válido.";
                    return response;
                }

                if (dto.PromotionalPrice.HasValue && dto.PromotionalPrice.Value >= dto.Price)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "O preço promocional precisa ser menor que o preço normal.";
                    return response;
                }

                if (dto.StockQuantity < 0)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "A quantidade em estoque não pode ser negativa.";
                    return response;
                }

                // slug único por loja (StoreId, Slug)
                var slugInUse = await _context.Product.AnyAsync(p =>
                    p.StoreId == dto.StoreId && p.Slug == dto.Slug && p.DeletionDate == null);

                if (slugInUse)
                {
                    response.Dados = null;
                    response.Status = false;
                    response.Mensagem = "Já existe um produto com esse slug nesta loja.";
                    return response;
                }

                if (dto.CategoryId.HasValue)
                {
                    var categoryExists = await _context.Category.AnyAsync(c =>
                        c.Id == dto.CategoryId.Value && c.StoreId == dto.StoreId && c.DeletionDate == null);

                    if (!categoryExists)
                    {
                        response.Dados = null;
                        response.Status = false;
                        response.Mensagem = "Categoria não encontrada nesta loja.";
                        return response;
                    }
                }

                var product = new Product
                {
                    StoreId = dto.StoreId,
                    CategoryId = dto.CategoryId,
                    Name = dto.Name.Trim(),
                    Slug = dto.Slug.Trim(),
                    Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                    Sku = string.IsNullOrWhiteSpace(dto.Sku) ? null : dto.Sku.Trim(),
                    Price = dto.Price,
                    PromotionalPrice = dto.PromotionalPrice,
                    StockQuantity = dto.StockQuantity,
                    IsActive = dto.IsActive,
                    IsFeatured = dto.IsFeatured,
                };

                if (dto.Images is { Count: > 0 })
                {
                    product.Images = dto.Images
                        .Select(img => new ProductImage
                        {
                            Url = img.Url,
                            Order = img.Order,
                            ProductId = product.Id, // EF preenche corretamente ao salvar via navigation
                        })
                        .ToList();
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                _context.Product.Add(product);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // recarrega a categoria (se houver) só pra devolver o nome no response
                ProductCategoryDto? categoryDto = null;
                if (product.CategoryId.HasValue)
                {
                    var category = await _context.Category
                        .Where(c => c.Id == product.CategoryId.Value)
                        .Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name })
                        .FirstOrDefaultAsync();
                    categoryDto = category;
                }

                response.Dados = new ProductResponseDto
                {
                    Id = product.Id,
                    StoreId = product.StoreId,
                    CategoryId = product.CategoryId,
                    Category = categoryDto,
                    Name = product.Name,
                    Slug = product.Slug,
                    Description = product.Description,
                    Sku = product.Sku,
                    Price = product.Price,
                    PromotionalPrice = product.PromotionalPrice,
                    StockQuantity = product.StockQuantity,
                    IsActive = product.IsActive,
                    IsFeatured = product.IsFeatured,
                    CreationDate = product.CreationDate,
                    UpdatedDate = product.UpdatedDate,
                    Images = product.Images
                        .OrderBy(img => img.Order)
                        .Select(img => new ProductImageResponseDto
                        {
                            Id = img.Id,
                            Url = img.Url,
                            Order = img.Order,
                        })
                        .ToList(),
                };
                response.Status = true;
                response.Mensagem = "Produto criado com sucesso.";
            }
            catch (Exception ex)
            {
                response.Dados = null;
                response.Status = false;
                response.Mensagem = $"Erro ao criar produto: {ex.Message}";
            }

            return response;
        }
    }
}