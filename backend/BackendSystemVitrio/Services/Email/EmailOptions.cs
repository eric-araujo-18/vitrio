namespace BackendSystemVitrio.Services.Email
{
    // Seção "Email" da configuração.
    // NUNCA coloque a ResendApiKey no appsettings.json versionado:
    // use "dotnet user-secrets" no desenvolvimento e variáveis de ambiente em produção.
    public class EmailOptions
    {
        public const string Section = "Email";

        // Chave da API do Resend (https://resend.com). Vazia no desenvolvimento: o e-mail
        // não é enviado e o conteúdo (com o link) vai para o log da API.
        public string ResendApiKey { get; set; } = "";

        // Remetente. Precisa ser de um domínio verificado no Resend; para testes o Resend
        // aceita "onboarding@resend.dev" (só entrega para o e-mail da própria conta).
        public string From { get; set; } = "Vitrio <onboarding@resend.dev>";
    }
}
