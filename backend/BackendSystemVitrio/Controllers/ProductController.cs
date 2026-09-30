using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.ProductService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Shopkeeper,Admin")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        // GET /api/Product/store/{storeId} -> produtos da loja
        [HttpGet("store/{storeId:int}")]
        public async Task<IActionResult> GetByStore(int storeId)
            => Ok(await _productService.GetProductsByStoreAsync(storeId, User.GetUserId()));

        // GET /api/Product/{id} -> um produto
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _productService.GetProductByIdAsync(id, User.GetUserId()));

        // POST /api/Product
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
            => Ok(await _productService.CreateProductAsync(User.GetUserId(), dto));

        // PUT /api/Product/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
            => Ok(await _productService.UpdateProductAsync(id, User.GetUserId(), dto));

        // DELETE /api/Product/{id} (soft delete)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
            => Ok(await _productService.DeleteProductAsync(id, User.GetUserId()));
    }
}
