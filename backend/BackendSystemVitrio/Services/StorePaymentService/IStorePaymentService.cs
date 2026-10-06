using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.StorePaymentService
{
    // Conta do Mercado Pago da loja (OAuth): conectar, desconectar e usar o token dela.
    public interface IStorePaymentService
    {
        Task<Response<StorePaymentStatusDto>> GetStatusAsync(int storeId, int userId);

        // Devolve o link do Mercado Pago onde o lojista autoriza o Vitrio.
        Task<Response<StorePaymentConnectDto>> StartConnectAsync(int storeId, int userId);

        // Volta do OAuth (o navegador chega sem login): troca o code pelos tokens e devolve
        // a página do frontend para onde redirecionar.
        Task<string> CompleteConnectAsync(string? code, string? state, string? error);

        Task<Response<StorePaymentStatusDto>> DisconnectAsync(int storeId, int userId);

        // Token da loja para chamar o Mercado Pago em nome dela (renova se estiver perto de
        // vencer). null = a loja não tem conta conectada.
        Task<string?> GetAccessTokenAsync(int storeId);

        // Usada pela manutenção: renova os tokens que vencem nos próximos 30 dias.
        Task RefreshExpiringTokensAsync();
    }
}
