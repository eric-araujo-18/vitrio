using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.SubscriptionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        // GET /api/Subscription/plans -> público (também serve para uma página de preços)
        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlans()
            => Ok(await _subscriptionService.GetPlansAsync());

        // GET /api/Subscription/me -> plano atual e uso do lojista logado
        [HttpGet("me")]
        [Authorize(Roles = "Shopkeeper,Admin")]
        public async Task<IActionResult> GetMine()
            => Ok(await _subscriptionService.GetMySubscriptionAsync(User.GetUserId()));

        // PUT /api/Subscription/admin/users/5/plan  { "planCode": "pro" }
        // Etapa 1: troca manual de plano, só para Admin.
        [HttpPut("admin/users/{userId:int}/plan")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminSetPlan(int userId, [FromBody] AdminSetPlanDto dto)
            => Ok(await _subscriptionService.AdminSetPlanAsync(userId, dto.PlanCode));
    }
}