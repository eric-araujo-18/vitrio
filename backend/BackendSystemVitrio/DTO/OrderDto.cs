using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.DTO
{
    // ===== Checkout (vitrine pública) =====

    public class CreateOrderItemDto
    {
        public int ProductId { get; set; }

        // Obrigatório quando o produto tem tamanhos.
        public int? VariantId { get; set; }

        public int Quantity { get; set; }
    }

    public class CreateOrderDto
    {
        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Notes { get; set; }
        public List<CreateOrderItemDto> Items { get; set; } = new();

        // Endereço de entrega — um dos dois:
        // AddressId: endereço salvo na conta (só para cliente logado);
        // ShippingAddress: endereço digitado no checkout (com ou sem login).
        public int? AddressId { get; set; }
        public AddressInputDto? ShippingAddress { get; set; }
    }

    // ===== Painel do lojista =====

    public class UpdateOrderStatusDto
    {
        public OrderStatus Status { get; set; }
    }

    public class OrderItemResponseDto
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public required string ProductName { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal => UnitPrice * Quantity;
    }

    public class OrderResponseDto
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public required string Code { get; set; }
        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Notes { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        // Nulo em pedidos antigos, de antes do endereço existir
        public ShippingAddressDto? ShippingAddress { get; set; }

        public List<OrderItemResponseDto> Items { get; set; } = new();
    }

    // "Meus pedidos" do cliente logado
    public class CustomerOrderDto
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
        public DateTime CreationDate { get; set; }
        public required string StoreName { get; set; }
        public required string StoreSlug { get; set; }
        public string? StorePhone { get; set; }
        public ShippingAddressDto? ShippingAddress { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new();
    }

    public class OrderSummaryDto
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string CustomerName { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreationDate { get; set; }
    }

    // Resposta do checkout pro cliente (sem dados internos).
    public class OrderCreatedDto
    {
        public required string Code { get; set; }
        public decimal Total { get; set; }
        public string? StorePhone { get; set; }
    }
}