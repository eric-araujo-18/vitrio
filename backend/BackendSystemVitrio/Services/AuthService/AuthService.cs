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
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
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

                if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                {
                    response.Dados = null;
                    response.Mensagem = "A senha precisa ter pelo menos 6 caracteres.";
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
                response.Mensagem = "Erro ao cadastrar usuário: " + ex.Message;
                response.Status = false;
            }

            return response;
        }

        public async Task<Response<AuthResultDto>> ValidateCredentialsAsync(LoginDto dto)
        {
            Response<AuthResultDto> response = new Response<AuthResultDto>();

            try
            {
                var cpf = SlugHelper.OnlyDigits(dto.Cpf);

                if (cpf is null)
                {
                    response.Dados = null;
                    response.Mensagem = "CPF inválido.";
                    response.Status = false;
                    return response;
                }

                var user = await _context.User
                    .FirstOrDefaultAsync(u => u.Cpf == cpf && u.DeletionDate == null);

                // Mesma mensagem pros dois casos: separar "usuário não encontrado"
                // de "senha incorreta" permite descobrir quais CPFs têm conta.
                if (user is null || !PasswordHelper.VerifyPasswordHash(dto.Password, user.PasswordHash, user.PasswordSalt))
                {
                    response.Dados = null;
                    response.Mensagem = "CPF ou senha incorretos.";
                    response.Status = false;
                    return response;
                }

                if (user.Role == Role.Client)
                {
                    response.Dados = null;
                    response.Mensagem = "Este acesso é exclusivo para lojistas e administradores.";
                    response.Status = false;
                    return response;
                }

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
                response.Mensagem = "Erro ao validar credenciais: " + ex.Message;
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

                var stored = await _context.RefreshToken
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

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
                stored.Token = GenerateSecureRandomToken();
                stored.ExpiresAt = DateTime.UtcNow.AddDays(days);

                var newAccessToken = GenerateAccessToken(stored.User);

                await _context.SaveChangesAsync();

                response.Dados = new AuthResultDto
                {
                    AccessToken = newAccessToken,
                    RefreshToken = stored.Token
                };
                response.Mensagem = "Sessão renovada.";
                response.Status = true;
            }
            catch (Exception ex)
            {
                response.Dados = null;
                response.Mensagem = "Erro ao renovar sessão: " + ex.Message;
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

                var stored = await _context.RefreshToken
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

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
                response.Mensagem = "Erro ao encerrar sessão: " + ex.Message;
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
        private async Task<string> CreateRefreshTokenAsync(int userId)
        {
            var days = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;

            var tokenValue = GenerateSecureRandomToken();

            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAt = DateTime.UtcNow.AddDays(days)
            };

            _context.RefreshToken.Add(refreshToken);
            await _context.SaveChangesAsync();

            return tokenValue;
        }

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
