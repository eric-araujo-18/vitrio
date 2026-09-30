using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.UserService
{
    public interface IUserService
    {
        Task<Response<UserDto>> UpdateUserAsync(int userId, UpdateUserDto dto);
        Task<Response<string>> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    }
}
