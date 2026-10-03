namespace BackendSystemVitrio.DTO
{
    // Cadastro de cliente da vitrine.
    // Sem campo de papel (Role): este endpoint SEMPRE cria Client, decidido pelo servidor.
    // Sem CPF: o cliente entra com e-mail.
    public class RegisterClientDto
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public string? Phone { get; set; }
    }
}