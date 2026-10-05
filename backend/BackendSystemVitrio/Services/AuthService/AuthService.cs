using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BackendSystemVitrio.Wrappers;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Helpers;

namespace BackendSystemVitrio.Services.AuthService
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<Response<string>> RegisterAsync(RegisterDto dto)
        {
            Response<string> response = new Response<string>();

            try
            {
                var normalizedCpf = SlugHelper.OnlyDigits(dto.Cpf);

                if (normalizedCpf is null || normalizedCpf.Length != 11)
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

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    response.Dados = null;
                    response.Mensagem = "Informe seu nome.";
                    response.Status = false;
                    return response;
                }

                if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < PasswordHelper.MinLength)
                {
                    response.Dados = null;
                    response.Mensagem = $"A senha precisa ter pelo menos {PasswordHelper.MinLength} caracteres.";
                    response.Status = false;
                    return response;
                }

                var email = dto.Email.Trim().ToLowerInvariant();

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
                    Name = dto.Name.Trim(),
                    Email = email,
                    // Cadastro público sempre cria lojista. Admin só direto no banco.
                    Role = Role.Shopkeeper,
                    Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
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

                var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
                if (!IsValidEmail(email))
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

        private static bool IsValidEmail(string email)
        {
            if (email.Length is < 5 or > 254 || !email.Contains('@'))
                return false;
            try
            {
                return new System.Net.Mail.MailAddress(email).Address == email;
            }
            catch
            {
                return false;
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