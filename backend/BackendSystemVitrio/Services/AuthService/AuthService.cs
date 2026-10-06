using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BackendSystemVitrio.Wrappers;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Helpers;

namespace BackendSystemVitrio.Services.AuthService
{
    public class AuthService : IAuthService
    {
        // Validade do link de "esqueci minha senha"
        private const int PasswordResetMinutes = 30;
        // Intervalo mínimo entre dois e-mails de redefinição para a mesma conta
        // (impede usar o formulário para lotar a caixa de alguém).
        private const int PasswordResetCooldownMinutes = 2;
        private static readonly Regex StoreSlugFormat = new("^[a-z0-9-]{1,100}$", RegexOptions.Compiled);

        private readonly AppDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailQueue _emailQueue;

        public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger, EmailQueue emailQueue)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _emailQueue = emailQueue;
        }

        public async Task<Response<string>> RegisterAsync(RegisterDto dto)
        {
            Response<string> response = new Response<string>();

            try
            {
                var normalizedCpf = SlugHelper.OnlyDigits(dto.Cpf);

                if (!ValidationHelper.IsValidCpf(normalizedCpf))
                {
                    response.Dados = null;
                    response.Mensagem = "CPF inválido.";
                    response.Status = false;
                    return response;
                }

                var cpfExists = await _context.User.AnyAsync(u => u.Cpf == normalizedCpf);
                if (cpfExists)
                {
                    response.Dados = null;
                    response.Mensagem = "CPF já cadastrado.";
                    response.Status = false;
                    return response;
                }

                var name = dto.Name?.Trim() ?? "";
                if (name.Length < 2 || name.Length > ValidationHelper.MaxNameLength)
                {
                    response.Dados = null;
                    response.Mensagem = name.Length < 2 ? "Informe seu nome." : $"O nome pode ter no máximo {ValidationHelper.MaxNameLength} caracteres.";
                    response.Status = false;
                    return response;
                }

                string? phone = null;
                if (!string.IsNullOrWhiteSpace(dto.Phone))
                {
                    phone = SlugHelper.OnlyDigits(dto.Phone);
                    if (phone is null || phone.Length < 10 || phone.Length > 11)
                    {
                        response.Dados = null;
                        response.Mensagem = "Telefone inválido. Use DDD + número.";
                        response.Status = false;
                        return response;
                    }
                }

                if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < PasswordHelper.MinLength)
                {
                    response.Dados = null;
                    response.Mensagem = $"A senha precisa ter pelo menos {PasswordHelper.MinLength} caracteres.";
                    response.Status = false;
                    return response;
                }

                var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
                if (!ValidationHelper.IsValidEmail(email))
                {
                    response.Dados = null;
                    response.Mensagem = "Informe um e-mail válido.";
                    response.Status = false;
                    return response;
                }

                var emailExists = await _context.User.AnyAsync(u => u.Email == email);
                if (emailExists)
                {
                    response.Dados = null;
                    response.Mensagem = "Email já cadastrado.";
                    response.Status = false;
                    return response;
                }

                PasswordHelper.CreatePasswordHash(dto.Password, out byte[] hash, out byte[] salt);

                var user = new User
                {
                    Name = name,
                    Email = email,
                    // Cadastro público sempre cria lojista. Admin só direto no banco.
                    Role = Role.Shopkeeper,
                    Phone = phone,
                    Cpf = normalizedCpf,
                    PasswordHash = hash,
                    PasswordSalt = salt
                };

                _context.User.Add(user);
                await _context.SaveChangesAsync();

                response.Dados = "Usuário cadastrado com sucesso.";
                response.Mensagem = null;
                response.Status = true;
            }
            catch (Exception ex)
            {
                response.Dados = null;
                _logger.LogError(ex, "Erro ao cadastrar usuário");
                response.Mensagem = "Erro ao cadastrar usuário. Tente novamente.";
                response.Status = false;
            }

            return response;
        }

        // Cadastro de cliente da vitrine. O papel é sempre Client, decidido aqui
        // (nunca pelo corpo da requisição).
        public async Task<Response<string>> RegisterClientAsync(RegisterClientDto dto)
        {
            try
            {
                var name = dto.Name?.Trim() ?? "";
                if (name.Length < 2)
                    return Response<string>.Fail("Informe seu nome.");
                if (name.Length > ValidationHelper.MaxNameLength)
                    return Response<string>.Fail($"O nome pode ter no máximo {ValidationHelper.MaxNameLength} caracteres.");

                var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
                if (!ValidationHelper.IsValidEmail(email))
                    return Response<string>.Fail("Informe um e-mail válido.");

                if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < PasswordHelper.MinLength)
                    return Response<string>.Fail($"A senha precisa ter pelo menos {PasswordHelper.MinLength} caracteres.");

                string? phone = null;
                if (!string.IsNullOrWhiteSpace(dto.Phone))
                {
                    phone = SlugHelper.OnlyDigits(dto.Phone);
                    if (phone is null || phone.Length < 10 || phone.Length > 11)
                        return Response<string>.Fail("Telefone inválido. Use DDD + número.");
                }

                if (await _context.User.AnyAsync(u => u.Email == email))
                    return Response<string>.Fail("Já existe uma conta com esse e-mail. Tente entrar.");

                PasswordHelper.CreatePasswordHash(dto.Password, out byte[] hash, out byte[] salt);

                _context.User.Add(new User
                {
                    Name = name,
                    Email = email,
                    Phone = phone,
                    Cpf = null,
                    Role = Role.Client,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                });
                await _context.SaveChangesAsync();

                return Response<string>.Ok("Conta criada com sucesso.", "Conta criada com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar conta");
                return Response<string>.Fail("Erro ao criar conta. Tente novamente.");
            }
        }

        public async Task<Response<AuthResultDto>> ValidateCredentialsAsync(LoginDto dto)
        {
            Response<AuthResultDto> response = new Response<AuthResultDto>();

            try
            {
                // Aceita e-mail ou CPF no mesmo campo: com "@" é e-mail, senão é CPF.
                var identifier = (dto.Login ?? dto.Cpf ?? "").Trim();

                if (identifier.Length == 0)
                {
                    response.Dados = null;
                    response.Mensagem = "Informe seu e-mail ou CPF.";
                    response.Status = false;
                    return response;
                }

                User? user;

                if (identifier.Contains('@'))
                {
                    var email = identifier.ToLowerInvariant();
                    user = await _context.User
                        .FirstOrDefaultAsync(u => u.Email == email && u.DeletionDate == null);
                }
                else
                {
                    var cpf = SlugHelper.OnlyDigits(identifier);
                    user = cpf is null
                        ? null
                        : await _context.User.FirstOrDefaultAsync(u => u.Cpf == cpf && u.DeletionDate == null);
                }

                // Mesma mensagem pros dois casos: separar "usuário não encontrado"
                // de "senha incorreta" permite descobrir quais CPFs têm conta.
                if (user is null || !PasswordHelper.VerifyPasswordHash(dto.Password, user.PasswordHash, user.PasswordSalt))
                {
                    response.Dados = null;
                    response.Mensagem = "E-mail/CPF ou senha incorretos.";
                    response.Status = false;
                    return response;
                }

                // Senha no formato antigo: aproveita que ela foi digitada certa e regrava
                // no formato novo (salvo junto com o refresh token logo abaixo).
                if (PasswordHelper.NeedsRehash(user.PasswordSalt))
                {
                    PasswordHelper.CreatePasswordHash(dto.Password, out var newHash, out var newSalt);
                    user.PasswordHash = newHash;
                    user.PasswordSalt = newSalt;
                }

                // Clientes da vitrine agora também entram por aqui. O que separa o que cada
                // um pode fazer é o papel no token: os controllers do painel exigem
                // [Authorize(Roles = "Shopkeeper,Admin")].

                var accessToken = GenerateAccessToken(user);
                var refreshToken = await CreateRefreshTokenAsync(user.Id);

                response.Dados = new AuthResultDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };
                response.Mensagem = "Login bem-sucedido.";
                response.Status = true;
            }
            catch (Exception ex)
            {
                response.Dados = null;
                _logger.LogError(ex, "Erro ao validar credenciais");
                response.Mensagem = "Erro ao validar credenciais. Tente novamente.";
                response.Status = false;
            }

            return response;
        }

        public async Task<Response<AuthResultDto>> RefreshTokenAsync(string refreshToken)
        {
            Response<AuthResultDto> response = new Response<AuthResultDto>();

            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    response.Dados = null;
                    response.Mensagem = "Sessão inválida.";
                    response.Status = false;
                    return response;
                }

                var tokenHash = HashToken(refreshToken);
                var stored = await _context.RefreshToken
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == tokenHash);

                if (stored is null || !stored.IsActive || stored.User is null)
                {
                    response.Dados = null;
                    response.Mensagem = "Sessão expirada. Faça login novamente.";
                    response.Status = false;
                    return response;
                }

                var days = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;

                // Rotação "in-place": atualiza o mesmo registro em vez de criar um novo,
                // evitando acumular linhas revogadas no banco a cada refresh.
                var newToken = GenerateSecureRandomToken();
                stored.Token = HashToken(newToken);
                stored.ExpiresAt = DateTime.UtcNow.AddDays(days);

                var newAccessToken = GenerateAccessToken(stored.User);

                await _context.SaveChangesAsync();

                response.Dados = new AuthResultDto
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newToken
                };
                response.Mensagem = "Sessão renovada.";
                response.Status = true;
            }
            catch (Exception ex)
            {
                response.Dados = null;
                _logger.LogError(ex, "Erro ao renovar sessão");
                response.Mensagem = "Erro ao renovar sessão. Tente novamente.";
                response.Status = false;
            }

            return response;
        }

        public async Task<Response<string>> RevokeRefreshTokenAsync(string refreshToken)
        {
            Response<string> response = new Response<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    response.Dados = null;
                    response.Mensagem = "Nenhuma sessão ativa.";
                    response.Status = true; // idempotente: já "deslogado"
                    return response;
                }

                var tokenHash = HashToken(refreshToken);
                var stored = await _context.RefreshToken
                    .FirstOrDefaultAsync(rt => rt.Token == tokenHash);

                if (stored is not null && stored.RevokedAt is null)
                {
                    stored.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                response.Dados = "Sessão encerrada.";
                response.Mensagem = null;
                response.Status = true;
            }
            catch (Exception ex)
            {
                response.Dados = null;
                _logger.LogError(ex, "Erro ao encerrar sessão");
                response.Mensagem = "Erro ao encerrar sessão. Tente novamente.";
                response.Status = false;
            }

            return response;
        }

        // ===================== Esqueci minha senha =====================

        // Responde sempre a mesma coisa, exista ou não a conta: assim a tela não revela
        // quais e-mails estão cadastrados.
        public async Task<Response<string>> RequestPasswordResetAsync(ForgotPasswordDto dto)
        {
            const string done = "Se existir uma conta com esse e-mail, enviamos um link para criar uma nova senha. " +
                                "Confira também a caixa de spam.";
            try
            {
                var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
                if (!ValidationHelper.IsValidEmail(email))
                    return Response<string>.Fail("Informe um e-mail válido.");

                var user = await _context.User.FirstOrDefaultAsync(u => u.Email == email && u.DeletionDate == null);
                if (user is null)
                    return Response<string>.Ok("", done);

                // Pediu há pouco: não manda outro (o link anterior continua valendo).
                var cooldownStart = DateTime.UtcNow.AddMinutes(-PasswordResetCooldownMinutes);
                if (await _context.PasswordResetToken.AnyAsync(t => t.UserId == user.Id && t.CreationDate > cooldownStart))
                    return Response<string>.Ok("", done);

                // Um link válido por vez: pedir de novo invalida os anteriores.
                await _context.PasswordResetToken.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();

                var token = GenerateSecureRandomToken();
                _context.PasswordResetToken.Add(new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = HashToken(token),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(PasswordResetMinutes),
                });
                await _context.SaveChangesAsync();

                // Envio em segundo plano: a resposta não espera o provedor (falhas vão para o log).
                if (!_emailQueue.TryEnqueue(new EmailMessage(user.Email, "Redefinir sua senha - Vitrio",
                        BuildPasswordResetEmail(user.Name, BuildPasswordResetLink(token, dto.StoreSlug)))))
                    _logger.LogError("Fila de e-mails cheia: link de redefinição para o usuário {UserId} não foi enviado", user.Id);

                return Response<string>.Ok("", done);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar o link de redefinição de senha");
                return Response<string>.Fail("Não foi possível enviar o e-mail agora. Tente novamente em instantes.");
            }
        }

        public async Task<Response<string>> ResetPasswordAsync(ResetPasswordDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Token))
                    return Response<string>.Fail("Link inválido. Peça um novo em \"Esqueci minha senha\".");

                if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < PasswordHelper.MinLength)
                    return Response<string>.Fail($"A nova senha precisa ter pelo menos {PasswordHelper.MinLength} caracteres.");

                var tokenHash = HashToken(dto.Token);
                var reset = await _context.PasswordResetToken
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

                if (reset is null || reset.ExpiresAt < DateTime.UtcNow || reset.User is null || reset.User.DeletionDate is not null)
                    return Response<string>.Fail("Este link expirou ou já foi usado. Peça um novo em \"Esqueci minha senha\".");

                var user = reset.User;
                PasswordHelper.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;

                // O link vale uma vez só.
                _context.PasswordResetToken.Remove(reset);

                // Derruba as sessões abertas: se alguém estava usando a conta, perde o acesso.
                await _context.RefreshToken
                    .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(rt => rt.RevokedAt, (DateTime?)DateTime.UtcNow));

                await _context.SaveChangesAsync();

                return Response<string>.Ok("", "Senha alterada. Agora é só entrar com a nova senha.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao redefinir senha");
                return Response<string>.Fail("Erro ao redefinir senha. Tente novamente.");
            }
        }

        private string BuildPasswordResetLink(string token, string? storeSlug)
        {
            var frontend = (_configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
            var link = $"{frontend}/auth/reset-password?token={Uri.EscapeDataString(token)}";

            // Só aceita um slug com formato válido (ele vai parar dentro de um link no e-mail).
            if (!string.IsNullOrWhiteSpace(storeSlug) && StoreSlugFormat.IsMatch(storeSlug))
                link += $"&store={storeSlug}";

            return link;
        }

        private static string BuildPasswordResetEmail(string name, string link)
        {
            var safeName = WebUtility.HtmlEncode(name);
            var safeLink = WebUtility.HtmlEncode(link);
            return $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#111827">
                  <h2 style="margin-bottom:8px">Redefinir sua senha</h2>
                  <p>Olá, {safeName}.</p>
                  <p>Recebemos um pedido para criar uma nova senha para a sua conta na Vitrio.</p>
                  <p style="margin:28px 0">
                    <a href="{safeLink}" style="background:#2563eb;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:bold">Criar nova senha</a>
                  </p>
                  <p style="color:#6b7280;font-size:14px">O link vale por {PasswordResetMinutes} minutos e só pode ser usado uma vez.
                  Se não foi você que pediu, é só ignorar este e-mail: sua senha continua a mesma.</p>
                </div>
                """;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.User.FirstOrDefaultAsync(u => u.Id == id && u.DeletionDate == null);
        }

        // Curto (padrão: 15min) — se vazar, a janela de abuso é pequena.
        private string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var minutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 15;

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(minutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Longo (padrão: 7 dias) — string opaca aleatória, salva no banco pra
        // poder ser revogada (logout, rotação, etc). Não é um JWT.
        // No banco fica só o hash (HashToken): quem ler a tabela não consegue usar a sessão.
        private async Task<string> CreateRefreshTokenAsync(int userId)
        {
            var days = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;

            var tokenValue = GenerateSecureRandomToken();

            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = HashToken(tokenValue),
                ExpiresAt = DateTime.UtcNow.AddDays(days)
            };

            _context.RefreshToken.Add(refreshToken);
            await _context.SaveChangesAsync();

            return tokenValue;
        }

        // O token tem 512 bits aleatórios, então SHA-256 simples basta (não precisa de PBKDF2).
        private static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string GenerateSecureRandomToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}