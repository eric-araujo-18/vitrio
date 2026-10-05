namespace BackendSystemVitrio.Models
{
    // Variação de um produto — hoje usada para tamanhos (P, M, G... ou 36, 38, 40...).
    // Cada tamanho tem o próprio estoque. Quando o produto tem variações,
    // Product.StockQuantity passa a ser a soma do estoque delas (mantida pelo backend),
    // então dashboard, cards e o selo "Esgotado" continuam funcionando sem mudança.
    public class ProductVariant
    {
        public int Id { get; set; }

        public required int ProductId { get; set; }
        public Product? Product { get; set; }

        // Texto livre (não enum) pra aceitar grades diferentes: "P", "GG", "42", "Único"...
        // Único por produto.
        public required string Size { get; set; }

        public int StockQuantity { get; set; } = 0;

        // Ordem de exibição (0 = primeiro), pra não aparecer "G, M, P".
        public int SortOrder { get; set; } = 0;
    }
}