using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.StoreService
{
    public interface IStoreService
    {
        Task<Response<List<StoreDto>>> GetStoresByUserAsync(int userId);
        Task<Response<StoreDto>> GetByIdAsync(int storeId, int userId);
        Task<Response<StoreDto>> CreateAsync(int userId, CreateStoreDto dto);
        Task<Response<StoreDto>> UpdateAsync(int storeId, int userId, UpdateStoreDto dto);
        Task<Response<string>> DeleteAsync(int storeId, int userId);
        Task<Response<List<StoreDto>>> GoOnlineAsync(int storeId, int userId);
        Task<Response<StoreDashboardDto>> GetDashboardAsync(int storeId, int userId);
    }
}
