using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.OrderService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Shopkeeper,Admin")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        // GET /api/Order/store/{storeId}?status=Pending
        [HttpGet("store/{storeId:int}")]
        public async Task<IActionResult> GetByStore(int storeId, [FromQuery] OrderStatus? status)
            => Ok(await _orderService.GetOrdersByStoreAsync(storeId, User.GetUserId(), status));

        // GET /api/Order/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _orderService.GetOrderByIdAsync(id, User.GetUserId()));

        // PUT /api/Order/{id}/status  { "status": "Confirmed" }
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
            => Ok(await _orderService.UpdateStatusAsync(id, User.GetUserId(), dto.Status));
    }
}
