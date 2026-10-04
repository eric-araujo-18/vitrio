namespace BackendSystemVitrio.Models
{
    // Plano de assinatura do lojista. Os planos ficam no banco (semeados pela migration),
    // então mudar preço ou limite é uma migration/UPDATE, não uma mudança de código.
    public class Plan
    {
        // Ids fixos usados no seed e no código
        public const int FreeId = 1;

        public int Id { get; set; }

        // Identificador estável: "free", "basic", "pro"
        public required string Code { get; set; }

        public required string Name { get; set; }
        public string? Description { get; set; }

        public decimal PriceMonthly { get; set; }

        // Limites
        public int MaxStores { get; set; }
        public int? MaxProductsPerStore { get; set; } // null = ilimitado
        public bool AllowsOnlinePayment { get; set; }

        // Aparece na lista de planos para assinar
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}