namespace BackendSystemVitrio.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public required int OrderId { get; set; }
        public Order? Order { get; set; }

        // Mantém o vínculo com o produto, mas guarda uma "foto" de nome e preço
        // no momento da compra — se o lojista mudar o preço depois, o pedido
        // antigo continua com o valor que o cliente pagou.
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        public required string ProductName { get; set; }
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
