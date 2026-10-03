using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.Models
{
    // Pedido feito pelo visitante na vitrine pública. Não exige conta:
    // o cliente informa nome e telefone no checkout.
    public class Order
    {
        public int Id { get; set; }

        public required int StoreId { get; set; }
        public Store? Store { get; set; }

        // Código curto e legível mostrado ao cliente e ao lojista (ex: "A7K3Q9").
        public required string Code { get; set; }

        // Conta do cliente que fez o pedido, se ele estava logado (opcional:
        // continua sendo possível comprar sem conta). Usado em "Meus pedidos".
        public int? CustomerUserId { get; set; }
        public User? CustomerUser { get; set; }

        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Notes { get; set; }

        // ===== Endereço de entrega =====
        // Cópia do endereço no momento do pedido (digitado no checkout ou vindo de um
        // endereço salvo). Nulo só em pedidos antigos, de antes do endereço existir.
        public string? ShippingCep { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingNeighborhood { get; set; }
        public string? ShippingStreet { get; set; }
        public string? ShippingNumber { get; set; }
        public string? ShippingComplement { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public decimal Total { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}