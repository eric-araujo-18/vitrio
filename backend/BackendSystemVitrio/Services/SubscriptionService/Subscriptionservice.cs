using System.Linq.Expressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.SubscriptionService
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;

        public SubscriptionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Response<List<PlanDto>>> GetPlansAsync()
        {
            try
            {
                var plans = await _context.Plan
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.SortOrder)
                    .Select(ToPlanDto)
                    .ToListAsync();

                return Response<List<PlanDto>>.Ok(plans);
            }
            catch (Exception ex)
            {
                return Response<List<PlanDto>>.Fail($"Erro ao buscar planos: {ex.Message}");
            }
        }

        public async Task<Response<MySubscriptionDto>> GetMySubscriptionAsync(int userId)
        {
            try
            {
                var subscription = await _context.Subscription
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                var plan = await _context.GetEffectivePlanAsync(userId);

                var stores = await _context.Store
                    .Where(s => s.UserId == userId && s.DeletionDate == null)
                    .OrderBy(s => s.CreationDate)
                    .Select(s => new StoreUsageDto
                    {
                        StoreId = s.Id,
                        StoreName = s.Name,
                        ProductCount = _context.Product.Count(p => p.StoreId == s.Id && p.DeletionDate == null),
                    })
                    .ToListAsync();

                return Response<MySubscriptionDto>.Ok(new MySubscriptionDto
                {
                    Plan = ToPlanDto.Compile()(plan),
                    Status = subscription?.Status,
                    TrialEndsAt = subscription?.TrialEndsAt,
                    CurrentPeriodEnd = subscription?.CurrentPeriodEnd,
                    StoreCount = stores.Count,
                    Stores = stores,
                });
            }
            catch (Exception ex)
            {
                return Response<MySubscriptionDto>.Fail($"Erro ao buscar assinatura: {ex.Message}");
            }
        }

        public async Task<Response<MySubscriptionDto>> AdminSetPlanAsync(int userId, string planCode)
        {
            try
            {
                var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId && u.DeletionDate == null);
                if (user is null || user.Role == Role.Client)
                    return Response<MySubscriptionDto>.Fail("Lojista não encontrado.");

                var plan = await _context.Plan.FirstOrDefaultAsync(p => p.Code == planCode);
                if (plan is null)
                    return Response<MySubscriptionDto>.Fail("Plano não encontrado.");

                var subscription = await _context.Subscription.FirstOrDefaultAsync(s => s.UserId == userId);
                if (subscription is null)
                {
                    subscription = new Subscription { UserId = userId, PlanId = plan.Id };
                    _context.Subscription.Add(subscription);
                }

                subscription.PlanId = plan.Id;
                subscription.Status = SubscriptionStatus.Active;
                subscription.CanceledAt = null;
                subscription.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Lojas/produtos acima do novo limite NÃO são apagados:
                // só fica bloqueado criar novos até voltar para dentro do limite.
                return await GetMySubscriptionAsync(userId);
            }
            catch (Exception ex)
            {
                return Response<MySubscriptionDto>.Fail($"Erro ao alterar plano: {ex.Message}");
            }
        }

        private static readonly Expression<Func<Plan, PlanDto>> ToPlanDto = p => new PlanDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            PriceMonthly = p.PriceMonthly,
            MaxStores = p.MaxStores,
            MaxProductsPerStore = p.MaxProductsPerStore,
            AllowsOnlinePayment = p.AllowsOnlinePayment,
        };
    }
}