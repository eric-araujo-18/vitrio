using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.OrderPaymentService
{
    // Pagamento online dos pedidos pelo Mercado Pago, na conta da loja (ver StorePaymentService).
    public interface IOrderPaymentService
    {
        // A vitrine pode oferecer "pagar agora"? (servidor configurado, plano permite, conta conectada)
        Task<bool> IsAvailableAsync(int storeId, Plan plan);

        // Cria a cobrança (preferência do Checkout Pro) de um pedido já salvo e devolve o link.
        Task<string> StartCheckoutAsync(Order order);

        // Cancela um pedido que ainda espera pagamento e devolve o estoque.
        // false = não esperava mais pagamento (acabou de ser pago ou já foi cancelado).
        Task<bool> CancelUnpaidAsync(int orderId);

        // Cancelamento pedido pelo cliente ou pelo lojista: confere no Mercado Pago antes (o
        // pagamento pode ter acabado de ser feito) e só então cancela.
        // true = cancelado; false = não esperava mais pagamento; null = não deu para conferir agora.
        Task<bool?> CancelAwaitingAsync(int orderId, int storeId);

        // Vitrine: o cliente desiste de um pedido que ainda espera pagamento (o estoque volta).
        Task<Response<OrderPaymentInfoDto>> CancelByCustomerAsync(string storeSlug, string code);

        // Estorna o pagamento aprovado de um pedido (o lojista cancelou um pedido pago).
        Task<RefundResult> RefundAsync(Order order);

        // Notificação do Mercado Pago sobre um pagamento de uma loja.
        Task HandleNotificationAsync(int storeId, string paymentId);

        // Vitrine: situação do pagamento (confere no Mercado Pago se ainda estiver esperando).
        Task<Response<OrderPaymentInfoDto>> GetPublicStatusAsync(string storeSlug, string code);

        // Volta do checkout: página da vitrine para onde mandar o cliente.
        Task<string> GetReturnUrlAsync(string storeSlug, string code);

        // Manutenção (a cada minuto): confere os pedidos esperando pagamento e cancela os vencidos.
        Task RunMaintenanceAsync();
    }
}
