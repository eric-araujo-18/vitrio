using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.AuthService
{
    public interface IAuthService
    {
        Task<Response<string>> RegisterAsync(RegisterDto dto);
        Task<Response<string>> RegisterClientAsync(RegisterClientDto dto);
        Task<Response<AuthResultDto>> ValidateCredentialsAsync(LoginDto dto);
        Task<Response<AuthResultDto>> RefreshTokenAsync(string refreshToken);
        Task<Response<string>> RevokeRefreshTokenAsync(string refreshToken);
        Task<User?> GetByIdAsync(int id);
    }
}