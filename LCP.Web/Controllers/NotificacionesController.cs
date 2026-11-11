using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public NotificacionesController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Operaciones()
    {
        var notifs = await _apiClient.GetNotificacionesOperacionesAsync();
        return Json(notifs);
    }

    [HttpGet]
    public async Task<IActionResult> Mensajes()
    {
        var msgs = await _apiClient.GetNotificacionesMensajesAsync();
        return Json(msgs);
    }

    [HttpPost]
    public async Task<IActionResult> MarcarMensajesLeidos(string? conversacionId)
    {
        var ok = await _apiClient.MarcarMensajesLeidosAsync(conversacionId);
        return Json(new { success = ok });
    }
}
