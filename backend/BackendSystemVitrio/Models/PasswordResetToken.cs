namespace BackendSystemVitrio.Models
{
    // Link de "esqueci minha senha". Vale por pouco tempo e uma vez só: ao redefinir a
    // senha a linha é apagada, e pedir um link novo apaga os anteriores do usuário.
    public class PasswordResetToken
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User? User { get; set; }

        // Hash SHA-256 (hex) do token enviado no e-mail. O token em si nunca é salvo.
        public required string TokenHash { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    }
}
