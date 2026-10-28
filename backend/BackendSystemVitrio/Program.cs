using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Middlewares;
using BackendSystemVitrio.Services.AdminService;
using BackendSystemVitrio.Services.AuthService;
using BackendSystemVitrio.Services.Captcha;
using BackendSystemVitrio.Services.CategoryService;
using BackendSystemVitrio.Services.Cleanup;
using BackendSystemVitrio.Services.OrderPaymentService;
using BackendSystemVitrio.Services.OrderService;
using BackendSystemVitrio.Services.StorePaymentService;
using BackendSystemVitrio.Services.ProductService;
using BackendSystemVitrio.Services.PublicService;
using BackendSystemVitrio.Services.CustomerService;
using BackendSystemVitrio.Services.Email;
using BackendSystemVitrio.Services.Health;
using BackendSystemVitrio.Services.SubscriptionService;
using BackendSystemVitrio.Services.Payments;
using BackendSystemVitrio.Services.StoreService;
using BackendSystemVitrio.Services.UserService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ===== Configuração obrigatória =====
// Fora do desenvolvimento, a API não sobe com configuração que quebraria em silêncio (links de
// e-mail para localhost, e-mail de teste do Mercado Pago, remetente que não entrega...): a
// mensagem lista tudo o que falta (ver ProductionConfigValidator). No desenvolvimento, os valores
// padrão ficam no appsettings.Development.json e os segredos nos user-secrets. O ambiente "Testing" é o
// dos testes automatizados (backend/BackendSystemVitrio.Tests), que montam a própria configuração.
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    var problems = ProductionConfigValidator.Validate(builder.Configuration);
    if (problems.Count > 0)
        throw new InvalidOperationException(
            "A configuração de produção está incompleta. Corrija as variáveis de ambiente:" +
            string.Concat(problems.Select(p => "\n - " + p)));
}

// Chave que assina os tokens de login. Sem ela, ou com uma chave conhecida (inclusive as que já
// passaram pelo git), qualquer pessoa montaria um token válido (até de Admin). Por isso a API nem
// sobe sem uma chave de verdade, em nenhum ambiente.
var jwtKey = builder.Configuration["Jwt:Key"];
if (!ProductionConfigValidator.IsValidJwtKey(jwtKey))
    throw new InvalidOperationException(
        ProductionConfigValidator.JwtKeyProblem(jwtKey) + " No desenvolvimento: " +
        "\"dotnet user-secrets set Jwt:Key <chave>\"; em produção, a variável de ambiente Jwt__Key.");

// ===== Monitoramento de erros (Sentry, opcional) =====
// Com Sentry:Dsn, os erros registrados com LogError (e as exceções não tratadas) vão para o
// Sentry, que avisa por e-mail. Sem ela, os erros ficam só no log da hospedagem.
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(options =>
    {
        options.Dsn = sentryDsn;
        options.Environment = builder.Environment.EnvironmentName;
        // Sem dados pessoais (IP, cookies, usuário) nos eventos.
        options.SendDefaultPii = false;
        options.TracesSampleRate = 0;
    });
}

builder.Services
    .AddControllers()
    // Enums trafegam como texto ("Pending", "Shopkeeper") — mais legível no
    // frontend. Números continuam sendo aceitos na entrada.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BackendSystemVitrio API",
        Version = "v1",
        Description = "API do sistema Vitrio"
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole aqui o token retornado pelo login. Não precisa digitar \"Bearer \" antes."
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<NewOrderNotifier>();
builder.Services.AddScoped<IPublicService, PublicService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<EmailConfirmation>();

