using LCP.Shared.DTOs;

namespace LCP.Mobile.Components.Services;

public interface ITokenStorage
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task ClearTokenAsync();

    Task<UsuarioDto?> GetUserAsync();
    Task SetUserAsync(UsuarioDto user);

    Task<string> GetApiBaseUrlAsync();
    Task SetApiBaseUrlAsync(string url);
}
