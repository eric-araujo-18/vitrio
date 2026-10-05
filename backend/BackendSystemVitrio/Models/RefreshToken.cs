// Models/RefreshToken.cs
namespace BackendSystemVitrio.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User? User { get; set; }

        // Hash SHA-256 (hex) do valor aleatório opaco que fica no cookie. O valor em si
        // nunca é salvo (ver AuthService.HashToken). Não é um JWT.
        public required string Token { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }

        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}