using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.StoreService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Shopkeeper,Admin")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public StoreController(IStoreService storeService)
        {
            _storeService = storeService;
        }

        // GET /api/Store -> todas as lojas do usuário logado
        [HttpGet]
        public async Task<IActionResult> GetMyStores()
            => Ok(await _storeService.GetStoresByUserAsync(User.GetUserId()));

        // GET /api/Store/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _storeService.GetByIdAsync(id, User.GetUserId()));

        // GET /api/Store/{id}/dashboard -> números da tela inicial do painel
        [HttpGet("{id:int}/dashboard")]
        public async Task<IActionResult> GetDashboard(int id)
            => Ok(await _storeService.GetDashboardAsync(id, User.GetUserId()));

        // POST /api/Store
        [HttpPost]
        public async Task<IActionResult> Create(CreateStoreDto dto)
            => Ok(await _storeService.CreateAsync(User.GetUserId(), dto));

        // PUT /api/Store/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, UpdateStoreDto dto)
            => Ok(await _storeService.UpdateAsync(id, User.GetUserId(), dto));

        // DELETE /api/Store/{id} (soft delete)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
            => Ok(await _storeService.DeleteAsync(id, User.GetUserId()));
    }
}
