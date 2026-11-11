using LCP.Shared.DTOs;

namespace LCP.Mobile.Components.Services;

public class InMemoryTokenStorage : ITokenStorage
{
    private string? _token;
    private UsuarioDto? _user;
    private DateTime? _expiration;
    private DateTime? _lastActivity;
    private bool _introShown = false;
    private string _apiBaseUrl = "http://localhost:5147";

    public event Action? OnChange;

    public async Task<bool> IsTokenExpiredAsync()
    {
        if (string.IsNullOrWhiteSpace(_token)) return true;

        var now = DateTime.UtcNow;

        // Regla: 30 minutos de vigencia total o 30 minutos de inactividad
        if (_expiration.HasValue && now > _expiration.Value)
        {
            await ClearTokenAsync();
            return true;
        }

        if (_lastActivity.HasValue && (now - _lastActivity.Value) > TimeSpan.FromMinutes(30))
        {
            await ClearTokenAsync();
            return true;
        }

        return false;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (string.IsNullOrWhiteSpace(_token) || _user == null)
        {
            return false;
        }

        if (await IsTokenExpiredAsync())
        {
            return false;
        }

        return true;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (await IsTokenExpiredAsync())
        {
            return null;
        }
        await TouchActivityAsync();
        return _token;
    }

    public Task SetTokenAsync(string token, DateTime? expiration = null)
    {
        _token = token;
        _lastActivity = DateTime.UtcNow;
        _expiration = expiration ?? DateTime.UtcNow.AddMinutes(30);
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public Task ClearTokenAsync()
    {
        _token = null;
        _user = null;
        _expiration = null;
        _lastActivity = null;
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public async Task<UsuarioDto?> GetUserAsync()
    {
        if (await IsTokenExpiredAsync())
        {
            return null;
        }
        await TouchActivityAsync();
        return _user;
    }

    public Task SetUserAsync(UsuarioDto user)
    {
        _user = user;
        _lastActivity = DateTime.UtcNow;
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public Task TouchActivityAsync()
    {
        if (!string.IsNullOrWhiteSpace(_token))
        {
            _lastActivity = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsIntroShownAsync()
    {
        return Task.FromResult(_introShown);
    }

    public Task SetIntroShownAsync(bool shown)
    {
        _introShown = shown;
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
