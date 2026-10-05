using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.CustomerService;
using BackendSystemVitrio.Services.OrderService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    // Área do cliente da vitrine. Tudo aqui usa o id do token,
    // então cada usuário só vê e altera os próprios dados.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ICustomerService _customerService;

        public CustomerController(IOrderService orderService, ICustomerService customerService)
        {
            _orderService = orderService;
            _customerService = customerService;
        }

        // GET /api/Customer/orders?storeSlug=minha-loja
        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders([FromQuery] string? storeSlug)
            => Ok(await _orderService.GetCustomerOrdersAsync(User.GetUserId(), storeSlug));

        // ===== Endereços =====

        // GET /api/Customer/addresses
        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses()
            => Ok(await _customerService.GetAddressesAsync(User.GetUserId()));

        // POST /api/Customer/addresses
        [HttpPost("addresses")]
        public async Task<IActionResult> CreateAddress([FromBody] AddressInputDto dto)
            => Ok(await _customerService.CreateAddressAsync(User.GetUserId(), dto));

        // PUT /api/Customer/addresses/5
        [HttpPut("addresses/{id:int}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] AddressInputDto dto)
            => Ok(await _customerService.UpdateAddressAsync(User.GetUserId(), id, dto));

        // DELETE /api/Customer/addresses/5
        [HttpDelete("addresses/{id:int}")]
        public async Task<IActionResult> DeleteAddress(int id)
            => Ok(await _customerService.DeleteAddressAsync(User.GetUserId(), id));
    }
}