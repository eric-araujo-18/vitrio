using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        // PUT /api/User/update-profile
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateMyProfile(UpdateUserDto dto)
            => Ok(await _userService.UpdateUserAsync(User.GetUserId(), dto));

        // PUT /api/User/change-password
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
            => Ok(await _userService.ChangePasswordAsync(User.GetUserId(), dto));
    }
}
