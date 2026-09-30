using BackendSystemVitrio.DTO;
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

        public PublicController(IPublicService publicService, IOrderService orderService)
        {
            _publicService = publicService;
            _orderService = orderService;
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

        // GET /api/Public/stores/{slug}/products?category=roupas&search=camisa
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(string slug, [FromQuery] string? category, [FromQuery] string? search)
            => Ok(await _publicService.GetProductsAsync(slug, category, search));

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
            => Ok(await _orderService.CreatePublicOrderAsync(slug, dto));
    }
}
