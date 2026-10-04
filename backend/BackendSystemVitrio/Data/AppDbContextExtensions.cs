using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Data
{
    public static class AppDbContextExtensions
    {
        // Busca a loja só se ela pertencer ao usuário e não estiver excluída.
        // Retornar null tanto pra "não existe" quanto pra "não é sua" evita
        // revelar quais IDs de loja existem.
        public static Task<Store?> FindOwnedStoreAsync(this AppDbContext context, int storeId, int userId)
            => context.Store.FirstOrDefaultAsync(s =>
                s.Id == storeId && s.UserId == userId && s.DeletionDate == null);

        // Plano que vale agora para o lojista. Assinatura em teste, ativa ou com
        // cobrança atrasada (ainda na tolerância) usa o plano assinado; sem assinatura,
        // suspensa ou cancelada, volta para o grátis.
        public static async Task<Plan> GetEffectivePlanAsync(this AppDbContext context, int userId)
        {
            var plan = await context.Subscription
                .Where(s => s.UserId == userId &&
                            (s.Status == SubscriptionStatus.Trial ||
                             s.Status == SubscriptionStatus.Active ||
                             s.Status == SubscriptionStatus.PastDue))
                .Select(s => s.Plan)
                .FirstOrDefaultAsync();

            return plan ?? await context.Plan.FirstAsync(p => p.Id == Plan.FreeId);
        }

        // Loja pública (vitrine): precisa estar ativa e não excluída.
        public static Task<Store?> FindPublicStoreAsync(this AppDbContext context, string slug)
            => context.Store.FirstOrDefaultAsync(s =>
                s.Slug == slug && s.IsActive && s.DeletionDate == null);
    }
}