namespace BackendSystemVitrio.DTO
{
    // Login por e-mail ou CPF (o backend descobre qual é pelo formato).
    // Lojistas podem continuar entrando com CPF; clientes da vitrine entram com e-mail.
    public class LoginDto
    {
        // E-mail ou CPF
        public string? Login { get; set; }

        // Campo antigo (login só por CPF). Mantido para não quebrar clientes antigos do front.
        public string? Cpf { get; set; }

        public required string Password { get; set; }
    }
}