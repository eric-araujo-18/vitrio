using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.AdminService
{
    public interface IAdminService
    {
        Task<Response<AdminOverviewDto>> GetOverviewAsync();
        Task<Response<List<AdminUserDto>>> SearchUsersAsync(string? search, string? role);
        Task<Response<AdminUserDto>> ConfirmEmailAsync(int userId);
    }
}
