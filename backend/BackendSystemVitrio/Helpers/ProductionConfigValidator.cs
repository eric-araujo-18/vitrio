using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace BackendSystemVitrio.Helpers
{
    // Confere, ao subir fora do desenvolvimento, a configuração que quebraria em silêncio: links de
    // e-mail apontando para localhost, e-mail de teste do Mercado Pago, remetente que não entrega,
    // pagamento online configurado pela metade... Devolve todos os problemas de uma vez, para a
    // mensagem de erro mostrar tudo o que falta (Program.cs não deixa a API subir com algum).
    // Os valores das variáveis nunca aparecem na mensagem, só os nomes (exceto URLs).
    public static class ProductionConfigValidator
    {
        // O HS512 exige pelo menos 64 bytes. Vale em todos os ambientes (ver Program.cs).
        public const int MinJwtKeyBytes = 64;

        public static bool IsValidJwtKey([NotNullWhen(true)] string? key)
            => !string.IsNullOrWhiteSpace(key) && Encoding.UTF8.GetByteCount(key) >= MinJwtKeyBytes;

        public static List<string> Validate(IConfiguration config)
        {
            var problems = new List<string>();

            if (!IsValidJwtKey(config["Jwt:Key"]))
                problems.Add($"Jwt:Key precisa ter pelo menos {MinJwtKeyBytes} caracteres aleatórios (variável Jwt__Key).");

            if (string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
                problems.Add("ConnectionStrings:DefaultConnection não configurada (variável ConnectionStrings__DefaultConnection).");

            // ===== Frontend e CORS =====
            // App:FrontendUrl vai nos links de e-mail (senha, pedido novo), na volta do pagamento e do
            // OAuth do Mercado Pago. Sem ela, tudo isso apontaria para localhost.
            var frontend = PublicHttpsUrl(config["App:FrontendUrl"], "App:FrontendUrl", problems);

            var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length == 0)
                problems.Add("Cors:AllowedOrigins vazio: o navegador não deixaria o frontend chamar a API (variável Cors__AllowedOrigins__0).");
            var validOrigins = origins.Select(o => PublicHttpsUrl(o, "Cors:AllowedOrigins", problems)).OfType<Uri>().ToList();
            if (frontend is not null && validOrigins.Count > 0 &&
                !validOrigins.Any(o => Uri.Compare(o, frontend, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0))
                problems.Add($"Cors:AllowedOrigins precisa incluir a origem do App:FrontendUrl ({frontend.GetLeftPart(UriPartial.Authority)}).");

            // ===== Mercado Pago =====
            var mp = config.GetSection("MercadoPago");
            if (!string.IsNullOrWhiteSpace(mp["TestPayerEmail"]))
                problems.Add("MercadoPago:TestPayerEmail precisa ficar vazio: com ele, toda assinatura é criada para o e-mail de teste e nenhum lojista consegue pagar.");
            if (!string.IsNullOrWhiteSpace(mp["TestOrderPayerEmail"]))
                problems.Add("MercadoPago:TestOrderPayerEmail precisa ficar vazio (é só para o sandbox).");
            if (mp.GetValue<bool>("OAuthTestToken"))
                problems.Add("MercadoPago:OAuthTestToken precisa ser false (true leva ao checkout de testes).");

            if (string.IsNullOrWhiteSpace(mp["AccessToken"]))
            {
                problems.Add("MercadoPago:AccessToken não configurado: os lojistas não conseguiriam assinar os planos.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(mp["WebhookSecret"]))
                    problems.Add("MercadoPago:WebhookSecret não configurado: os avisos de assinatura do Mercado Pago seriam recusados.");
                var backUrl = PublicHttpsUrl(mp["BackUrl"], "MercadoPago:BackUrl", problems);
                // Voltando pela API (/api/Subscription/return), ela precisa saber para onde mandar o lojista.
                if (backUrl is not null && backUrl.AbsolutePath.TrimEnd('/').EndsWith("/api/Subscription/return", StringComparison.OrdinalIgnoreCase))
                    PublicHttpsUrl(mp["FrontendReturnUrl"], "MercadoPago:FrontendReturnUrl", problems);
            }

            // Pagamento online dos pedidos: opcional, mas tudo ou nada. Pela metade, a vitrine só
            // deixaria de oferecer "pagar agora", sem nenhum aviso.
            string[] onlineKeys = ["ClientId", "ClientSecret", "TokenEncryptionKey", "PublicApiUrl"];
            var onlineMissing = onlineKeys.Where(k => string.IsNullOrWhiteSpace(mp[k])).ToList();
            if (onlineMissing.Count > 0 && onlineMissing.Count < onlineKeys.Length)
                problems.Add("Pagamento online dos pedidos configurado pela metade: falta " +
                             string.Join(", ", onlineMissing.Select(k => $"MercadoPago:{k}")) + ".");
            if (onlineMissing.Count == 0)
            {
                PublicHttpsUrl(mp["PublicApiUrl"], "MercadoPago:PublicApiUrl", problems);
                if (!IsAesKey(mp["TokenEncryptionKey"]))
                    problems.Add("MercadoPago:TokenEncryptionKey precisa ter 32 bytes em base64.");
            }

            var fee = mp.GetValue<decimal>("MarketplaceFeePercent");
            if (fee < 0 || fee >= 100)
                problems.Add("MercadoPago:MarketplaceFeePercent precisa estar entre 0 e 100.");

            // ===== E-mail (Resend) =====
            if (string.IsNullOrWhiteSpace(config["Email:ResendApiKey"]))
                problems.Add("Email:ResendApiKey não configurada: a recuperação de senha e os avisos de pedido não seriam enviados.");
            var from = config["Email:From"];
            if (string.IsNullOrWhiteSpace(from) || from.Contains("resend.dev", StringComparison.OrdinalIgnoreCase))
                problems.Add("Email:From precisa ser um endereço de um domínio verificado no Resend (o onboarding@resend.dev só entrega para o dono da conta do Resend).");

            // ===== Cloudinary =====
            // Opcional no backend (sem ele, só a limpeza de imagens sem uso deixa de rodar), mas tudo ou nada.
            string[] cloudinaryKeys = ["CloudName", "ApiKey", "ApiSecret"];
            var cloudinaryMissing = cloudinaryKeys.Where(k => string.IsNullOrWhiteSpace(config[$"Cloudinary:{k}"])).ToList();
            if (cloudinaryMissing.Count > 0 && cloudinaryMissing.Count < cloudinaryKeys.Length)
                problems.Add("Cloudinary configurado pela metade: falta " +
                             string.Join(", ", cloudinaryMissing.Select(k => $"Cloudinary:{k}")) + ".");

            return problems;
        }

        // Endereço absoluto, https e que não seja a própria máquina. Devolve null (e registra o
        // problema) quando não serve.
        private static Uri? PublicHttpsUrl(string? value, string name, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                problems.Add($"{name} não configurado.");
                return null;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                problems.Add($"{name} precisa ser um endereço https (está \"{value}\").");
                return null;
            }

            if (uri.IsLoopback || uri.Host is "0.0.0.0")
            {
                problems.Add($"{name} aponta para a própria máquina (\"{value}\"): use o endereço público.");
                return null;
            }

            return uri;
        }

        private static bool IsAesKey(string? base64)
        {
            try
            {
                return base64 is not null && Convert.FromBase64String(base64.Trim()).Length == 32;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
