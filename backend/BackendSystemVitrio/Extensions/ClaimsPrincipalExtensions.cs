using System.Security.Claims;

namespace BackendSystemVitrio.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        // Substitui o GetUserId() que estava copiado em todos os controllers.
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id)
                ? id
                : throw new UnauthorizedAccessException("Token sem identificação de usuário.");
        }
    }
}
