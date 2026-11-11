using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using LCP.Api.Services;
using LCP.Shared.DTOs;

namespace LCP.Api.Hubs;

[Authorize]
public class ChatIncidentesHub : Hub
{
    private readonly IChatAuthorizationService _chatAuth;

    public ChatIncidentesHub(IChatAuthorizationService chatAuth)
    {
        _chatAuth = chatAuth;
    }

    public async Task UnirseConversacion(string conversacionId)
    {
        var user = Context.User;
        if (user == null || string.IsNullOrWhiteSpace(conversacionId)) return;

        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int currentUserId)) return;

        bool autorizado = await _chatAuth.PuedeAccederAConversacionAsync(conversacionId, currentUserId, user);
        if (autorizado)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversacionId);
        }
    }

    public async Task SalirConversacion(string conversacionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversacionId);
    }

    public async Task EnviarMensaje(MensajeChatDto mensaje)
    {
        var user = Context.User;
        if (user == null || mensaje == null || string.IsNullOrWhiteSpace(mensaje.ConversacionId)) return;

        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int currentUserId)) return;

        bool autorizado = await _chatAuth.PuedeAccederAConversacionAsync(mensaje.ConversacionId, currentUserId, user);
        if (autorizado)
        {
            await Clients.Group(mensaje.ConversacionId).SendAsync("RecibirMensaje", mensaje);
        }
    }

    public async Task ReportarIncidente(MensajeChatDto incidente)
    {
        var user = Context.User;
        if (user == null || incidente == null || string.IsNullOrWhiteSpace(incidente.ConversacionId)) return;

        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int currentUserId)) return;

        bool autorizado = await _chatAuth.PuedeAccederAConversacionAsync(incidente.ConversacionId, currentUserId, user);
        if (autorizado)
        {
            await Clients.Group(incidente.ConversacionId).SendAsync("RecibirMensaje", incidente);
            await Clients.Group(incidente.ConversacionId).SendAsync("AlertaIncidente", incidente);
        }
    }
}
