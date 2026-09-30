using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.CategoryService
{
    public interface ICategoryService
    {
        Task<Response<List<CategoryResponseDto>>> GetCategoriesByStoreAsync(int storeId, int userId);
        Task<Response<CategoryResponseDto>> CreateCategoryAsync(int userId, CreateCategoryDto dto);
        Task<Response<CategoryResponseDto>> UpdateCategoryAsync(int categoryId, int userId, UpdateCategoryDto dto);
        Task<Response<string>> DeleteCategoryAsync(int categoryId, int userId);
    }
}
