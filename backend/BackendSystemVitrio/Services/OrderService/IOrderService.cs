using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.OrderService
{
    public interface IOrderService
    {
        // Painel do lojista
        // Em páginas, do mais novo para o mais antigo. beforeId: continua a partir desse pedido.
        Task<Response<List<OrderResponseDto>>> GetOrdersByStoreAsync(int storeId, int userId, OrderStatus? status, int? beforeId = null);
        Task<Response<OrderResponseDto>> GetOrderByIdAsync(int orderId, int userId);
        Task<Response<OrderResponseDto>> UpdateStatusAsync(int orderId, int userId, OrderStatus status);
        Task<Response<PendingOrdersSummaryDto>> GetPendingSummaryAsync(int storeId, int userId);

        // Vitrine pública (checkout)
        // customerUserId: conta do cliente, se ele estava logado (null = compra sem conta)
        Task<Response<OrderCreatedDto>> CreatePublicOrderAsync(string storeSlug, CreateOrderDto dto, int? customerUserId = null);

        // "Meus pedidos" do cliente logado (opcionalmente só de uma loja)
        Task<Response<List<CustomerOrderDto>>> GetCustomerOrdersAsync(int userId, string? storeSlug);
    }
}