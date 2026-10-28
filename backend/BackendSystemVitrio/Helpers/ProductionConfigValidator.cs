using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
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

        // SHA-256 das chaves que já ficaram no histórico do git (appsettings.json dos commits 65776bd
        // a 8692398). O repositório é público, então qualquer um pode montar tokens com elas: a API
        // recusa essas chaves em qualquer ambiente. Só o hash fica aqui, nunca a chave.
        private static readonly HashSet<string> LeakedJwtKeyHashes = new(StringComparer.OrdinalIgnoreCase)
        {
            "85450135f52156510901728efb8eead9723af26dbaa761a6290caf0c4f011a7b",
            "6aea189afac416594f0e9be53ca71865a5c0f70e42a79f14e757a04d766903db",
        };

        public static bool IsValidJwtKey([NotNullWhen(true)] string? key) => JwtKeyProblem(key) is null;

        // Null quando a chave serve; senão, o motivo (sem mostrar a chave).
        public static string? JwtKeyProblem(string? key)
        {
            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinJwtKeyBytes)
                return $"Jwt:Key precisa ter pelo menos {MinJwtKeyBytes} caracteres aleatórios.";
            if (LeakedJwtKeyHashes.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))))
                return "Jwt:Key é uma chave que já foi publicada no histórico do repositório. Gere uma nova (openssl rand -base64 48).";
            return null;
        }

        public static List<string> Validate(IConfiguration config)
        {
            var problems = new List<string>();

            if (JwtKeyProblem(config["Jwt:Key"]) is { } jwtProblem)
                problems.Add(jwtProblem + " (variável Jwt__Key)");

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

            // ===== Captcha do checkout (Turnstile) =====
            // Opcional, mas tudo ou nada: só com a chave pública a vitrine mostraria um captcha que a
            // API não confere; só com a secreta, nada pediria o captcha.
            var turnstileSite = !string.IsNullOrWhiteSpace(config["Turnstile:SiteKey"]);
            var turnstileSecret = !string.IsNullOrWhiteSpace(config["Turnstile:SecretKey"]);
            if (turnstileSite != turnstileSecret)
                problems.Add($"Turnstile configurado pela metade: falta Turnstile:{(turnstileSite ? "SecretKey" : "SiteKey")}.");

            // ===== Monitoramento (Sentry) =====
            var sentryDsn = config["Sentry:Dsn"];
            if (!string.IsNullOrWhiteSpace(sentryDsn) &&
                (!Uri.TryCreate(sentryDsn.Trim(), UriKind.Absolute, out var dsn) || dsn.Scheme != Uri.UriSchemeHttps))
                problems.Add("Sentry:Dsn precisa ser o endereço https que o Sentry mostra no projeto (ou fique vazia).");

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
