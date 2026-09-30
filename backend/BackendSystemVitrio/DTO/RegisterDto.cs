namespace BackendSystemVitrio.DTO
{
    public class RegisterDto
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }

        // Obrigatório: é a credencial usada no login.
        public required string Cpf { get; set; }

        public string? Phone { get; set; }

        // Removido "Role": antes o próprio cliente escolhia o papel no cadastro,
        // então qualquer pessoa podia mandar role = 1 e virar Admin.
        // Agora todo cadastro público é Shopkeeper (definido no AuthService).
    }
}
