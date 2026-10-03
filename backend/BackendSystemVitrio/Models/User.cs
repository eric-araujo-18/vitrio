using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.Models
{
    public class User
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }

        public string? Phone { get; set; }

        // Lojista: obrigatório (validado no cadastro de lojista).
        // Cliente da vitrine: opcional — ele entra com e-mail, e o CPF só é pedido
        // se um dia o pagamento exigir.
        // O índice único continua valendo: no PostgreSQL vários NULL não conflitam,
        // e o login por CPF ignora usuários sem CPF.
        public string? Cpf { get; set; }

        public required Role Role { get; set; }

        public required byte[] PasswordHash { get; set; }
        public required byte[] PasswordSalt { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? DeletionDate { get; set; } = null;

        // Um usuário pode ter várias lojas (antes era Store? Store, 1 para 1).
        public ICollection<Store> Stores { get; set; } = new List<Store>();
    }
}