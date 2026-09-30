using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.CategoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Shopkeeper,Admin")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // GET /api/Category/store/{storeId}
        // (antes era GET /api/Category/{id}, o que dava a entender que {id} era da categoria)
        [HttpGet("store/{storeId:int}")]
        public async Task<IActionResult> GetByStore(int storeId)
            => Ok(await _categoryService.GetCategoriesByStoreAsync(storeId, User.GetUserId()));

        // POST /api/Category
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
            => Ok(await _categoryService.CreateCategoryAsync(User.GetUserId(), dto));

        // PUT /api/Category/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryDto dto)
            => Ok(await _categoryService.UpdateCategoryAsync(id, User.GetUserId(), dto));

        // DELETE /api/Category/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
            => Ok(await _categoryService.DeleteCategoryAsync(id, User.GetUserId()));
    }
}
