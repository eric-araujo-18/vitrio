namespace BackendSystemVitrio.DTO
{
    public class UpdateUserDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }

        // Obrigatória só para trocar o e-mail: quem pegasse uma sessão aberta poderia trocar o
        // e-mail e, pelo "esqueci minha senha", ficar com a conta.
        public string? CurrentPassword { get; set; }
    }
}