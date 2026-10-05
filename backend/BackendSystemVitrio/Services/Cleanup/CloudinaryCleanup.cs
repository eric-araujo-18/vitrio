using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.Cleanup
{
    // Apaga do Cloudinary as imagens que nada no banco usa mais: enviadas num formulário que
    // não foi salvo, trocadas ou removidas de um produto, logo trocado...
    //
    // Conta como "em uso" qualquer URL do banco, inclusive de produtos e lojas excluídos (a
    // exclusão é lógica) e as fotos copiadas para os itens de pedidos antigos.
    // Cuidados, porque apagar no Cloudinary não tem volta:
    // - só olha a pasta configurada (Cloudinary:Folder);
    // - só apaga imagens com mais de 1 dia (dá tempo de o formulário ser salvo);
    // - não apaga nada se o banco não tiver nenhuma imagem (banco vazio ou errado);
    // - apaga no máximo 100 por rodada.
    public class CloudinaryCleanup
    {
        private static readonly TimeSpan MinAge = TimeSpan.FromDays(1);
        private const int MaxDeletesPerRun = 100; // limite de uma chamada de exclusão da API
        private const int MaxListPages = 20;      // 500 por página

        private readonly HttpClient _http;
        private readonly AppDbContext _context;
        private readonly CloudinaryOptions _options;
        private readonly ILogger<CloudinaryCleanup> _logger;

        public CloudinaryCleanup(HttpClient http, AppDbContext context, IOptions<CloudinaryOptions> options, ILogger<CloudinaryCleanup> logger)
        {
            _http = http;
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (_options.CleanupMode == CloudinaryCleanupMode.Off)
                return;

            if (string.IsNullOrWhiteSpace(_options.CloudName) ||
                string.IsNullOrWhiteSpace(_options.ApiKey) ||
                string.IsNullOrWhiteSpace(_options.ApiSecret))
            {
                _logger.LogDebug("Limpeza do Cloudinary não configurada (Cloudinary:CloudName/ApiKey/ApiSecret)");
                return;
            }

            var folder = _options.Folder.Trim().Trim('/');
            if (folder.Length == 0)
            {
                _logger.LogWarning("Limpeza do Cloudinary ignorada: Cloudinary:Folder está vazio (apagaria fora da pasta do Vitrio)");
                return;
            }
            var prefix = folder + "/";

            var urls = await _context.ProductImage.Select(i => i.Url)
                .Concat(_context.Store.Where(s => s.LogoUrl != null).Select(s => s.LogoUrl!))
                .Concat(_context.OrderItem.Where(i => i.ImageUrl != null).Select(i => i.ImageUrl!))
                .ToListAsync(cancellationToken);

            if (urls.Count == 0)
            {
                _logger.LogWarning("Limpeza do Cloudinary ignorada: nenhuma imagem no banco");
                return;
            }

            var inUse = new HashSet<string>(StringComparer.Ordinal);
            foreach (var url in urls)
                AddPossiblePublicIds(url, inUse);

            var cutoff = DateTime.UtcNow - MinAge;
            var orphans = (await ListImagesAsync(prefix, cancellationToken))
                .Where(r => r.PublicId.StartsWith(prefix, StringComparison.Ordinal) &&
                            r.CreatedAt.ToUniversalTime() < cutoff &&
                            !inUse.Contains(r.PublicId))
                .Select(r => r.PublicId)
                .Take(MaxDeletesPerRun)
                .ToList();

            if (orphans.Count == 0)
                return;

            if (_options.CleanupMode == CloudinaryCleanupMode.DryRun)
            {
                _logger.LogInformation(
                    "Limpeza do Cloudinary (simulação, nada foi apagado): {Count} imagens sem uso: {PublicIds}",
                    orphans.Count, string.Join(", ", orphans));
                return;
            }

            await DeleteAsync(orphans, cancellationToken);
            _logger.LogInformation("Limpeza do Cloudinary: {Count} imagens sem uso apagadas: {PublicIds}",
                orphans.Count, string.Join(", ", orphans));
        }

        // Da URL não dá para saber com certeza onde começa o public_id (pode ter versão
        // "v123/" e transformações antes dele). Então guarda todos os finais possíveis do
        // caminho, sem a extensão: ".../upload/v1/vitrio/abc.jpg" -> "abc", "vitrio/abc",
        // "v1/vitrio/abc"... Sobram candidatos demais, nunca de menos: na dúvida, a imagem fica.
        private static void AddPossiblePublicIds(string url, HashSet<string> ids)
        {
            var path = url.Split('?', '#')[0];
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();
            if (segments.Length == 0)
                return;

            var last = segments[^1];
            var dot = last.LastIndexOf('.');
            var lastWithoutExtension = dot > 0 ? last[..dot] : last;

            for (var start = 0; start < segments.Length; start++)
            {
                var head = string.Join('/', segments[start..^1]);
                var prefix = head.Length == 0 ? "" : head + "/";
                ids.Add(prefix + last);
                ids.Add(prefix + lastWithoutExtension);
            }
        }

        private async Task<List<CloudinaryResource>> ListImagesAsync(string prefix, CancellationToken cancellationToken)
        {
            var all = new List<CloudinaryResource>();
            string? cursor = null;

            for (var page = 0; page < MaxListPages; page++)
            {
                var url = $"v1_1/{Uri.EscapeDataString(_options.CloudName)}/resources/image/upload" +
                          $"?prefix={Uri.EscapeDataString(prefix)}&max_results=500" +
                          (cursor is null ? "" : $"&next_cursor={Uri.EscapeDataString(cursor)}");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = BasicAuth();
                using var response = await _http.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<CloudinaryResourceList>(cancellationToken);
                if (result is null)
                    break;

                all.AddRange(result.Resources);
                cursor = result.NextCursor;
                if (string.IsNullOrEmpty(cursor))
                    break;
            }

            return all;
        }

        private async Task DeleteAsync(List<string> publicIds, CancellationToken cancellationToken)
        {
            var query = string.Join('&', publicIds.Select(id => "public_ids[]=" + Uri.EscapeDataString(id)));
            var url = $"v1_1/{Uri.EscapeDataString(_options.CloudName)}/resources/image/upload?{query}";

            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = BasicAuth();
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // A Admin API do Cloudinary usa autenticação básica com a chave e o segredo da API.
        private AuthenticationHeaderValue BasicAuth()
            => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.ApiSecret}")));

        private sealed class CloudinaryResourceList
        {
            [JsonPropertyName("resources")]
            public List<CloudinaryResource> Resources { get; set; } = [];

            [JsonPropertyName("next_cursor")]
            public string? NextCursor { get; set; }
        }

        private sealed class CloudinaryResource
        {
            [JsonPropertyName("public_id")]
            public string PublicId { get; set; } = "";

            [JsonPropertyName("created_at")]
            public DateTime CreatedAt { get; set; }
        }
    }
}
