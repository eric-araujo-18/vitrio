using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.UserService
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Response<UserDto>> UpdateUserAsync(int userId, UpdateUserDto dto)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null)
                    return Response<UserDto>.Fail("Usuário não encontrado.");

                if (!string.IsNullOrWhiteSpace(dto.Name))
                {
                    var name = dto.Name.Trim();
                    if (name.Length < 2 || name.Length > ValidationHelper.MaxNameLength)
                        return Response<UserDto>.Fail(name.Length < 2
                            ? "Informe seu nome."
                            : $"O nome pode ter no máximo {ValidationHelper.MaxNameLength} caracteres.");
                    user.Name = name;
                }

                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var email = dto.Email.Trim().ToLowerInvariant();

                    if (email != user.Email)
                    {
                        if (!ValidationHelper.IsValidEmail(email))
                            return Response<UserDto>.Fail("E-mail inválido.");

                        // O e-mail é por onde a senha é redefinida: trocar exige a senha atual.
                        if (string.IsNullOrEmpty(dto.CurrentPassword) ||
                            !PasswordHelper.VerifyPasswordHash(dto.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                            return Response<UserDto>.Fail("Para trocar o e-mail, informe sua senha atual corretamente.");

                        // Antes não checava: trocar pra um e-mail já usado estourava
                        // o índice único e voltava uma mensagem de erro do banco.
                        if (await _context.User.AnyAsync(u => u.Email == email && u.Id != userId))
                            return Response<UserDto>.Fail("Este e-mail já está em uso.");

                        user.Email = email;
                    }
                }

                if (!string.IsNullOrWhiteSpace(dto.Phone))
                {
                    var phone = SlugHelper.OnlyDigits(dto.Phone);
                    if (phone is null || phone.Length < 10 || phone.Length > 11)
                        return Response<UserDto>.Fail("Telefone inválido. Use DDD + número.");
                    user.Phone = phone;
                }

                await _context.SaveChangesAsync();

                return Response<UserDto>.Ok(new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    Cpf = user.Cpf,
                    Role = user.Role.ToString()
                }, "Perfil atualizado com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar perfil");
                return Response<UserDto>.Fail("Erro ao atualizar perfil. Tente novamente.");
            }
        }

        public async Task<Response<string>> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null)
                    return Response<string>.Fail("Usuário não encontrado.");

                if (!PasswordHelper.VerifyPasswordHash(dto.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                    return Response<string>.Fail("Senha atual incorreta.");

                if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < PasswordHelper.MinLength)
                    return Response<string>.Fail($"A nova senha precisa ter pelo menos {PasswordHelper.MinLength} caracteres.");

                PasswordHelper.CreatePasswordHash(dto.NewPassword, out var hash, out var salt);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;

                // Derruba as outras sessões: se alguém tinha a senha antiga, perde o acesso.
                await _context.RefreshToken
                    .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(rt => rt.RevokedAt, (DateTime?)DateTime.UtcNow));

                await _context.SaveChangesAsync();

                return Response<string>.Ok("", "Senha alterada. Faça login novamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar senha");
                return Response<string>.Fail("Erro ao alterar senha. Tente novamente.");
            }
        }
    }
}
