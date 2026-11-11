using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize(Roles = "Operador,Administrador,Auditor")]
public class PatioController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public PatioController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Darsenas()
    {
        var darsenas = await _apiClient.GetDarsenasAsync();
        var solicitudes = await _apiClient.GetSolicitudesAsync();
        var turnosHoy = solicitudes.Where(s => s.FechaHoraTurnoProgramado.Date == DateTime.UtcNow.Date).ToList();

        ViewBag.Solicitudes = solicitudes;
        ViewBag.TurnosHoy = turnosHoy;
        return View(darsenas);
    }

    [HttpPost]
    [Authorize(Roles = "Operador,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, EstadoDarsena nuevoEstado, string? detalle)
    {
        var dto = new ActualizarEstadoDarsenaDto
        {
            DarsenaId = id,
            NuevoEstado = nuevoEstado,
            DetalleOperacion = detalle
        };

        var exito = await _apiClient.ActualizarEstadoDarsenaAsync(id, dto);
        if (exito)
        {
            TempData["Mensaje"] = $"Estado de la dársena #{id} actualizado correctamente a {nuevoEstado}.";
        }
        else
        {
            TempData["Error"] = "No se pudo actualizar el estado de la dársena.";
        }

        return RedirectToAction(nameof(Darsenas));
    }

    [HttpGet]
    [Authorize(Roles = "Operador,Administrador")]
    public async Task<IActionResult> DescargarYOrdenar(int solicitudId)
    {
        var solicitud = await _apiClient.GetSolicitudAsync(solicitudId);
        if (solicitud == null)
        {
            TempData["Error"] = "Solicitud no encontrada.";
            return RedirectToAction(nameof(Darsenas));
        }

        bool esDespacho = solicitud.TipoOperacion == TipoOperacionSolicitud.DespachoRetiro;

        // Validación para Arribo / Ingreso: El camión debe haber reportado su llegada a LCP antes de descargar stock nuevo
        bool camionLlego = (solicitud.EstadoViaje >= EstadoViaje.EnPredioDarsena)
            || (solicitud.ViajeInfo != null && solicitud.ViajeInfo.EstadoViaje >= EstadoViaje.EnPredioDarsena)
            || solicitud.FechaHoraIngresoReal.HasValue
            || solicitud.Estado == EstadoSolicitud.Completada
            || (solicitud.ViajeInfo?.ObservacionesChofer != null && (
                solicitud.ViajeInfo.ObservacionesChofer.Contains("Paso 5") ||
                solicitud.ViajeInfo.ObservacionesChofer.Contains("Paso 3") ||
                solicitud.ViajeInfo.ObservacionesChofer.Contains("Paso 2: Llegada") ||
                solicitud.ViajeInfo.ObservacionesChofer.Contains("Arrib") ||
                solicitud.ViajeInfo.ObservacionesChofer.Contains("Lleg") ||
                solicitud.ViajeInfo.ObservacionesChofer.Contains("posicionado", StringComparison.OrdinalIgnoreCase) ||
                solicitud.ViajeInfo.ObservacionesChofer.Contains("San Martín", StringComparison.OrdinalIgnoreCase)
            ));

        if (!esDespacho && !camionLlego)
        {
            TempData["Error"] = "El camión aún se encuentra en viaje hacia LCP. El chofer debe confirmar su arribo a dársena en la hoja de ruta antes de poder iniciar la descarga y guardado de stock.";
            return RedirectToAction("Detalle", "Solicitudes", new { id = solicitudId });
        }

        var inventarioDarsena = await _apiClient.GetInventarioAsync(solicitud.DarsenaId);
        var lotesCliente = inventarioDarsena.Where(l => l.ClienteEmpresaId == solicitud.ClienteEmpresaId).ToList();

        LoteInventarioDto? loteSeleccionado = null;
        if (solicitud.LoteInventarioId.HasValue)
        {
            loteSeleccionado = lotesCliente.FirstOrDefault(l => l.Id == solicitud.LoteInventarioId.Value);
        }
        if (loteSeleccionado == null && lotesCliente.Any())
        {
            loteSeleccionado = lotesCliente.First();
        }

        int unidadesADescontar = solicitud.CantidadRetirada ?? (loteSeleccionado != null ? Math.Min(loteSeleccionado.CantidadUnidades, 24) : 1);

        var model = new CompletarDescargaDto
        {
            SolicitudId = solicitud.Id,
            EsRetiro = esDespacho,
            LoteInventarioId = loteSeleccionado?.Id ?? solicitud.LoteInventarioId,
            CodigoLote = loteSeleccionado?.CodigoLote ?? solicitud.LoteCodigo ?? $"LOT-{DateTime.UtcNow:yyyyMMdd}-{solicitud.Id}",
            ProductoDescripcion = loteSeleccionado?.ProductoDescripcion ?? solicitud.DescripcionMercaderia,
            CantidadRetirada = unidadesADescontar,
            CantidadUnidades = unidadesADescontar,
            CantidadBultosCajas = loteSeleccionado?.CantidadBultosCajas ?? 2,
            PesoKg = solicitud.PesoTotalKg,
            VolumenM3 = solicitud.VolumenTotalM3,
            UbicacionInternaDarsena = loteSeleccionado?.UbicacionInternaDarsena ?? "Muelle Central / Pasillo de Carga"
        };

        ViewBag.Solicitud = solicitud;
        ViewBag.EsDespacho = esDespacho;
        ViewBag.LotesCliente = lotesCliente;
        ViewBag.LoteSeleccionado = loteSeleccionado;
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Operador,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarDescarga(CompletarDescargaDto model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Solicitud = await _apiClient.GetSolicitudAsync(model.SolicitudId);
            return View("DescargarYOrdenar", model);
        }

        var (exito, mensaje) = await _apiClient.CompletarDescargaAsync(model.SolicitudId, model);
        if (exito)
        {
            TempData["Mensaje"] = mensaje;
            return RedirectToAction(nameof(Darsenas));
        }

        TempData["Error"] = mensaje;
        return RedirectToAction("Detalle", "Solicitudes", new { id = model.SolicitudId });
    }
}