// ===== Captcha do checkout (Cloudflare Turnstile, opcional) =====
// Com Turnstile:SiteKey e Turnstile:SecretKey, a vitrine mostra o captcha e a API confere a
// resposta antes de criar o pedido. Sem eles, o checkout não pede captcha.
builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection(TurnstileOptions.Section));
builder.Services.AddHttpClient<TurnstileVerifier>(client =>
{
    client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// GET /health: a API está no ar e o banco responde (ver DatabaseHealthCheck).
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// Compressão só nas rotas públicas da vitrine (ver mais abaixo): a lista de produtos de uma
// loja grande passa de 1 MB sem compressão.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

// ===== Mercado Pago =====
// Credenciais vêm de "dotnet user-secrets" (desenvolvimento) ou variáveis de ambiente (produção).
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.Section));
builder.Services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHostedService<SubscriptionMaintenanceService>();

// Pagamento online dos pedidos: cada loja conecta a própria conta do Mercado Pago (OAuth)
// e a cobrança é criada em nome dela (o dinheiro cai na conta da loja).
builder.Services.AddSingleton<PaymentTokenProtector>();
builder.Services.AddHttpClient<IMercadoPagoMarketplaceClient, MercadoPagoMarketplaceClient>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<IStorePaymentService, StorePaymentService>();
builder.Services.AddScoped<IOrderPaymentService, OrderPaymentService>();
builder.Services.AddHostedService<OrderPaymentMaintenanceService>();

// ===== E-mail (Resend) =====
// Sem Email:ResendApiKey, no desenvolvimento o e-mail vai para o log em vez de ser enviado.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
// Os e-mails vão para uma fila e saem em segundo plano (a requisição não espera o provedor).
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailBackgroundSender>();

// ===== Limpeza periódica (registros vencidos e imagens sem uso no Cloudinary) =====
// Sem Cloudinary:CloudName/ApiKey/ApiSecret, só a limpeza do banco roda.
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(CloudinaryOptions.Section));
builder.Services.AddScoped<DatabaseCleanup>();
builder.Services.AddHttpClient<CloudinaryCleanup>(client =>
{
    client.BaseAddress = new Uri("https://api.cloudinary.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<CleanupService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        // Padrão é 5 min de tolerância — com access token de 15 min isso é muito.
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

const string CorsPolicy = "FrontendPolicy";

// Origens vêm da configuração (Cors:AllowedOrigins): localhost:3000 no appsettings.Development.json,
// o domínio do frontend em produção (Cors__AllowedOrigins__0). Fallback: localhost:3000.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // cookie HttpOnly do refresh token
    });
});

// ===== Proxy reverso (nginx, Cloudflare Tunnel, load balancer...) =====
// Atrás de um proxy, a conexão chega do IP do proxy. Sem ler X-Forwarded-For, o rate limit
// "por IP" viraria um limite único para todos os clientes, e sem X-Forwarded-Proto a API acharia
// que a requisição é HTTP e redirecionaria para HTTPS sem necessidade.
// Só os cabeçalhos vindos de proxies confiáveis são aceitos (senão qualquer um mandaria um
// X-Forwarded-For falso para fugir do rate limit). Localhost já é confiável por padrão. Na
// hospedagem (seção ReverseProxy):
// - KnownProxies: IPs fixos do proxy (nginx em outra máquina, por exemplo);
// - KnownNetworks: faixas de IP do proxy, em CIDR ("10.0.0.0/8"), quando o IP muda;
// - TrustAll: aceita o cabeçalho de qualquer origem. Só para hospedagens em que a API não pode
//   ser acessada sem passar pelo proxy delas (Render, Railway, Fly.io, Azure App Service...);
// - ForwardLimit: quantos proxies seguidos (Cloudflare na frente do proxy da hospedagem = 2).
// Se chegar X-Forwarded-For que a API não aceita, ela avisa no log (ver mais abaixo).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var proxy = builder.Configuration.GetSection("ReverseProxy");
    options.ForwardLimit = proxy.GetValue("ForwardLimit", 1);

    if (proxy.GetValue<bool>("TrustAll"))
    {
        // Sem nenhum proxy ou rede conhecida, o middleware aceita qualquer origem.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        return;
    }

    foreach (var ip in proxy.GetSection("KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(ip));
    foreach (var network in proxy.GetSection("KnownNetworks").Get<string[]>() ?? [])
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});

