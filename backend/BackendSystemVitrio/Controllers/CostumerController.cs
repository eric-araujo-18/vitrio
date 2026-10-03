using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.OrderService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    // Área do cliente da vitrine (qualquer usuário logado pode ver os próprios pedidos).
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public CustomerController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        // GET /api/Customer/orders?storeSlug=minha-loja
        // Só devolve pedidos do próprio usuário (filtra pelo id do token).
        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders([FromQuery] string? storeSlug)
            => Ok(await _orderService.GetCustomerOrdersAsync(User.GetUserId(), storeSlug));
    }
}