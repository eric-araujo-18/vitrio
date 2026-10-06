using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.Payments
{
    // Criptografa os tokens do Mercado Pago das lojas (AES-GCM) e assina o "state" do OAuth.
    // A chave vem de MercadoPago:TokenEncryptionKey (32 bytes em base64), nunca do appsettings.
    public class PaymentTokenProtector
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly byte[]? _key;

        public PaymentTokenProtector(IOptions<MercadoPagoOptions> options)
        {
            var raw = options.Value.TokenEncryptionKey;
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var key = Convert.FromBase64String(raw.Trim());
            if (key.Length != 32)
                throw new InvalidOperationException("MercadoPago:TokenEncryptionKey precisa ter 32 bytes (em base64).");
            _key = key;
        }

        private byte[] Key => _key ?? throw new InvalidOperationException("MercadoPago:TokenEncryptionKey não configurada.");

        public string Encrypt(string plain)
        {
            var data = Encoding.UTF8.GetBytes(plain);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[data.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(Key, TagSize))
                aes.Encrypt(nonce, data, cipher, tag);

            return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        public string Decrypt(string encrypted)
        {
            var all = Convert.FromBase64String(encrypted);
            var nonce = all.AsSpan(0, NonceSize);
            var tag = all.AsSpan(NonceSize, TagSize);
            var cipher = all.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using (var aes = new AesGcm(Key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }

        // ===== state do OAuth =====
        // O Mercado Pago devolve o navegador para a API sem o login do lojista (o access token
        // fica só na memória do frontend). O state assinado diz qual loja e qual lojista pediram
        // a conexão, e vence em poucos minutos.

        public string CreateState(int storeId, int userId, TimeSpan validFor)
        {
            var expires = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
            var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(12));
            var payload = $"{storeId}:{userId}:{expires}:{nonce}";
            return $"{WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{Sign(payload)}";
        }

        public bool TryReadState(string? state, out int storeId, out int userId)
        {
            storeId = userId = 0;
            if (string.IsNullOrWhiteSpace(state))
                return false;

            var parts = state.Split('.');
            if (parts.Length != 2)
                return false;

            string payload;
            try { payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[0])); }
            catch (FormatException) { return false; }

            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(payload)), Encoding.UTF8.GetBytes(parts[1])))
                return false;

            var fields = payload.Split(':');
            return fields.Length == 4 &&
                   int.TryParse(fields[0], out storeId) &&
                   int.TryParse(fields[1], out userId) &&
                   long.TryParse(fields[2], out var expires) &&
                   DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= expires;
        }

        // Chave separada da usada na criptografia (derivada dela).
        private string Sign(string payload)
        {
            var signingKey = HMACSHA256.HashData(Key, "vitrio-oauth-state"u8);
            return WebEncoders.Base64UrlEncode(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(payload)));
        }
    }
}
