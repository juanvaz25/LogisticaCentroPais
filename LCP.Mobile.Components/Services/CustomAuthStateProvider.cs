using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using LCP.Shared.DTOs;

namespace LCP.Mobile.Components.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private static readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public CustomAuthStateProvider(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        var user = await _tokenStorage.GetUserAsync();

        if (string.IsNullOrWhiteSpace(token) || user == null)
        {
            return new AuthenticationState(_anonymous);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.NombreCompleto),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Rol.ToString())
        };

        if (user.ClienteId.HasValue)
        {
            claims.Add(new Claim("ClienteId", user.ClienteId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserAuthentication(UsuarioDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.NombreCompleto),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Rol.ToString())
        };

        if (user.ClienteId.HasValue)
        {
            claims.Add(new Claim("ClienteId", user.ClienteId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        var authUser = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(authUser)));
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }
}
