namespace BackendSystemVitrio.DTO
{
    public class CreateStoreDto
    {
        public required string Name { get; set; }

        // Opcional: MEI pode ter só o CPF do dono (já cadastrado no User).
        public string? Cnpj { get; set; }

        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? Phone { get; set; }

        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? TertiaryColor { get; set; }
    }
}
