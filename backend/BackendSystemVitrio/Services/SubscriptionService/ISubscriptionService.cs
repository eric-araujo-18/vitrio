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
        // force = false: pula se já conferiu há pouco (10s com checkout pendente, 1 min sem).
        // force = true ("Já paguei"): pula só se conferiu há menos de 3s.
        Task<Response<MySubscriptionDto>> SyncMineAsync(int userId, bool force);

        // Verificação leve para as outras telas: se houver checkout pendente, confere no
        // Mercado Pago e devolve só o plano atual e o pendente.
        Task<Response<SubscriptionCheckDto>> CheckPendingAsync(int userId);

        // Admin: troca manual de plano, sem cobrança (cancela a cobrança no Mercado Pago, se houver)
        Task<Response<MySubscriptionDto>> AdminSetPlanAsync(int userId, string planCode);

        // ===== Usados pelo webhook e pelo job de manutenção =====

        // Busca a assinatura no Mercado Pago e atualiza a nossa com o estado real
        Task SyncPreapprovalAsync(string preapprovalId);

        // Uma cobrança mensal foi processada (aprovada ou recusada)
        Task HandleAuthorizedPaymentAsync(string authorizedPaymentId);

        // Suspende quem passou da tolerância e confere checkouts pendentes
        Task RunMaintenanceAsync();

        // Confere os checkouts abertos há pouco (roda a cada minuto, sem depender do webhook)
        Task SyncRecentCheckoutsAsync();
    }
}