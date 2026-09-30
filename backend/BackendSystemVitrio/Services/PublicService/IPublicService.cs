using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.PublicService
{
    // Leitura da vitrine pública — sem autenticação.
    public interface IPublicService
    {
        Task<Response<PublicStoreDto>> GetStoreAsync(string slug);
        Task<Response<List<PublicCategoryDto>>> GetCategoriesAsync(string slug);
        Task<Response<List<PublicProductDto>>> GetProductsAsync(string slug, string? categorySlug, string? search);
        Task<Response<PublicProductDto>> GetProductAsync(string slug, string productSlug);
    }
}
