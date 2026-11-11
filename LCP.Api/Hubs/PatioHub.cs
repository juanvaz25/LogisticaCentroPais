using Microsoft.AspNetCore.SignalR;
using LCP.Shared.DTOs;

namespace LCP.Api.Hubs;

public class PatioHub : Hub
{
    public async Task UnirseGrupoPatio()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "PatioGeneral");
    }

    public async Task SalirGrupoPatio()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "PatioGeneral");
    }

    // Server-side methods can broadcast to all connected clients
    public async Task NotificarCambioDarsena(DarsenaDto darsena)
    {
        await Clients.Group("PatioGeneral").SendAsync("DarsenaActualizada", darsena);
    }
}
