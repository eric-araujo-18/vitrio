namespace BackendSystemVitrio.DTO
{
    // Todos opcionais: só o que vier preenchido é alterado.
    // Pra limpar um campo opcional (descrição, logo, telefone, CNPJ), mande string vazia "".
    public class UpdateStoreDto
    {
        public string? Name { get; set; }
        public string? Cnpj { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? Phone { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? TertiaryColor { get; set; }

        // Permite ativar/pausar a loja (soft toggle, não deleção).
        public bool? IsActive { get; set; }

        // E-mail para o dono a cada pedido novo.
        public bool? NotifyNewOrdersByEmail { get; set; }

        // Regra de entrega. Quando vem, substitui a anterior inteira (null dentro dela tem significado).
        public DeliverySettingsDto? Delivery { get; set; }
    }

    public class DeliverySettingsDto
    {
        // Null = frete a combinar com o cliente; 0 = grátis; > 0 = taxa fixa.
        public decimal? Fee { get; set; }
        // Frete grátis a partir deste valor em produtos (só com taxa > 0). Null = nunca.
        public decimal? FreeFrom { get; set; }
    }
}
