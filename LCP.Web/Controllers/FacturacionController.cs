using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize(Roles = "Auditor,Cliente,Administrador")]
public class FacturacionController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public FacturacionController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EstadoLiquidacion? filtro)
    {
        var liquidaciones = await _apiClient.GetLiquidacionesAsync(filtro);
        var kpis = await _apiClient.GetKpisLiquidacionesAsync();
        var contratos = await _apiClient.GetContratosAsync();

        ViewBag.Kpis = kpis;
        ViewBag.FiltroActual = filtro;
        ViewBag.SolicitudesDarsenasPendientes = contratos.Where(c => c.Estado == EstadoContratoDarsena.PendienteRevision).ToList();
        ViewBag.DarsenasListasParaHabilitar = contratos.Where(c => c.Estado == EstadoContratoDarsena.FacturaCargadaPendienteHabilitacion || (!string.IsNullOrEmpty(c.ComprobantePagoUrl) && c.Estado != EstadoContratoDarsena.Activo && c.Estado != EstadoContratoDarsena.Rechazado)).ToList();
        ViewBag.SolicitudesDarsenasAprobadas = contratos.Where(c => c.Estado == EstadoContratoDarsena.AprobadoPendientePago && string.IsNullOrEmpty(c.ComprobantePagoUrl)).ToList();

        return View(liquidaciones);
    }

    [HttpPost]
    [Authorize(Roles = "Auditor,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HabilitarDarsena(int id)
    {
        var ok = await _apiClient.HabilitarContratoDarsenaAsync(id);
        if (ok)
        {
            TempData["Mensaje"] = "¡Factura verificada y dársena habilitada con éxito! La dársena ha quedado formalmente asignada al cliente para operar y cargar stock.";
        }
        else
        {
            TempData["Error"] = "No se pudo habilitar la dársena.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Auditor,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarSolicitudDarsena(int id, string? observacion)
    {
        var ok = await _apiClient.AprobarContratoDarsenaAsync(id, new AprobarSolicitudDarsenaDto { ObservacionAuditor = observacion });
        if (ok)
        {
            TempData["Mensaje"] = "Solicitud de dársena confirmada por Auditoría. Se generó la liquidación y se habilitó la carga de factura para el cliente.";
        }
        else
        {
            TempData["Error"] = "No se pudo aprobar la solicitud de dársena.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Auditor,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarSolicitudDarsena(int id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "Debe indicar el motivo del rechazo técnico.";
            return RedirectToAction(nameof(Index));
        }

        var ok = await _apiClient.RechazarContratoDarsenaAsync(id, new RechazarSolicitudDarsenaDto { Motivo = motivo });
        if (ok)
        {
            TempData["Mensaje"] = "Solicitud de dársena rechazada con éxito.";
        }
        else
        {
            TempData["Error"] = "No se pudo rechazar la solicitud de dársena.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Auditor,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Auditar(int id, EstadoLiquidacion nuevoEstado, string? observacion)
    {
        var dto = new AuditarLiquidacionDto
        {
            LiquidacionId = id,
            NuevoEstado = nuevoEstado,
            Observacion = observacion
        };

        var (exito, mensaje) = await _apiClient.AuditarLiquidacionAsync(id, dto);
        if (exito)
        {
            TempData["Mensaje"] = mensaje ?? $"Liquidación marcada como {nuevoEstado} exitosamente.";
        }
        else
        {
            TempData["Error"] = mensaje ?? "Error al auditar la liquidación.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirFactura(int liquidacionId, IFormFile archivo, string tipoDocumento)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "Por favor seleccione un archivo para adjuntar a la liquidación.";
            return RedirectToAction(nameof(Index));
        }

        var doc = await _apiClient.SubirDocumentoAsync(archivo, tipoDocumento, liquidacionId: liquidacionId);
        if (doc != null)
        {
            TempData["Mensaje"] = $"Factura/Comprobante '{archivo.FileName}' adjuntado con éxito a la liquidación.";
        }
        else
        {
            TempData["Error"] = "No se pudo subir el archivo. Verifique el formato y tamaño (máx. 10 MB).";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Documentos(int liquidacionId)
    {
        var docs = await _apiClient.GetDocumentosPorLiquidacionAsync(liquidacionId);
        return Json(docs);
    }
}
