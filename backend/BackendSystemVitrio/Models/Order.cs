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

        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Notes { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public decimal Total { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
