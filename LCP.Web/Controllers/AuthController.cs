using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

public class AuthController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public AuthController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginRequestDto());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequestDto model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        var result = await _apiClient.LoginAsync(model);
        if (result == null || !result.Exitoso || result.Usuario == null)
        {
            ModelState.AddModelError("", result?.Mensaje ?? "Error al comunicarse con el servidor central.");
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        await SignInUser(result.Usuario, result.Token, model.Recordarme);

        if (result.Usuario.DebeCambiarPassword)
        {
            return RedirectToAction(nameof(PrimerIngreso));
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("LCP_JWT_TOKEN");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmarEmail(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Exito = false;
            ViewBag.Mensaje = "El enlace de verificación no contiene un código de seguridad válido o está incompleto.";
            return View();
        }

        var result = await _apiClient.ConfirmarEmailAsync(token);
        ViewBag.Exito = result.Exitoso;
        ViewBag.Mensaje = result.Mensaje;
        ViewBag.RazonSocial = result.RazonSocial;
        ViewBag.YaConfirmado = result.YaConfirmado;
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult PrimerIngreso()
    {
        return View(new CambiarPasswordDto());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrimerIngreso(CambiarPasswordDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var exito = await _apiClient.CambiarPasswordAsync(model);
        if (!exito)
        {
            ModelState.AddModelError("", "Error al cambiar la contraseña. Verifique que la contraseña actual sea la provisoria correcta y que la nueva cumpla los requisitos.");
            return View(model);
        }

        // Actualizar el claim en la cookie de sesión para levantar el bloqueo
        var claims = User.Claims.Where(c => c.Type != "DebeCambiarPassword").ToList();
        claims.Add(new Claim("DebeCambiarPassword", "false"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        TempData["MensajeExito"] = "Contraseña definitiva establecida correctamente. Acceso operativo habilitado.";
        return RedirectToAction("Index", "Dashboard");
    }

    private async Task SignInUser(UsuarioDto user, string token, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.NombreCompleto),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RolNombre),
            new("JwtToken", token),
            new("DebeCambiarPassword", user.DebeCambiarPassword ? "true" : "false")
        };

        if (user.ClienteId.HasValue)
        {
            claims.Add(new Claim("ClienteId", user.ClienteId.Value.ToString()));
            if (!string.IsNullOrEmpty(user.EmpresaRazonSocial))
            {
                claims.Add(new Claim("EmpresaRazonSocial", user.EmpresaRazonSocial));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        // Guardar token en cookie segura para que las peticiones JS a SignalR y API puedan leerlo
        Response.Cookies.Append("LCP_JWT_TOKEN", token, new CookieOptions
        {
            HttpOnly = false, // Permite a JS en SignalR leer el token para WebSocket auth
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
