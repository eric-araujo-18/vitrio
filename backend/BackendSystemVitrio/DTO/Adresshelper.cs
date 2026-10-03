using BackendSystemVitrio.DTO;

namespace BackendSystemVitrio.Helpers
{
    // Endereço já validado e limpo (CEP só com dígitos, UF maiúscula, textos aparados).
    public record AddressData(
        string Cep,
        string State,
        string City,
        string? Neighborhood,
        string Street,
        string Number,
        string? Complement);

    public static class AddressHelper
    {
        public static readonly HashSet<string> States = new()
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
            "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
        };

        // Valida e normaliza. Retorna a mensagem de erro, ou null se estiver tudo certo.
        public static string? Normalize(AddressInputDto? input, out AddressData? address)
        {
            address = null;

            if (input is null)
                return "Informe o endereço de entrega.";

            var cep = SlugHelper.OnlyDigits(input.Cep);
            if (cep is null || cep.Length != 8)
                return "CEP inválido. Use os 8 números.";

            var state = (input.State ?? "").Trim().ToUpperInvariant();
            if (!States.Contains(state))
                return "Selecione a UF.";

            var city = Clean(input.City);
            if (city is null || city.Length < 2)
                return "Informe o município.";
            if (city.Length > 100)
                return "O município pode ter no máximo 100 caracteres.";

            var street = Clean(input.Street);
            if (street is null || street.Length < 2)
                return "Informe o nome da rua.";
            if (street.Length > 150)
                return "A rua pode ter no máximo 150 caracteres.";

            var number = Clean(input.Number);
            if (number is null)
                return "Informe o número (ou S/N se não tiver).";
            if (number.Length > 20)
                return "O número pode ter no máximo 20 caracteres.";

            var neighborhood = Clean(input.Neighborhood);
            if (neighborhood is { Length: > 100 })
                return "O bairro pode ter no máximo 100 caracteres.";

            var complement = Clean(input.Complement);
            if (complement is { Length: > 100 })
                return "O complemento pode ter no máximo 100 caracteres.";

            address = new AddressData(cep, state, city, neighborhood, street, number, complement);
            return null;
        }

        private static string? Clean(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}