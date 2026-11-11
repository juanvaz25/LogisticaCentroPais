using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize(Roles = "Conductor,Operador,Administrador")]
public class ConductorController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public ConductorController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var solicitudes = await _apiClient.GetSolicitudesAsync();

        ViewBag.ViajesActivos = solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Programada || s.Estado == EstadoSolicitud.EnCurso)
            .OrderBy(s => s.FechaHoraTurnoProgramado)
            .ToList();

        ViewBag.ViajesHistorial = solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Completada || s.Estado == EstadoSolicitud.Cancelada)
            .OrderByDescending(s => s.Id)
            .ToList();

        return View(solicitudes);
    }

    [HttpGet]
    public async Task<IActionResult> HojaDeRuta(int id)
    {
        var solicitud = await _apiClient.GetSolicitudAsync(id);
        if (solicitud == null)
        {
            TempData["Error"] = "La orden de transporte u operación no fue encontrada.";
            return RedirectToAction(nameof(Index));
        }

        return View(solicitud);
    }

    [HttpPost]
    [Authorize(Roles = "Conductor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarHito(int solicitudId, EstadoViaje estadoViaje, string? hito, string? observaciones, double? latitud, double? longitud)
    {
        var dto = new ActualizarViajeRequestDto
        {
            EstadoViaje = estadoViaje,
            HitoAlcanzado = hito,
            Observaciones = observaciones,
            Latitud = latitud,
            Longitud = longitud
        };

        var exito = await _apiClient.ActualizarViajeAsync(solicitudId, dto);
        if (exito)
        {
            TempData["Mensaje"] = $"Estado de hoja de ruta actualizado: {hito ?? estadoViaje.ToString()}";
        }
        else
        {
            TempData["Error"] = "No se pudo actualizar el estado de la hoja de ruta.";
        }

        return RedirectToAction(nameof(HojaDeRuta), new { id = solicitudId });
    }

    [HttpPost]
    [Authorize(Roles = "Conductor")]
    [Route("Conductor/ActualizarViajeAjax/{id}")]
    public async Task<IActionResult> ActualizarViajeAjax(int id, [FromBody] ActualizarViajeRequestDto dto)
    {
        if (dto == null) return BadRequest();
        var exito = await _apiClient.ActualizarViajeAsync(id, dto);
        return Json(new { exitoso = exito });
    }

    [HttpPost]
    [Authorize(Roles = "Conductor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportarIncidente(int solicitudId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "Debe especificar el motivo del incidente o demora.";
            return RedirectToAction(nameof(HojaDeRuta), new { id = solicitudId });
        }

        var dto = new ActualizarViajeRequestDto
        {
            EstadoViaje = EstadoViaje.ConIncidenteDemora,
            HitoAlcanzado = "🚨 Demora / Incidente en ruta reportado por Conductor",
            Observaciones = motivo
        };

        var exito = await _apiClient.ActualizarViajeAsync(solicitudId, dto);
        if (exito)
        {
            TempData["Mensaje"] = "Alerta de incidente notificada en tiempo real al operador y al cliente.";
        }
        else
        {
            TempData["Error"] = "Ocurrió un error al registrar el incidente.";
        }

        return RedirectToAction(nameof(HojaDeRuta), new { id = solicitudId });
    }
}
