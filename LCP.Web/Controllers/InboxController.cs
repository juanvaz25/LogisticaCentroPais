using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.DTOs;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize]
public class InboxController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public InboxController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? conv)
    {
        var conversaciones = await _apiClient.GetConversacionesAsync();
        var contactos = await _apiClient.GetContactosChatAsync();
        var solicitudes = await _apiClient.GetSolicitudesAsync();

        string convActiva = "";
        if (!string.IsNullOrEmpty(conv) && conversaciones.Any(c => c.ConversacionId.Equals(conv, StringComparison.OrdinalIgnoreCase)))
        {
            convActiva = conv;
        }
        else
        {
            convActiva = conversaciones.FirstOrDefault()?.ConversacionId ?? "";
        }

        var historial = !string.IsNullOrEmpty(convActiva) 
            ? await _apiClient.GetHistorialChatAsync(convActiva) 
            : new List<MensajeChatDto>();

        ViewBag.Conversaciones = conversaciones;
        ViewBag.Contactos = contactos;
        ViewBag.Solicitudes = solicitudes;
        ViewBag.ConversacionActivaId = convActiva;
        ViewBag.ConvActiva = conversaciones.FirstOrDefault(c => c.ConversacionId == convActiva);

        return View(historial);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enviar(EnviarMensajeDto model)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index), new { conv = model.ConversacionId });
        }

        await _apiClient.EnviarMensajeChatAsync(model);
        return RedirectToAction(nameof(Index), new { conv = model.ConversacionId });
    }

    [HttpPost]
    public async Task<IActionResult> EnviarAjax([FromBody] EnviarMensajeDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var creado = await _apiClient.EnviarMensajeChatAsync(model);
        if (creado != null)
        {
            return Ok(creado);
        }

        return StatusCode(500, new { Mensaje = "No se pudo entregar el mensaje." });
    }

    [HttpGet]
    public async Task<IActionResult> ConversacionesAjax()
    {
        var conversaciones = await _apiClient.GetConversacionesAsync();
        return Json(conversaciones);
    }

    [HttpGet]
    public async Task<IActionResult> HistorialAjax(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Json(new List<MensajeChatDto>());
        var historial = await _apiClient.GetHistorialChatAsync(id);
        return Json(historial);
    }
}
