using BackendSystemVitrio.Models;

namespace BackendSystemVitrio.Helpers
{
    // Regra de entrega da loja (Store.DeliveryFee / FreeDeliveryFrom). O checkout da vitrine faz a
    // mesma conta para mostrar o total (deliveryFeeFor em lib/format.ts).
    public static class DeliveryHelper
    {
        public const decimal MaxFee = 10_000m;

        // Taxa do pedido para esta soma de produtos. Null = frete a combinar (fora do total).
        public static decimal? FeeFor(Store store, decimal productsTotal)
        {
            if (store.DeliveryFee is not { } fee)
                return null;
            if (store.FreeDeliveryFrom is { } freeFrom && productsTotal >= freeFrom)
                return 0m;
            return fee;
        }

        // Null quando a regra serve; senão, a mensagem para o lojista.
        public static string? Validate(decimal? fee, decimal? freeFrom)
        {
            if (fee is < 0 or > MaxFee)
                return "A taxa de entrega precisa estar entre R$ 0,00 e R$ 10.000,00.";
            if (fee is not null && decimal.Round(fee.Value, 2) != fee.Value)
                return "A taxa de entrega pode ter no máximo 2 casas decimais.";
            if (freeFrom is not null)
            {
                if (fee is null or 0)
                    return "O frete grátis a partir de um valor só vale com uma taxa de entrega.";
                if (freeFrom <= 0 || freeFrom > 1_000_000m || decimal.Round(freeFrom.Value, 2) != freeFrom.Value)
                    return "Informe um valor válido para o frete grátis.";
            }
            return null;
        }
    }
}
