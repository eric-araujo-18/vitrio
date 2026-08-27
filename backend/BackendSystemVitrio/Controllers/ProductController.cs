using System.Security.Claims;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Services.ProductService;
using BackendSystemVitrio.Wrappers;
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

        // GET /api/Product/{id} -> produtos da loja com Id = id
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProductsByStore(int id)
        {
            var response = await _productService.GetProductsByStore(id, GetUserId());
            return Ok(response);
        }

        // POST /api/Product -> cria um produto na loja do usuário logado
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
        {
            var response = await _productService.CreateProduct(GetUserId(), dto);
            return Ok(response);
        }

        private int GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(claim!);
        }
    }
}