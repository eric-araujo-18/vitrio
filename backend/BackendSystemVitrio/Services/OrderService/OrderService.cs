using System.Linq.Expressions;
using System.Security.Cryptography;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.OrderPaymentService;
using BackendSystemVitrio.Services.Payments;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.OrderService
{
    public class OrderService : IOrderService
    {
        private const int MaxItemsPerOrder = 50;
        private const int MaxQuantityPerItem = 999;
        // Pedidos por página no painel (ORDERS_PAGE_SIZE no frontend).
        private const int OrdersPageSize = 50;
        // Mesmo limite do campo no checkout.
        private const int MaxNotesLength = 500;

        private readonly AppDbContext _context;
        private readonly IOrderPaymentService _payments;
        private readonly NewOrderNotifier _notifier;
        private readonly MercadoPagoOptions _mercadoPagoOptions;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            AppDbContext context,
            IOrderPaymentService payments,
            NewOrderNotifier notifier,
            IOptions<MercadoPagoOptions> mercadoPagoOptions,
            ILogger<OrderService> logger)
        {
            _context = context;
            _payments = payments;
            _notifier = notifier;
            _mercadoPagoOptions = mercadoPagoOptions.Value;
            _logger = logger;
        }

        // ===== Painel do lojista =====

        public async Task<Response<List<OrderResponseDto>>> GetOrdersByStoreAsync(int storeId, int userId, OrderStatus? status, int? beforeId = null)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<List<OrderResponseDto>>.Fail("Loja não encontrada.");

                var query = _context.Order.Where(o => o.StoreId == storeId);
                if (status.HasValue)
                    query = query.Where(o => o.Status == status.Value);
                // Pelo id (e não por "pular N"): pedidos novos chegando no topo não fazem a
                // próxima página repetir pedidos.
                if (beforeId.HasValue)
                    query = query.Where(o => o.Id < beforeId.Value);

                var orders = await query
                    .OrderByDescending(o => o.Id)
                    .Take(OrdersPageSize)
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

        public async Task<Response<PendingOrdersSummaryDto>> GetPendingSummaryAsync(int storeId, int userId)
        {
            try
            {
                var store = await _context.FindOwnedStoreAsync(storeId, userId);
                if (store is null)
                    return Response<PendingOrdersSummaryDto>.Fail("Loja não encontrada.");

                var pending = _context.Order.Where(o => o.StoreId == storeId && o.Status == OrderStatus.Pending);

                return Response<PendingOrdersSummaryDto>.Ok(new PendingOrdersSummaryDto
                {
                    PendingCount = await pending.CountAsync(),
                    Latest = await pending
                        .OrderByDescending(o => o.Id)
                        .Select(o => new OrderSummaryDto
                        {
                            Id = o.Id,
                            Code = o.Code,
                            CustomerName = o.CustomerName,
                            Status = o.Status,
                            PaymentStatus = o.PaymentStatus,
                            Total = o.Total,
                            ItemCount = o.Items.Sum(i => i.Quantity),
                            CreationDate = o.CreationDate,
                        })
                        .FirstOrDefaultAsync(),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar pedidos pendentes");
                return Response<PendingOrdersSummaryDto>.Fail("Erro ao verificar pedidos pendentes. Tente novamente.");
            }
        }

        public async Task<Response<OrderResponseDto>> UpdateStatusAsync(int orderId, int userId, OrderStatus status)
        {
            try
            {
                if (!System.Enum.IsDefined(status))
                    return Response<OrderResponseDto>.Fail("Status inválido.");

                var order = await OwnedOrders(userId)
                    .AsNoTracking()
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order is null)
                    return Response<OrderResponseDto>.Fail("Pedido não encontrado.");

                if (order.Status == status)
                    return await ReloadAsDtoAsync(order.Id, "Nada para alterar.");

                // "Aguardando pagamento" é controlado pelo pagamento online, não pelo lojista.
                if (status == OrderStatus.AwaitingPayment)
                    return Response<OrderResponseDto>.Fail("Status inválido.");

                if (order.Status == OrderStatus.AwaitingPayment)
                {
                    if (status != OrderStatus.Canceled)
                        return Response<OrderResponseDto>.Fail(
                            "Este pedido está esperando o pagamento do cliente. Ele muda sozinho quando o pagamento for aprovado.");

                    // Se o cliente pagar mesmo assim, o pagamento é estornado automaticamente.
                    return await _payments.CancelAwaitingAsync(order.Id, order.StoreId) switch
                    {
                        true => await ReloadAsDtoAsync(order.Id, "Pedido cancelado."),
                        false => Response<OrderResponseDto>.Fail("O pagamento deste pedido acabou de ser aprovado. Atualize a lista de pedidos."),
                        null => Response<OrderResponseDto>.Fail("Não foi possível conferir o pagamento no Mercado Pago agora. Tente de novo em instantes."),
                    };
                }

                // Pedido cancelado é final: reabrir exigiria reservar o estoque de novo,
                // e ele pode já ter sido vendido pra outra pessoa.
                if (order.Status == OrderStatus.Canceled)
                    return Response<OrderResponseDto>.Fail("Pedidos cancelados não podem ser reabertos.");

                if (order.Status == OrderStatus.Delivered)
                    return Response<OrderResponseDto>.Fail("Pedido já entregue.");

                if (!AllowedNextStatus.TryGetValue(order.Status, out var allowed) || !allowed.Contains(status))
                    return Response<OrderResponseDto>.Fail(
                        $"Não dá para passar de {StatusLabel(order.Status)} para {StatusLabel(status)}.");

                // Cancelar um pedido pago online devolve o dinheiro ao cliente. Se o estorno falhar
                // por um erro passageiro, o pedido não é cancelado (o lojista tenta de novo). Se a
                // conta conectada não alcança mais o pagamento (desconectada, acesso revogado ou
                // outra conta), cancela, e o lojista devolve o valor pelo Mercado Pago.
                var refunded = false;
                var manualRefund = false;
                if (status == OrderStatus.Canceled && order.PaymentStatus == OrderPaymentStatus.Approved)
                {
                    switch (await _payments.RefundAsync(order))
                    {
                        case RefundResult.Refunded:
                            refunded = true;
                            break;
                        case RefundResult.Manual:
                            manualRefund = true;
                            break;
                        default:
                            return Response<OrderResponseDto>.Fail(
                                "Não foi possível estornar o pagamento no Mercado Pago agora. Tente de novo em alguns minutos.");
                    }
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();
                var now = DateTime.UtcNow;
                int affected;

                if (status == OrderStatus.Canceled)
                {
                    // Condicional: dois cancelamentos ao mesmo tempo (duas abas) não devolvem o
                    // estoque duas vezes. Só o primeiro muda o pedido; o outro não encontra nada.
                    affected = await _context.Order
                        .Where(o => o.Id == order.Id && o.Status != OrderStatus.Canceled && o.Status != OrderStatus.AwaitingPayment)
                        .ExecuteUpdateAsync(set => set
                            .SetProperty(o => o.Status, OrderStatus.Canceled)
                            .SetProperty(o => o.PaymentStatus, o => refunded ? OrderPaymentStatus.Refunded : o.PaymentStatus)
                            .SetProperty(o => o.UpdatedDate, now));

                    // Devolve ao estoque o que foi reservado na criação do pedido.
                    if (affected == 1)
                        await _context.RestoreStockAsync(order.Items);
                }
                else
                {
                    // Só se ninguém mudou o pedido desde que ele foi lido.
                    var current = order.Status;
                    affected = await _context.Order
                        .Where(o => o.Id == order.Id && o.Status == current)
                        .ExecuteUpdateAsync(set => set
                            .SetProperty(o => o.Status, status)
                            .SetProperty(o => o.UpdatedDate, now));
                }

                if (affected == 0)
                    return Response<OrderResponseDto>.Fail(
                        "Este pedido foi alterado ao mesmo tempo em outra tela. Atualize a lista de pedidos.");

                await transaction.CommitAsync();

                return await ReloadAsDtoAsync(order.Id,
                    refunded ? "Pedido cancelado e pagamento estornado ao cliente."
                    : manualRefund ? "Pedido cancelado. A conta do Mercado Pago conectada à loja não consegue mais estornar este pagamento: devolva o valor ao cliente pelo app do Mercado Pago."
                    : "Status do pedido atualizado.");
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
                var payableUntilAfter = DateTime.UtcNow + OrderPaymentService.OrderPaymentService.PixMinimumValidity;
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
                        PaymentMethod = o.PaymentMethod,
                        PaymentStatus = o.PaymentStatus,
                        // O link vale enquanto ainda dá para começar a pagar (até 30 min antes do prazo).
                        PaymentCheckoutUrl = o.Status == OrderStatus.AwaitingPayment && o.PaymentDeadline > payableUntilAfter
                            ? o.PaymentCheckoutUrl
                            : null,
                        PaymentDeadline = o.PaymentDeadline == null ? null : o.PaymentDeadline.Value.AddMinutes(-OrderPaymentService.OrderPaymentService.PixMinimumValidity.TotalMinutes),
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

                var online = dto.PaymentMethod == OrderPaymentMethod.Online;
                if (online && !await _payments.IsAvailableAsync(store.Id, found.Plan))
                    return Response<OrderCreatedDto>.Fail("Esta loja não está recebendo pagamento online agora. Escolha combinar com a loja.");

                if (string.IsNullOrWhiteSpace(dto.CustomerName) || dto.CustomerName.Trim().Length < 2)
                    return Response<OrderCreatedDto>.Fail("Informe seu nome.");
                if (dto.CustomerName.Trim().Length > ValidationHelper.MaxNameLength)
                    return Response<OrderCreatedDto>.Fail($"O nome pode ter no máximo {ValidationHelper.MaxNameLength} caracteres.");

                var phone = SlugHelper.OnlyDigits(dto.CustomerPhone);
                if (phone is null || phone.Length < 10 || phone.Length > 11)
                    return Response<OrderCreatedDto>.Fail("Informe um telefone válido com DDD.");

                // No pagamento online o e-mail vai para o Mercado Pago, que recusa um inválido.
                var email = string.IsNullOrWhiteSpace(dto.CustomerEmail) ? null : dto.CustomerEmail.Trim();
                if (email is not null && !ValidationHelper.IsValidEmail(email))
                    return Response<OrderCreatedDto>.Fail("E-mail inválido.");

                var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
                if (notes?.Length > MaxNotesLength)
                    return Response<OrderCreatedDto>.Fail($"As observações podem ter no máximo {MaxNotesLength} caracteres.");

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

                // Junta itens repetidos do mesmo produto e tamanho (na ordem do carrinho).
                var requested = dto.Items
                    .GroupBy(i => new { i.ProductId, i.VariantId })
                    .Select(g => new { g.Key.ProductId, g.Key.VariantId, Quantity = g.Sum(i => i.Quantity) })
                    .ToList();

                if (requested.Any(i => i.Quantity <= 0 || i.Quantity > MaxQuantityPerItem))
                    return Response<OrderCreatedDto>.Fail("Quantidade inválida em um dos itens.");

                var productIds = requested.Select(i => i.ProductId).Distinct().ToList();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Preço SEMPRE vem do banco — nunca confiar no valor enviado pelo navegador.
                // Só aceita produtos visíveis na vitrine (ativos, dentro do limite do plano e fora
                // de categorias desativadas).
                var products = await (await _context.VisibleProductsAsync(found))
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
                    CustomerEmail = email,
                    Notes = notes,
                    ShippingCep = shipping!.Cep,
                    ShippingState = shipping.State,
                    ShippingCity = shipping.City,
                    ShippingNeighborhood = shipping.Neighborhood,
                    ShippingStreet = shipping.Street,
                    ShippingNumber = shipping.Number,
                    ShippingComplement = shipping.Complement,
                };

                if (online)
                {
                    // O estoque fica reservado até o prazo; sem pagamento, a manutenção cancela o
                    // pedido e devolve. O lojista só é avisado quando o pagamento for aprovado.
                    order.Status = OrderStatus.AwaitingPayment;
                    order.PaymentMethod = OrderPaymentMethod.Online;
                    order.PaymentStatus = OrderPaymentStatus.Pending;
                    order.PaymentDeadline = DateTime.UtcNow.AddMinutes(_mercadoPagoOptions.EffectiveOrderPaymentMinutes);
                }

                // A baixa do estoque segue a ordem (produto, tamanho), a mesma do cancelamento e da
                // edição do produto (ver RestoreStockAsync): assim duas operações ao mesmo tempo não
                // ficam esperando uma pela outra. Os itens do pedido ficam na ordem do carrinho.
                var created = new SortedList<int, OrderItem>();
                foreach (var (item, position) in requested
                             .Select((item, position) => (item, position))
                             .OrderBy(x => x.item.ProductId)
                             .ThenBy(x => x.item.VariantId))
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

                    created.Add(position, new OrderItem
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

                foreach (var item in created.Values)
                    order.Items.Add(item);

                order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

                _context.Order.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Pedido pago online só vira pedido de verdade quando o pagamento é aprovado: o aviso
                // ao lojista sai nessa hora (OrderPaymentService).
                if (!online)
                    await _notifier.NotifyAsync(order.Id);

                string? checkoutUrl = null;
                if (online)
                {
                    try
                    {
                        checkoutUrl = await _payments.StartCheckoutAsync(order);
                    }
                    catch (Exception ex)
                    {
                        // Sem o link não dá para pagar: desfaz o pedido e devolve o estoque.
                        _logger.LogError(ex, "Erro ao criar o pagamento do pedido {OrderId}", order.Id);
                        await _payments.CancelUnpaidAsync(order.Id);
                        return Response<OrderCreatedDto>.Fail(
                            "Não foi possível abrir o pagamento online agora. Tente de novo ou escolha combinar com a loja.");
                    }
                }

                return Response<OrderCreatedDto>.Ok(new OrderCreatedDto
                {
                    Code = order.Code,
                    Total = order.Total,
                    StorePhone = store.Phone,
                    PaymentMethod = order.PaymentMethod,
                    CheckoutUrl = checkoutUrl,
                    PaymentDeadline = order.PaymentDeadline is null
                        ? null
                        : OrderPaymentService.OrderPaymentService.CheckoutClosesAt(order.PaymentDeadline.Value),
                }, online ? "Pedido criado. Falta só o pagamento." : "Pedido enviado com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar pedido");
                return Response<OrderCreatedDto>.Fail("Erro ao enviar pedido. Tente novamente.");
            }
        }

        // ===== Helpers =====

        // Próximos status que o lojista pode escolher: os mesmos que o painel oferece
        // (ORDER_NEXT_STATUS em lib/format.ts). Entregue e Cancelado são finais; "Aguardando
        // pagamento" tem regra própria. Cancelar um pedido entregue devolveria ao estoque algo
        // que já saiu da loja.
        private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedNextStatus = new()
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Canceled],
            [OrderStatus.Confirmed] = [OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Canceled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Canceled],
        };

        private static string StatusLabel(OrderStatus status) => status switch
        {
            OrderStatus.AwaitingPayment => "Aguardando pagamento",
            OrderStatus.Pending => "Pendente",
            OrderStatus.Confirmed => "Confirmado",
            OrderStatus.Shipped => "Enviado",
            OrderStatus.Delivered => "Entregue",
            OrderStatus.Canceled => "Cancelado",
            _ => status.ToString(),
        };

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
            PaymentMethod = o.PaymentMethod,
            PaymentStatus = o.PaymentStatus,
            PaidAt = o.PaidAt,
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