using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Extensions;
using BackendSystemVitrio.Services.AuthService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private const string RefreshTokenCookieName = "vitrio_refresh_token";

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var response = await _authService.RegisterAsync(dto);
            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var result = await _authService.ValidateCredentialsAsync(dto);

            if (!result.Status || result.Dados is null)
                return Ok(Response<string>.Fail(result.Mensagem ?? "Não foi possível entrar."));

            SetRefreshTokenCookie(result.Dados.RefreshToken);

            // Só o access token vai no corpo — o refresh token fica exclusivamente no cookie HttpOnly.
            return Ok(Response<string>.Ok(result.Dados.AccessToken, "Login bem-sucedido."));
        }

        // POST /api/Auth/refresh -> chamado automaticamente pelo frontend quando
        // o access token expira. Não precisa de [Authorize]: a credencial aqui
        // é o refresh token do cookie, não o access token (que já expirou).
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshToken = Request.Cookies[RefreshTokenCookieName];
            var result = await _authService.RefreshTokenAsync(refreshToken ?? "");

            if (!result.Status || result.Dados is null)
            {
                DeleteRefreshTokenCookie();
                return Unauthorized(Response<string>.Fail(result.Mensagem ?? "Sessão expirada."));
            }

            SetRefreshTokenCookie(result.Dados.RefreshToken);

            return Ok(Response<string>.Ok(result.Dados.AccessToken, "Sessão renovada."));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies[RefreshTokenCookieName];
            await _authService.RevokeRefreshTokenAsync(refreshToken ?? "");

            DeleteRefreshTokenCookie();

            return Ok(Response<string>.Ok("", "Sessão encerrada."));
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _authService.GetByIdAsync(User.GetUserId());

            if (user is null)
                return NotFound(Response<string>.Fail("Usuário não encontrado."));

            var dto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Cpf = user.Cpf,
                Role = user.Role.ToString()
            };

            return Ok(Response<UserDto>.Ok(dto));
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,       // exige HTTPS — em dev local via http, ver nota abaixo
                SameSite = SameSiteMode.None, // frontend (3000) e backend (5020) são origens diferentes
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/api/Auth"   // cookie só é enviado pras rotas de Auth, reduz exposição
            });
        }

        // Precisa repetir EXATAMENTE as mesmas opções usadas no Append (Path, Secure,
        // SameSite) — o navegador identifica o cookie por nome + domínio + path, então
        // um Delete sem Path bate num cookie "diferente" e o original nunca é removido.
        private void DeleteRefreshTokenCookie()
        {
            Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/api/Auth"
            });
        }
    }
}