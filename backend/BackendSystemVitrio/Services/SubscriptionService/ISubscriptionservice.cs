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

        // Etapa 1 (antes da cobrança): admin troca o plano de um lojista manualmente
        Task<Response<MySubscriptionDto>> AdminSetPlanAsync(int userId, string planCode);
    }
}