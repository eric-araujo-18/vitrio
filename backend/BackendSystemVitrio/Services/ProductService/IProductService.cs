using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.ProductService
{
    public interface IProductService
    {
        Task<Response<List<ProductResponseDto>>> GetProductsByStore(int storeId, int userId);
        Task<Response<ProductResponseDto>> CreateProduct(int userId, CreateProductDto dto);
    }
}