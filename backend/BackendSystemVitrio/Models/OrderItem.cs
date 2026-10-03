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

        // Tamanho escolhido (quando o produto tem variações).
        // Size é uma cópia em texto: o pedido continua mostrando "M" mesmo
        // se o lojista apagar essa variação depois.
        public int? VariantId { get; set; }
        public ProductVariant? Variant { get; set; }
        public string? Size { get; set; }

        // Cópia da cor do produto no momento da compra (ex: "Azul marinho").
        public string? Color { get; set; }

        public required string ProductName { get; set; }
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}