namespace BackendSystemVitrio.DTO
{
    // Usado tanto para salvar um endereço na conta quanto para o endereço
    // digitado direto no checkout (cliente sem login).
    public class AddressInputDto
    {
        public string? Label { get; set; }
        public required string Cep { get; set; }
        public required string State { get; set; }
        public required string City { get; set; }
        public string? Neighborhood { get; set; }
        public required string Street { get; set; }
        public required string Number { get; set; }
        public string? Complement { get; set; }

        // Só para endereços salvos na conta
        public bool IsDefault { get; set; }
    }

    // Endereço salvo na conta
    public class AddressResponseDto
    {
        public int Id { get; set; }
        public string? Label { get; set; }
        public required string Cep { get; set; }
        public required string State { get; set; }
        public required string City { get; set; }
        public string? Neighborhood { get; set; }
        public required string Street { get; set; }
        public required string Number { get; set; }
        public string? Complement { get; set; }
        public bool IsDefault { get; set; }
    }

    // Endereço de entrega copiado no pedido
    public class ShippingAddressDto
    {
        public required string Cep { get; set; }
        public required string State { get; set; }
        public required string City { get; set; }
        public string? Neighborhood { get; set; }
        public required string Street { get; set; }
        public required string Number { get; set; }
        public string? Complement { get; set; }
    }
}