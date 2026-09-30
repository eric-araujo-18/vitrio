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

        // Loja pública (vitrine): precisa estar ativa e não excluída.
        public static Task<Store?> FindPublicStoreAsync(this AppDbContext context, string slug)
            => context.Store.FirstOrDefaultAsync(s =>
                s.Slug == slug && s.IsActive && s.DeletionDate == null);
    }
}
