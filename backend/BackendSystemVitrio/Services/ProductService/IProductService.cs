using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.ProductService
{
    public interface IProductService
    {
        Task<Response<List<ProductResponseDto>>> GetProductsByStoreAsync(int storeId, int userId);
        Task<Response<ProductResponseDto>> GetProductByIdAsync(int productId, int userId);
        Task<Response<ProductResponseDto>> CreateProductAsync(int userId, CreateProductDto dto);
        Task<Response<ProductResponseDto>> UpdateProductAsync(int productId, int userId, UpdateProductDto dto);
        Task<Response<string>> DeleteProductAsync(int productId, int userId);
    }
}
