using BackendSystemVitrio.Services.AdminService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    // Painel do Admin (/menu/admin no frontend). A troca de plano é
    // PUT /api/Subscription/admin/users/{id}/plan.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        // GET /api/Admin/overview
        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
            => Ok(await _adminService.GetOverviewAsync());

        // GET /api/Admin/users?search=maria&role=Shopkeeper
        [HttpGet("users")]
        public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] string? role)
            => Ok(await _adminService.SearchUsersAsync(search, role));

        // POST /api/Admin/users/5/confirm-email
        [HttpPost("users/{userId:int}/confirm-email")]
        public async Task<IActionResult> ConfirmEmail(int userId)
            => Ok(await _adminService.ConfirmEmailAsync(userId));
    }
}
