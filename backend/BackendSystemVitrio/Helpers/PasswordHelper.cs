using System.Security.Cryptography;
using System.Text;

namespace BackendSystemVitrio.Helpers
{
    // Mesmo esquema que já existia no AuthService (HMACSHA512 com salt aleatório),
    // extraído pra cá porque agora o UserService também precisa (troca de senha).
    // Mantido igual pra não invalidar as senhas já cadastradas.
    public static class PasswordHelper
    {
        public static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
        {
            using var hmac = new HMACSHA512();
            salt = hmac.Key;
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        public static bool VerifyPasswordHash(string password, byte[] hash, byte[] salt)
        {
            using var hmac = new HMACSHA512(salt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            // Comparação em tempo constante (SequenceEqual para no primeiro byte diferente).
            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }
    }
}
