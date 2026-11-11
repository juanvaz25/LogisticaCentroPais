using System.Security.Claims;

namespace LCP.Api.Services;

public interface IChatAuthorizationService
{
    Task<bool> PuedeAccederAConversacionAsync(string conversacionId, int userId, ClaimsPrincipal user);
}

