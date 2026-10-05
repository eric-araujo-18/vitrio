using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.SubscriptionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using BackendSystemVitrio.Services.Payments;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly MercadoPagoOptions _mercadoPagoOptions;

        public SubscriptionController(ISubscriptionService subscriptionService, IOptions<MercadoPagoOptions> mercadoPagoOptions)
        {
            _subscriptionService = subscriptionService;
            _mercadoPagoOptions = mercadoPagoOptions.Value;
        }

        // GET /api/Subscription/return?preapproval_id=...
        // O Mercado Pago não aceita "localhost" como back_url. No desenvolvimento, o back_url
        // aponta para cá (pelo túnel da API) e este endpoint só redireciona o navegador para
        // a página de Assinatura do front, repassando os parâmetros.
        // O destino vem da configuração (nunca da URL), então não vira um "open redirect".
        [HttpGet("return")]
        [AllowAnonymous]
        public IActionResult ReturnFromCheckout()
        {
            var target = _mercadoPagoOptions.FrontendReturnUrl;
            if (string.IsNullOrWhiteSpace(target))
                return NotFound();

            return Redirect(target + Request.QueryString.Value);
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

        // GET /api/Subscription/check -> verificação leve usada pelas outras telas do painel
        [HttpGet("check")]
        [Authorize(Roles = "Shopkeeper,Admin")]
        public async Task<IActionResult> Check()
            => Ok(await _subscriptionService.CheckPendingAsync(User.GetUserId()));

        // POST /api/Subscription/checkout  { "planCode": "basic" }
        // Executa a ação que o backend permite para esse plano (ver DecideAction):
        // devolve o link do Mercado Pago, ou null quando não precisa pagar agora.
        [HttpPost("checkout")]
        [Authorize(Roles = "Shopkeeper,Admin")]
        [EnableRateLimiting("subscription-changes")]
        public async Task<IActionResult> Checkout([FromBody] StartCheckoutDto dto)
            => Ok(await _subscriptionService.StartCheckoutAsync(User.GetUserId(), dto.PlanCode));

        // POST /api/Subscription/sync?force=true
        // Confere a assinatura no Mercado Pago. A página chama ao abrir (force=false, com
        // limite de 1 vez por minuto) e o botão "Já paguei" chama com force=true.
        [HttpPost("sync")]
        [Authorize(Roles = "Shopkeeper,Admin")]
        public async Task<IActionResult> Sync([FromQuery] bool force = false)
            => Ok(await _subscriptionService.SyncMineAsync(User.GetUserId(), force));

        // POST /api/Subscription/cancel
        [HttpPost("cancel")]
        [Authorize(Roles = "Shopkeeper,Admin")]
        [EnableRateLimiting("subscription-changes")]
        public async Task<IActionResult> Cancel()
            => Ok(await _subscriptionService.CancelAsync(User.GetUserId()));

        // PUT /api/Subscription/admin/users/5/plan  { "planCode": "pro" }
        // Etapa 1: troca manual de plano, só para Admin.
        [HttpPut("admin/users/{userId:int}/plan")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminSetPlan(int userId, [FromBody] AdminSetPlanDto dto)
            => Ok(await _subscriptionService.AdminSetPlanAsync(userId, dto.PlanCode));
    }
}