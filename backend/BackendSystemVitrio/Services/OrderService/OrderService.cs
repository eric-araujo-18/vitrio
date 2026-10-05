using System.Linq.Expressions;
using System.Security.Cryptography;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.OrderService
{
    public class OrderService : IOrderService
    {
        private const int MaxItemsPerOrder = 50;
        private const int MaxQuantityPerItem = 999;

        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(AppDbContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===== Painel do lojista =====

        public async Task<Response<List<OrderResponseDto>>> GetOrdersByStoreAsync(int storeId, int userId, OrderStatus? status)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<List<OrderResponseDto>>.Fail("Loja não encontrada.");

                var query = _context.Order.Where(o => o.StoreId == storeId);
                if (status.HasValue)
                    query = query.Where(o => o.Status == status.Value);

                var orders = await query
                    .OrderByDescending(o => o.CreationDate)
                    .Take(200)
                    .Select(ToDtoExpression)
                    .ToListAsync();

                return Response<List<OrderResponseDto>>.Ok(orders, "Pedidos recuperados com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar pedidos");
                return Response<List<OrderResponseDto>>.Fail("Erro ao recuperar pedidos. Tente novamente.");
            }
        }

        public async Task<Response<OrderResponseDto>> GetOrderByIdAsync(int orderId, int userId)
        {
            try
            {
                var order = await OwnedOrders(userId)
                    .Where(o => o.Id == orderId)
                    .Select(ToDtoExpression)
                    .FirstOrDefaultAsync();

                return order is null
                    ? Response<OrderResponseDto>.Fail("Pedido não encontrado.")
                    : Response<OrderResponseDto>.Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar pedido");
                return Response<OrderResponseDto>.Fail("Erro ao recuperar pedido. Tente novamente.");
            }
        }

        public async Task<Response<OrderResponseDto>> UpdateStatusAsync(int orderId, int userId, OrderStatus status)
        {
            try
            {
                if (!System.Enum.IsDefined(status))
                    return Response<OrderResponseDto>.Fail("Status inválido.");

                var order = await OwnedOrders(userId)
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order is null)
                    return Response<OrderResponseDto>.Fail("Pedido não encontrado.");

                if (order.Status == status)
                    return await ReloadAsDtoAsync(order.Id, "Nada para alterar.");

                // Pedido cancelado é final: reabrir exigiria reservar o estoque de novo,
                // e ele pode já ter sido vendido pra outra pessoa.
                if (order.Status == OrderStatus.Canceled)
                    return Response<OrderResponseDto>.Fail("Pedidos cancelados não podem ser reabertos.");

                if (order.Status == OrderStatus.Delivered && status != OrderStatus.Canceled)
                    return Response<OrderResponseDto>.Fail("Pedido já entregue.");

                await using var transaction = await _context.Database.BeginTransactionAsync();

                if (status == OrderStatus.Canceled)
                {
                    // Devolve ao estoque o que foi reservado na criação do pedido.
                    foreach (var item in order.Items.Where(i => i.ProductId.HasValue))
                    {
                        // Se o tamanho ainda existe, devolve pra ele e soma no total do produto.
                        // Se o lojista apagou o tamanho, não há onde devolver: o total do
                        // produto também não muda, pra continuar igual à soma dos tamanhos.
                        if (item.VariantId.HasValue)
                        {
                            var restored = await _context.ProductVariant
                                .Where(v => v.Id == item.VariantId.Value)
                                .ExecuteUpdateAsync(set => set.SetProperty(v => v.StockQuantity, v => v.StockQuantity + item.Quantity));

                            if (restored == 0)
                                continue;
                        }

                        await _context.Product
                            .Where(p => p.Id == item.ProductId!.Value)
                            .ExecuteUpdateAsync(set => set.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
                    }
                }

                order.Status = status;
                order.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await ReloadAsDtoAsync(order.Id, "Status do pedido atualizado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar pedido");
                return Response<OrderResponseDto>.Fail("Erro ao atualizar pedido. Tente novamente.");
            }
        }

        // ===== Vitrine pública =====

        public async Task<Response<List<CustomerOrderDto>>> GetCustomerOrdersAsync(int userId, string? storeSlug)
        {
            try
            {
                var query = _context.Order.Where(o => o.CustomerUserId == userId);

                if (!string.IsNullOrWhiteSpace(storeSlug))
                    query = query.Where(o => o.Store!.Slug == storeSlug);

                var orders = await query
                    .OrderByDescending(o => o.CreationDate)
                    .Take(100)
                    .Select(o => new CustomerOrderDto
                    {
                        Id = o.Id,
                        Code = o.Code,
                        Status = o.Status,
                        Total = o.Total,
                        CreationDate = o.CreationDate,
                        StoreName = o.Store!.Name,
                        StoreSlug = o.Store.Slug,
                        StorePhone = o.Store.Phone,
                        ShippingAddress = o.ShippingCep == null
                            ? null
                            : new ShippingAddressDto
                            {
                                Cep = o.ShippingCep,
                                State = o.ShippingState!,
                                City = o.ShippingCity!,
                                Neighborhood = o.ShippingNeighborhood,
                                Street = o.ShippingStreet!,
                                Number = o.ShippingNumber!,
                                Complement = o.ShippingComplement,
                            },
                        Items = o.Items
                            .OrderBy(i => i.Id)
                            .Select(i => new OrderItemResponseDto
                            {
                                Id = i.Id,
                                ProductId = i.ProductId,
                                ProductName = i.ProductName,
                                Size = i.Size,
                                Color = i.Color,
                                ImageUrl = i.ImageUrl,
                                UnitPrice = i.UnitPrice,
                                Quantity = i.Quantity,
                            })
                            .ToList(),
                    })
                    .ToListAsync();

                return Response<List<CustomerOrderDto>>.Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar seus pedidos");
                return Response<List<CustomerOrderDto>>.Fail("Erro ao buscar seus pedidos. Tente novamente.");
            }
        }

        public async Task<Response<OrderCreatedDto>> CreatePublicOrderAsync(string storeSlug, CreateOrderDto dto, int? customerUserId = null)
        {
            try
            {
                var found = await _context.FindPublicStoreAsync(storeSlug);
                if (found is null)
                    return Response<OrderCreatedDto>.Fail("Loja não encontrada ou indisponível.");

                var store = found.Store;

                if (string.IsNullOrWhiteSpace(dto.CustomerName) || dto.CustomerName.Trim().Length < 2)
                    return Response<OrderCreatedDto>.Fail("Informe seu nome.");

                var phone = SlugHelper.OnlyDigits(dto.CustomerPhone);
                if (phone is null || phone.Length < 10 || phone.Length > 11)
                    return Response<OrderCreatedDto>.Fail("Informe um telefone válido com DDD.");

                if (dto.Items is null || dto.Items.Count == 0)
                    return Response<OrderCreatedDto>.Fail("O carrinho está vazio.");

                if (dto.Items.Count > MaxItemsPerOrder)
                    return Response<OrderCreatedDto>.Fail("Pedido com itens demais.");

                // Endereço de entrega: salvo na conta (AddressId) ou digitado no checkout.
                AddressData? shipping;
                if (dto.AddressId.HasValue)
                {
                    if (!customerUserId.HasValue)
                        return Response<OrderCreatedDto>.Fail("Entre na sua conta para usar um endereço salvo.");

                    // Só aceita endereço da própria conta.
                    var saved = await _context.CustomerAddress.FirstOrDefaultAsync(a =>
                        a.Id == dto.AddressId.Value && a.UserId == customerUserId.Value);

                    if (saved is null)
                        return Response<OrderCreatedDto>.Fail("Endereço não encontrado. Escolha outro ou digite um novo.");

                    shipping = new AddressData(saved.Cep, saved.State, saved.City, saved.Neighborhood,
                        saved.Street, saved.Number, saved.Complement);
                }
                else
                {
                    var addressError = AddressHelper.Normalize(dto.ShippingAddress, out shipping);
                    if (addressError is not null)
                        return Response<OrderCreatedDto>.Fail(addressError);
                }

                // Junta itens repetidos do mesmo produto e tamanho.
                var requested = dto.Items
                    .GroupBy(i => new { i.ProductId, i.VariantId })
                    .Select(g => new { g.Key.ProductId, g.Key.VariantId, Quantity = g.Sum(i => i.Quantity) })
                    .ToList();

                if (requested.Any(i => i.Quantity <= 0 || i.Quantity > MaxQuantityPerItem))
                    return Response<OrderCreatedDto>.Fail("Quantidade inválida em um dos itens.");

                var productIds = requested.Select(i => i.ProductId).Distinct().ToList();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Preço SEMPRE vem do banco — nunca confiar no valor enviado pelo navegador.
                // Só aceita produtos visíveis na vitrine (ativos e dentro do limite do plano).
                var products = await _context.ProductsWithinPlan(store.Id, found.Plan)
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                var order = new Order
                {
                    StoreId = store.Id,
                    CustomerUserId = customerUserId,
                    Code = await GenerateUniqueCodeAsync(),
                    CustomerName = dto.CustomerName.Trim(),
                    CustomerPhone = phone,
                    CustomerEmail = string.IsNullOrWhiteSpace(dto.CustomerEmail) ? null : dto.CustomerEmail.Trim(),
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    ShippingCep = shipping!.Cep,
                    ShippingState = shipping.State,
                    ShippingCity = shipping.City,
                    ShippingNeighborhood = shipping.Neighborhood,
                    ShippingStreet = shipping.Street,
                    ShippingNumber = shipping.Number,
                    ShippingComplement = shipping.Complement,
                };

                foreach (var item in requested)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                        return Response<OrderCreatedDto>.Fail("Um dos produtos do carrinho não está mais disponível.");

                    ProductVariant? variant = null;

                    if (product.Variants.Count > 0)
                    {
                        // Produto com tamanhos: o cliente precisa ter escolhido um, e ele
                        // precisa ser deste produto.
                        if (!item.VariantId.HasValue)
                            return Response<OrderCreatedDto>.Fail($"Escolha o tamanho de \"{product.Name}\".");

                        variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                        if (variant is null)
                            return Response<OrderCreatedDto>.Fail($"O tamanho escolhido de \"{product.Name}\" não está mais disponível.");

                        // Baixa atômica no tamanho: só decrementa se ainda houver estoque suficiente
                        // (evita vender a mesma unidade pra duas pessoas ao mesmo tempo).
                        var selectedVariantId = variant.Id;
                        var variantAffected = await _context.ProductVariant
                            .Where(v => v.Id == selectedVariantId && v.StockQuantity >= item.Quantity)
                            .ExecuteUpdateAsync(set => set.SetProperty(v => v.StockQuantity, v => v.StockQuantity - item.Quantity));

                        if (variantAffected == 0)
                            return Response<OrderCreatedDto>.Fail($"Estoque insuficiente para \"{product.Name}\" no tamanho {variant.Size}.");

                        // O total do produto acompanha a soma dos tamanhos.
                        await _context.Product
                            .Where(p => p.Id == product.Id)
                            .ExecuteUpdateAsync(set => set.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity));
                    }
                    else
                    {
                        // Produto sem tamanho: mesma baixa atômica de antes, direto no produto.
                        var affected = await _context.Product
                            .Where(p => p.Id == product.Id && p.StockQuantity >= item.Quantity)
                            .ExecuteUpdateAsync(set => set.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity));

                        if (affected == 0)
                            return Response<OrderCreatedDto>.Fail($"Estoque insuficiente para \"{product.Name}\".");
                    }

                    order.Items.Add(new OrderItem
                    {
                        OrderId = 0, // preenchido pelo EF via navigation
                        ProductId = product.Id,
                        VariantId = variant?.Id,
                        Size = variant?.Size,
                        Color = product.ColorName,
                        ProductName = product.Name,
                        ImageUrl = product.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault(),
                        UnitPrice = product.PromotionalPrice ?? product.Price,
                        Quantity = item.Quantity,
                    });
                }

                order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

                _context.Order.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Response<OrderCreatedDto>.Ok(new OrderCreatedDto
                {
                    Code = order.Code,
                    Total = order.Total,
                    StorePhone = store.Phone,
                }, "Pedido enviado com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar pedido");
                return Response<OrderCreatedDto>.Fail("Erro ao enviar pedido. Tente novamente.");
            }
        }

        // ===== Helpers =====

        private IQueryable<Order> OwnedOrders(int userId)
            => _context.Order.Where(o => o.Store!.UserId == userId && o.Store.DeletionDate == null);

        // 6 caracteres sem letras/números ambíguos (0/O, 1/I/L).
        private async Task<string> GenerateUniqueCodeAsync()
        {
            const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

            while (true)
            {
                var code = new string(Enumerable.Range(0, 6)
                    .Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)])
                    .ToArray());

                if (!await _context.Order.AnyAsync(o => o.Code == code))
                    return code;
            }
        }

        private async Task<Response<OrderResponseDto>> ReloadAsDtoAsync(int orderId, string message)
        {
            var dto = await _context.Order.Where(o => o.Id == orderId).Select(ToDtoExpression).FirstAsync();
            return Response<OrderResponseDto>.Ok(dto, message);
        }

        private static readonly Expression<Func<Order, OrderResponseDto>> ToDtoExpression = o => new OrderResponseDto
        {
            Id = o.Id,
            StoreId = o.StoreId,
            Code = o.Code,
            CustomerName = o.CustomerName,
            CustomerPhone = o.CustomerPhone,
            CustomerEmail = o.CustomerEmail,
            Notes = o.Notes,
            Status = o.Status,
            Total = o.Total,
            CreationDate = o.CreationDate,
            UpdatedDate = o.UpdatedDate,
            ShippingAddress = o.ShippingCep == null
                ? null
                : new ShippingAddressDto
                {
                    Cep = o.ShippingCep,
                    State = o.ShippingState!,
                    City = o.ShippingCity!,
                    Neighborhood = o.ShippingNeighborhood,
                    Street = o.ShippingStreet!,
                    Number = o.ShippingNumber!,
                    Complement = o.ShippingComplement,
                },
            Items = o.Items
                .OrderBy(i => i.Id)
                .Select(i => new OrderItemResponseDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Size = i.Size,
                    Color = i.Color,
                    ImageUrl = i.ImageUrl,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                })
                .ToList(),
        };
    }
}