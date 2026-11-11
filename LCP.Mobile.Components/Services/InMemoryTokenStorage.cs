using LCP.Shared.DTOs;

namespace LCP.Mobile.Components.Services;

public class InMemoryTokenStorage : ITokenStorage
{
    private string? _token;
    private UsuarioDto? _user;
    private string _apiBaseUrl = "http://localhost:5147";

    public event Action? OnChange;

    public Task<string?> GetTokenAsync()
    {
        return Task.FromResult(_token);
    }

    public Task SetTokenAsync(string token)
    {
        _token = token;
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public Task ClearTokenAsync()
    {
        _token = null;
        _user = null;
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public Task<UsuarioDto?> GetUserAsync()
    {
        return Task.FromResult(_user);
    }

    public Task SetUserAsync(UsuarioDto user)
    {
        _user = user;
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public Task<string> GetApiBaseUrlAsync()
    {
        return Task.FromResult(_apiBaseUrl);
    }

    public Task SetApiBaseUrlAsync(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            _apiBaseUrl = url.TrimEnd('/');
            OnChange?.Invoke();
        }
        return Task.CompletedTask;
    }
}
