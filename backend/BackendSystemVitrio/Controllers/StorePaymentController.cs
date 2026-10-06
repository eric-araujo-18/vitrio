using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.StorePaymentService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BackendSystemVitrio.Controllers
{
    // Conta do Mercado Pago da loja, para receber os pagamentos online dos pedidos.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Shopkeeper,Admin")]
    public class StorePaymentController : ControllerBase
    {
        private readonly IStorePaymentService _storePayments;

        public StorePaymentController(IStorePaymentService storePayments)
        {
            _storePayments = storePayments;
        }

        // GET /api/StorePayment/{storeId}
        [HttpGet("{storeId:int}")]
        public async Task<IActionResult> GetStatus(int storeId)
            => Ok(await _storePayments.GetStatusAsync(storeId, User.GetUserId()));

        // POST /api/StorePayment/{storeId}/connect -> link do Mercado Pago para o lojista autorizar
        [HttpPost("{storeId:int}/connect")]
        [EnableRateLimiting("subscription-changes")]
        public async Task<IActionResult> Connect(int storeId)
            => Ok(await _storePayments.StartConnectAsync(storeId, User.GetUserId()));

        // DELETE /api/StorePayment/{storeId}
        [HttpDelete("{storeId:int}")]
        public async Task<IActionResult> Disconnect(int storeId)
            => Ok(await _storePayments.DisconnectAsync(storeId, User.GetUserId()));

        // GET /api/StorePayment/oauth/callback?code=...&state=...
        // O Mercado Pago devolve o navegador do lojista para cá (redirect_uri cadastrada no painel
        // do app). Não tem login: quem diz a loja e o lojista é o state assinado.
        [HttpGet("oauth/callback")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
            => Redirect(await _storePayments.CompleteConnectAsync(code, state, error));
    }
}
