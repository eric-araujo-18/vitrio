using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace BackendSystemVitrio.Helpers
{
    // Hash de senha com o PasswordHasher do ASP.NET (PBKDF2-HMACSHA512 com muitas
    // iterações): lento de propósito, para dificultar força bruta se o banco vazar.
    //
    // Dois formatos convivem nas colunas PasswordHash/PasswordSalt (sem migration):
    // - novo: PasswordSalt vazio; PasswordHash guarda a saída do PasswordHasher (o salt vai embutido nela);
    // - antigo: HMACSHA512 de uma passada, com o salt em PasswordSalt. Continua aceito no login e é
    //   regravado no formato novo assim que o usuário entra (ver NeedsRehash).
    public static class PasswordHelper
    {
        public const int MinLength = 8;

        private static readonly PasswordHasher<object> Hasher = new();

        public static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
        {
            hash = Convert.FromBase64String(Hasher.HashPassword(null!, password));
            salt = [];
        }

        public static bool VerifyPasswordHash(string password, byte[] hash, byte[] salt)
        {
            if (salt.Length == 0)
                return Hasher.VerifyHashedPassword(null!, Convert.ToBase64String(hash), password)
                       != PasswordVerificationResult.Failed;

            // Formato antigo
            using var hmac = new HMACSHA512(salt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            // Comparação em tempo constante (SequenceEqual para no primeiro byte diferente).
            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }

        // Senha ainda no formato antigo: deve ser regravada depois de um login bem-sucedido.
        public static bool NeedsRehash(byte[] salt) => salt.Length > 0;
    }
}
