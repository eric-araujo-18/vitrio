using System.Text.RegularExpressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.UserService
{
    public class UserService : IUserService
    {
        private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<UserDto>> UpdateUserAsync(int userId, UpdateUserDto dto)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null)
                    return Response<UserDto>.Fail("Usuário não encontrado.");

                if (!string.IsNullOrWhiteSpace(dto.Name))
                    user.Name = dto.Name.Trim();

                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var email = dto.Email.Trim().ToLowerInvariant();

                    if (!EmailRegex.IsMatch(email))
                        return Response<UserDto>.Fail("E-mail inválido.");

                    // Antes não checava: trocar pra um e-mail já usado estourava
                    // o índice único e voltava uma mensagem de erro do banco.
                    if (email != user.Email && await _context.User.AnyAsync(u => u.Email == email && u.Id != userId))
                        return Response<UserDto>.Fail("Este e-mail já está em uso.");

                    user.Email = email;
                }

                if (!string.IsNullOrWhiteSpace(dto.Phone))
                    user.Phone = dto.Phone.Trim();

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
                return Response<UserDto>.Fail($"Erro ao atualizar perfil: {ex.Message}");
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

                if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                    return Response<string>.Fail("A nova senha precisa ter pelo menos 6 caracteres.");

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
                return Response<string>.Fail($"Erro ao alterar senha: {ex.Message}");
            }
        }
    }
}
