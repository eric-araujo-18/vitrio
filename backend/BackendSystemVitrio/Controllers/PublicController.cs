using System.Security.Claims;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Services.OrderPaymentService;
using BackendSystemVitrio.Services.OrderService;
using BackendSystemVitrio.Services.PublicService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BackendSystemVitrio.Controllers
{
    // Rotas da vitrine: qualquer visitante acessa, sem login.
    [ApiController]
    [Route("api/[controller]/stores/{slug}")]
    [AllowAnonymous]
    public class PublicController : ControllerBase
    {
        private readonly IPublicService _publicService;
        private readonly IOrderService _orderService;
        private readonly IOrderPaymentService _orderPayments;

        public PublicController(IPublicService publicService, IOrderService orderService, IOrderPaymentService orderPayments)
        {
            _publicService = publicService;
            _orderService = orderService;
            _orderPayments = orderPayments;
        }

        // GET /api/Public/stores/{slug}
        [HttpGet]
        public async Task<IActionResult> GetStore(string slug)
        {
            var response = await _publicService.GetStoreAsync(slug);
            return response.Status ? Ok(response) : NotFound(response);
        }

        // GET /api/Public/stores/{slug}/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(string slug)
            => Ok(await _publicService.GetCategoriesAsync(slug));

        // GET /api/Public/stores/{slug}/products?category=roupas&search=camisa&page=2
        // Em páginas de 200: a vitrine busca a próxima enquanto a anterior vier cheia.
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(string slug, [FromQuery] string? category, [FromQuery] string? search, [FromQuery] int page = 1)
            => Ok(await _publicService.GetProductsAsync(slug, category, search, page));

        // GET /api/Public/stores/{slug}/products/{productSlug}
        [HttpGet("products/{productSlug}")]
        public async Task<IActionResult> GetProduct(string slug, string productSlug)
        {
            var response = await _publicService.GetProductAsync(slug, productSlug);
            return response.Status ? Ok(response) : NotFound(response);
        }

        // POST /api/Public/stores/{slug}/orders -> checkout
        // Limitado por IP (ver Program.cs) pra dificultar spam de pedidos falsos.
        [HttpPost("orders")]
        [EnableRateLimiting("public-orders")]
        public async Task<IActionResult> CreateOrder(string slug, [FromBody] CreateOrderDto dto)
        {
            // A rota é pública, mas se o cliente mandou um token válido o pedido
            // fica ligado à conta dele (aparece em "Meus pedidos"). Sem token, ou com
            // token inválido, o pedido é feito normalmente, sem conta.
            int? customerUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
            return Ok(await _orderService.CreatePublicOrderAsync(slug, dto, customerUserId));
        }

        // GET /api/Public/stores/{slug}/orders/{code}/payment
        // Situação do pagamento online. A vitrine consulta enquanto o cliente espera a confirmação;
        // se ainda estiver esperando, confere no Mercado Pago (no máximo a cada 5s por pedido).
        [HttpGet("orders/{code}/payment")]
        [EnableRateLimiting("public-payment-status")]
        public async Task<IActionResult> GetOrderPayment(string slug, string code)
            => Ok(await _orderPayments.GetPublicStatusAsync(slug, code));

        // POST /api/Public/stores/{slug}/orders/{code}/cancel
        // O cliente desiste de um pedido que ainda espera pagamento: o estoque volta na hora, em
        // vez de ficar reservado até o prazo. Quem tem o código do pedido é quem o fez.
        [HttpPost("orders/{code}/cancel")]
        [EnableRateLimiting("public-orders")]
        public async Task<IActionResult> CancelUnpaidOrder(string slug, string code)
            => Ok(await _orderPayments.CancelByCustomerAsync(slug, code));

        // GET /api/Public/stores/{slug}/orders/{code}/payment-return
        // Volta do checkout do Mercado Pago (back_url): manda o cliente para a vitrine, que mostra
        // a situação do pagamento. O destino é montado com dados do banco, nunca da URL.
        [HttpGet("orders/{code}/payment-return")]
        public async Task<IActionResult> PaymentReturn(string slug, string code)
            => Redirect(await _orderPayments.GetReturnUrlAsync(slug, code));
    }
}