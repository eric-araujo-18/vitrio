using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.SubscriptionService
{
    public interface ISubscriptionService
    {
        // Planos disponíveis (público: também serve para a página de preços)
        Task<Response<List<PlanDto>>> GetPlansAsync();

        // Plano atual do lojista + uso (lojas e produtos)
        Task<Response<MySubscriptionDto>> GetMySubscriptionAsync(int userId);

        // Assinar, subir ou descer de plano (ou desistir de um downgrade agendado)
        Task<Response<CheckoutResultDto>> StartCheckoutAsync(int userId, string planCode);

        // Cancelar a assinatura paga (o plano vale até o fim do período já pago)
        Task<Response<MySubscriptionDto>> CancelAsync(int userId);

        // Confere a assinatura do lojista no Mercado Pago agora.
        // force = false: pula se já conferiu há menos de 1 minuto (usado ao abrir a página).
        Task<Response<MySubscriptionDto>> SyncMineAsync(int userId, bool force);

        // Admin: troca manual de plano, sem cobrança
        Task<Response<MySubscriptionDto>> AdminSetPlanAsync(int userId, string planCode);

        // ===== Usados pelo webhook e pelo job de manutenção =====

        // Busca a assinatura no Mercado Pago e atualiza a nossa com o estado real
        Task SyncPreapprovalAsync(string preapprovalId);

        // Uma cobrança mensal foi processada (aprovada ou recusada)
        Task HandleAuthorizedPaymentAsync(string authorizedPaymentId);

        // Suspende quem passou da tolerância e confere checkouts pendentes
        Task RunMaintenanceAsync();
    }
}