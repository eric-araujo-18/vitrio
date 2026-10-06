using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Data
{
    // Loja da vitrine junto com o plano que vale agora para o dono dela.
    public record PublicStore(Store Store, Plan Plan);

    public static class AppDbContextExtensions
    {
        // Devolve ao estoque o que um pedido reservou (pedido cancelado ou pagamento expirado).
        // Chame dentro da mesma transação que muda o status do pedido.
        public static async Task RestoreStockAsync(this AppDbContext context, IEnumerable<OrderItem> items)
        {
            foreach (var item in items.Where(i => i.ProductId.HasValue))
            {
                // Se o tamanho ainda existe, devolve pra ele e soma no total do produto.
                // Se o lojista apagou o tamanho, não há onde devolver: o total do
                // produto também não muda, pra continuar igual à soma dos tamanhos.
                if (item.VariantId.HasValue)
                {
                    var restored = await context.ProductVariant
                        .Where(v => v.Id == item.VariantId.Value)
                        .ExecuteUpdateAsync(set => set.SetProperty(v => v.StockQuantity, v => v.StockQuantity + item.Quantity));

                    if (restored == 0)
                        continue;
                }

                await context.Product
                    .Where(p => p.Id == item.ProductId!.Value)
                    .ExecuteUpdateAsync(set => set.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
            }
        }

        // Busca a loja só se ela pertencer ao usuário e não estiver excluída.
        // Retornar null tanto pra "não existe" quanto pra "não é sua" evita
        // revelar quais IDs de loja existem.
        public static Task<Store?> FindOwnedStoreAsync(this AppDbContext context, int storeId, int userId)
            => context.Store.FirstOrDefaultAsync(s =>
                s.Id == storeId && s.UserId == userId && s.DeletionDate == null);

        // Plano que vale agora para o lojista:
        // - em teste, ativa ou com cobrança atrasada (ainda na tolerância): plano assinado;
        // - cancelada: plano assinado até o fim do período já pago;
        // - sem assinatura ou suspensa: grátis.
        public static async Task<Plan> GetEffectivePlanAsync(this AppDbContext context, int userId)
        {
            var now = DateTime.UtcNow;
            var plan = await context.Subscription
                .Where(s => s.UserId == userId &&
                            (s.Status == SubscriptionStatus.Trial ||
                             s.Status == SubscriptionStatus.Active ||
                             s.Status == SubscriptionStatus.PastDue ||
                             (s.Status == SubscriptionStatus.Canceled && s.CurrentPeriodEnd != null && s.CurrentPeriodEnd > now)))
                .Select(s => s.Plan)
                .FirstOrDefaultAsync();

            return plan ?? await context.Plan.FirstAsync(p => p.Id == Plan.FreeId);
        }

        // ===== Limites do plano =====
        // Os limites valem para o que está ATIVO (loja no ar / produto visível).
        // Quando o plano cai (fim do Profissional, assinatura suspensa...) nada é apagado nem
        // alterado no banco: lojas e produtos ativos além do limite só deixam de aparecer na
        // vitrine. Ficam os mais antigos; para escolher outros, o lojista pausa a loja ou
        // oculta o produto que não quer. Se ele assinar de novo, tudo volta sozinho.
        // Como é calculado na hora, vale também quando o plano "vence" sem nenhum evento
        // (assinatura cancelada que passou de CurrentPeriodEnd).

        // Lojas do usuário que podem ficar no ar com o plano atual.
        public static IQueryable<Store> StoresWithinPlan(this AppDbContext context, int userId, Plan plan)
            => context.Store
                .Where(s => s.UserId == userId && s.IsActive && s.DeletionDate == null)
                .OrderBy(s => s.CreationDate)
                .ThenBy(s => s.Id)
                .Take(plan.MaxStores);

        // Produtos ativos da loja que podem aparecer na vitrine com o plano atual.
        public static IQueryable<Product> ProductsWithinPlan(this AppDbContext context, int storeId, Plan plan)
        {
            var active = context.Product.Where(p => p.StoreId == storeId && p.IsActive && p.DeletionDate == null);
            if (plan.MaxProductsPerStore is not int max)
                return active;

            var allowedIds = active
                .OrderBy(p => p.CreationDate)
                .ThenBy(p => p.Id)
                .Take(max)
                .Select(p => p.Id);

            return active.Where(p => allowedIds.Contains(p.Id));
        }

        // Loja pública (vitrine): precisa estar ativa, não excluída e dentro do limite
        // de lojas do plano do dono.
        public static async Task<PublicStore?> FindPublicStoreAsync(this AppDbContext context, string slug)
        {
            var store = await context.Store.FirstOrDefaultAsync(s =>
                s.Slug == slug && s.IsActive && s.DeletionDate == null);
            if (store is null)
                return null;

            var plan = await context.GetEffectivePlanAsync(store.UserId);
            var withinPlan = await context.StoresWithinPlan(store.UserId, plan).AnyAsync(s => s.Id == store.Id);

            return withinPlan ? new PublicStore(store, plan) : null;
        }
    }
}
