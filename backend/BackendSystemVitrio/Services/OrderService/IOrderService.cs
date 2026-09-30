using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.OrderService
{
    public interface IOrderService
    {
        // Painel do lojista
        Task<Response<List<OrderResponseDto>>> GetOrdersByStoreAsync(int storeId, int userId, OrderStatus? status);
        Task<Response<OrderResponseDto>> GetOrderByIdAsync(int orderId, int userId);
        Task<Response<OrderResponseDto>> UpdateStatusAsync(int orderId, int userId, OrderStatus status);

        // Vitrine pública (checkout)
        Task<Response<OrderCreatedDto>> CreatePublicOrderAsync(string storeSlug, CreateOrderDto dto);
    }
}