// Limite de pedidos públicos: 10 por minuto por IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Login, cadastros (lojista e cliente) e senha: até 10 tentativas por minuto por IP.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
            }));

    // Assinar / trocar / cancelar plano: até 6 por 10 minutos por lojista.
    // Evita criar assinaturas em série no Mercado Pago (cliques repetidos ou abuso).
    options.AddPolicy("subscription-changes", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));

    // Conferir a assinatura no Mercado Pago: até 30 por minuto por lojista. A página de
    // assinatura chama a cada 5s logo depois do checkout, bem abaixo disso; o limite só
    // barra chamadas em série (cada uma pode virar uma consulta ao Mercado Pago).
    options.AddPolicy("subscription-sync", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Vitrine conferindo o pagamento de um pedido (a cada 4-5s enquanto o cliente espera).
    options.AddPolicy("public-payment-status", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Avisos de pagamento dos pedidos: cada um vira uma consulta ao Mercado Pago com o token
    // da loja, então um IP mandando avisos em série é barrado.
    options.AddPolicy("payment-notifications", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("public-orders", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Aplica as migrations pendentes ao subir, para não depender de rodar
// "dotnet ef database update" à mão a cada deploy.
if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Primeiro de todos: o resto do pipeline (rate limit, HTTPS, logs) precisa do IP e do esquema reais.
app.UseForwardedHeaders();

// Quando o X-Forwarded-For é aceito, o middleware acima o consome. Se ele continua aqui, veio de
// um proxy que a API não aceita (ou de mais proxies seguidos do que o ForwardLimit). Atrás do
// proxy da hospedagem, isso faria todo mundo dividir o mesmo limite de tentativas, porque o IP
// visto seria o do proxy. Avisa uma vez no log, com o que configurar.
var proxyWarningLogged = 0;
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("X-Forwarded-For") && Interlocked.Exchange(ref proxyWarningLogged, 1) == 0)
        app.Logger.LogWarning(
            "Chegou X-Forwarded-For de um proxy que a API não aceita ({RemoteIp}). O limite de tentativas por IP " +
            "está usando o IP do proxy, então todos os usuários dividem o mesmo limite. Configure " +
            "ReverseProxy:KnownNetworks (faixa de IPs do proxy), ReverseProxy:TrustAll (só se a API não puder ser " +
            "acessada sem passar pelo proxy) ou ReverseProxy:ForwardLimit (mais de um proxy no caminho).",
            context.Connection.RemoteIpAddress);
    await next();
});

// Cabeçalhos de segurança em todas as respostas: a API só devolve JSON e redirecionamentos, então
// nada dela pode ser embutido em outro site, interpretado como outro tipo de arquivo ou carregar
// scripts. (O Swagger, só no desenvolvimento, precisa de scripts: lá não vai CSP.)
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (!app.Environment.IsDevelopment())
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        return Task.CompletedTask;
    });
    await next();
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Compressão (gzip/brotli) só na vitrine pública. Essas respostas não têm segredo nenhum; nas
// rotas com token ou dados da conta, a compressão sobre HTTPS abriria espaço para ataques que
// adivinham o conteúdo pelo tamanho (BREACH).
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/Public"), branch => branch.UseResponseCompression());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Diz ao navegador para usar sempre HTTPS neste domínio.
    app.UseHsts();

    // Só fora do desenvolvimento: em dev o frontend chama http://localhost:5020, e com o perfil
    // "https" (necessário para o túnel do Mercado Pago) o redirecionamento 307 para a porta
    // HTTPS quebra o CORS e o navegador mostra "não foi possível conectar".
    // O /health fica de fora: a hospedagem costuma consultá-lo por HTTP, direto no contêiner.
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());
}

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Para os testes de integração (WebApplicationFactory<Program>).
public partial class Program;
