using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.Services.Cleanup
{
    // Seção "Cloudinary" da configuração. Usada só pela limpeza de imagens órfãs: o upload
    // continua sendo feito pelo frontend (app/api/upload/route.ts).
    // NUNCA coloque ApiKey/ApiSecret no appsettings.json versionado:
    // use "dotnet user-secrets" no desenvolvimento e variáveis de ambiente em produção.
    public class CloudinaryOptions
    {
        public const string Section = "Cloudinary";

        // Os mesmos valores de CLOUDINARY_CLOUD_NAME / _API_KEY / _API_SECRET do frontend.
        // Sem eles, a limpeza não roda.
        public string CloudName { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ApiSecret { get; set; } = "";

        // Pasta onde o frontend sobe as imagens (CLOUDINARY_FOLDER no frontend). Só imagens
        // dessa pasta são apagadas. Se dev e produção usarem a mesma conta do Cloudinary,
        // use pastas diferentes: a limpeza de um ambiente não conhece as imagens do outro.
        public string Folder { get; set; } = "vitrio";

        public CloudinaryCleanupMode CleanupMode { get; set; } = CloudinaryCleanupMode.DryRun;
    }
}
