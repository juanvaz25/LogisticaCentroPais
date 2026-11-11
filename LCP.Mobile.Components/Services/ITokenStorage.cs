using LCP.Shared.DTOs;

namespace LCP.Mobile.Components.Services;

public interface ITokenStorage
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token, DateTime? expiration = null);
    Task ClearTokenAsync();

    Task<UsuarioDto?> GetUserAsync();
    Task SetUserAsync(UsuarioDto user);

    Task<bool> IsAuthenticatedAsync();
    Task<bool> IsTokenExpiredAsync();
    Task TouchActivityAsync();

    Task<bool> IsIntroShownAsync();
    Task SetIntroShownAsync(bool shown);

    Task<string> GetApiBaseUrlAsync();
    Task SetApiBaseUrlAsync(string url);

    event Action? OnChange;
}
