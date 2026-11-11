using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public SolicitudesController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EstadoSolicitud? filtro)
    {
        var solicitudes = await _apiClient.GetSolicitudesAsync(filtro);
        var choferes = await _apiClient.GetConductoresAsync(estado: EstadoPostulacionAlta.Aprobada);
        var darsenas = await _apiClient.GetDarsenasAsync();

        ViewBag.FiltroActual = filtro;
        ViewBag.Conductores = choferes;
        ViewBag.Darsenas = darsenas;

        return View(solicitudes);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Cliente)]
    public async Task<IActionResult> Nueva(int? darsenaId, int? tipo, int? loteId)
    {
        // Obtener dársenas con contratos activos y stock disponible
        var contratos = await _apiClient.GetContratosAsync();
        var choferes = await _apiClient.GetConductoresAsync(tipo: TipoConductor.PropioCliente, estado: EstadoPostulacionAlta.Aprobada);
        var inventario = await _apiClient.GetInventarioAsync();

        ViewBag.Contratos = contratos.Where(c => c.Estado == EstadoContratoDarsena.Activo).ToList();
        ViewBag.ChoferesPropios = choferes;
        ViewBag.Inventario = inventario;

        var model = new CrearSolicitudDto
        {
            FechaSalidaCliente = DateTime.Now.AddDays(1).Date.AddHours(9),
            FechaHoraTurnoProgramado = DateTime.Now.AddDays(2).Date.AddHours(10),
            TipoOperacion = tipo.HasValue ? (TipoOperacionSolicitud)tipo.Value : TipoOperacionSolicitud.ArriboIngreso,
            DarsenaId = darsenaId ?? 0,
            LoteInventarioId = loteId,
            ModalidadTransporte = ModalidadTransporte.FlotaLCP,
            CiudadOrigen = "Buenos Aires (CABA)",
            CiudadDestino = "Santa Rosa, La Pampa (Predio Central LCP)",
            DistanciaEstimadaKm = 615
        };

        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int cid))
            {
                model.ClienteEmpresaId = cid;
            }
        }

        // Si se especificó un lote de retiro, auto-completar los datos del lote
        if (loteId.HasValue)
        {
            var lote = inventario.FirstOrDefault(l => l.Id == loteId.Value);
            if (lote != null)
            {
                model.DarsenaId = lote.DarsenaId;
                model.TipoOperacion = TipoOperacionSolicitud.DespachoRetiro;
                model.CantidadRetirada = Math.Min(10, lote.CantidadUnidades);
                model.DescripcionMercaderia = $"Retiro de {lote.ProductoDescripcion} (Lote: {lote.CodigoLote})";
                if (lote.CantidadUnidades > 0)
                {
                    model.PesoTotalKg = Math.Round((decimal)model.CantidadRetirada.Value / lote.CantidadUnidades * lote.PesoKg, 2);
                    model.VolumenTotalM3 = Math.Round((decimal)model.CantidadRetirada.Value / lote.CantidadUnidades * lote.VolumenM3, 2);
                }
            }
        }

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Cliente)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(CrearSolicitudDto model)
    {
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int cid))
            {
                model.ClienteEmpresaId = cid;
            }
        }

        var contratos = await _apiClient.GetContratosAsync();
        var contratosActivos = contratos.Where(c => c.Estado == EstadoContratoDarsena.Activo).ToList();

        // Validar capacidad de la dársena para Arribos / Ingresos
        if (model.TipoOperacion == TipoOperacionSolicitud.ArriboIngreso)
        {
            var contratoDarsena = contratosActivos.FirstOrDefault(c => c.DarsenaId == model.DarsenaId);
            if (contratoDarsena != null)
            {
                if (contratoDarsena.EstaLlena)
                {
                    ModelState.AddModelError("", $"La dársena {contratoDarsena.DarsenaCodigo} se encuentra al 100% de su capacidad ({contratoDarsena.CapacidadMaximaM3:N0} m³). No se pueden realizar solicitudes de ingreso hasta despachar mercadería existente.");
                }
                else if (model.VolumenTotalM3 > contratoDarsena.VolumenDisponibleM3)
                {
                    ModelState.AddModelError("VolumenTotalM3", $"El volumen solicitado ({model.VolumenTotalM3:N2} m³) supera la capacidad disponible en la dársena {contratoDarsena.DarsenaCodigo} ({contratoDarsena.VolumenDisponibleM3:N2} m³ disponibles de {contratoDarsena.CapacidadMaximaM3:N0} m³ totales).");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            var choferes = await _apiClient.GetConductoresAsync(tipo: TipoConductor.PropioCliente, estado: EstadoPostulacionAlta.Aprobada);
            var inventario = await _apiClient.GetInventarioAsync();
            ViewBag.Contratos = contratosActivos;
            ViewBag.ChoferesPropios = choferes;
            ViewBag.Inventario = inventario;
            return View(model);
        }

        var creada = await _apiClient.CrearSolicitudAsync(model);
        if (creada != null)
        {
            TempData["Mensaje"] = $"Solicitud {creada.CodigoSolicitud} enviada con éxito. Aguarde la confirmación y asignación de turno oficial por parte del Operador.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", "No se pudo registrar la solicitud. Verifique que tenga contrato activo sobre la dársena y que disponga de capacidad suficiente.");
        ViewBag.Contratos = contratosActivos;
        ViewBag.Inventario = await _apiClient.GetInventarioAsync();
        ViewBag.ChoferesPropios = await _apiClient.GetConductoresAsync(tipo: TipoConductor.PropioCliente, estado: EstadoPostulacionAlta.Aprobada);
        return View(model);
    }

    [HttpGet]
    [ActionName("Detalle")]
    public async Task<IActionResult> Detalle(int id)
    {
        var solicitud = await _apiClient.GetSolicitudAsync(id);
        if (solicitud == null) return NotFound();

        var documentos = await _apiClient.GetDocumentosPorSolicitudAsync(id);
        var choferes = await _apiClient.GetConductoresAsync(estado: EstadoPostulacionAlta.Aprobada);
        var darsenas = await _apiClient.GetDarsenasAsync();

        if (solicitud.HistorialChat == null || !solicitud.HistorialChat.Any())
        {
            solicitud.HistorialChat = await _apiClient.GetHistorialChatAsync($"GRP-SOL-{id}");
        }

        ViewBag.Documentos = documentos;
        ViewBag.Conductores = choferes;
        ViewBag.Darsenas = darsenas;

        return View("Detalle", solicitud);
    }

    [HttpGet]
    [ActionName("Detalles")]
    public async Task<IActionResult> Detalles(int id) => await Detalle(id);

    [HttpPost]
    [Authorize(Roles = "Operador,Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Asignar(AsignarSolicitudDto dto)
    {
        var solicitud = await _apiClient.GetSolicitudAsync(dto.SolicitudId);
        if (solicitud != null && solicitud.TipoOperacion == TipoOperacionSolicitud.ArriboIngreso)
        {
            int darsenaId = dto.DarsenaId ?? solicitud.DarsenaId;
            var darsenas = await _apiClient.GetDarsenasAsync();
            var darsena = darsenas.FirstOrDefault(d => d.Id == darsenaId);
            if (darsena != null)
            {
                if (darsena.EstaLlena)
                {
                    TempData["Error"] = $"No se puede confirmar la solicitud: la dársena {darsena.Codigo} ya se encuentra al 100% de su capacidad ({darsena.CapacidadMaximaM3:N0} m³).";
                    return RedirectToAction(nameof(Detalle), new { id = dto.SolicitudId });
                }
                if (solicitud.VolumenTotalM3 > darsena.VolumenDisponibleM3)
                {
                    TempData["Error"] = $"No se puede confirmar la solicitud: el volumen del arribo ({solicitud.VolumenTotalM3:N2} m³) supera la capacidad disponible en la dársena {darsena.Codigo} ({darsena.VolumenDisponibleM3:N2} m³ disponibles de {darsena.CapacidadMaximaM3:N0} m³ totales).";
                    return RedirectToAction(nameof(Detalle), new { id = dto.SolicitudId });
                }
            }
        }

        var exito = await _apiClient.AsignarSolicitudAsync(dto);
        if (exito)
        {
            TempData["Mensaje"] = "Turno y chofer programados exitosamente. El cliente y conductor han sido notificados.";
        }
        else
        {
            TempData["Error"] = "Error al programar el turno en la solicitud. Verifique que la dársena pertenezca a un contrato activo y disponga de capacidad.";
        }

        return RedirectToAction(nameof(Detalle), new { id = dto.SolicitudId });
    }

    [HttpPost]
    [Authorize(Roles = "Operador,Administrador,Conductor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarOperacion(int id)
    {
        var exito = await _apiClient.CambiarEstadoSolicitudAsync(id, EstadoSolicitud.EnCurso);
        if (exito)
        {
            TempData["Mensaje"] = "La operación ha pasado al estado 'En Curso' (Camión en muelle).";
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [Authorize(Roles = "Operador,Administrador,Cliente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, string? motivo)
    {
        var dto = new CancelarSolicitudDto
        {
            SolicitudId = id,
            Motivo = motivo
        };

        var exito = await _apiClient.CancelarSolicitudAsync(id, dto);
        if (exito)
        {
            if (User.IsInRole(Roles.Cliente))
            {
                TempData["Mensaje"] = "Tu solicitud ha sido cancelada correctamente.";
            }
            else
            {
                TempData["Mensaje"] = "La solicitud u operación ha sido rechazada/cancelada correctamente y la dársena quedó liberada.";
            }
        }
        else
        {
            TempData["Error"] = "No se pudo cancelar o rechazar la solicitud.";
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirDocumento(int solicitudId, IFormFile archivo, string tipoDocumento)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "Seleccione un archivo válido para adjuntar.";
            return RedirectToAction(nameof(Detalle), new { id = solicitudId });
        }

        var doc = await _apiClient.SubirDocumentoAsync(archivo, tipoDocumento, solicitudId: solicitudId);
        if (doc != null)
        {
            TempData["Mensaje"] = $"Documento '{archivo.FileName}' ({tipoDocumento}) adjuntado correctamente.";
        }
        else
        {
            TempData["Error"] = "No se pudo subir el archivo. Intente nuevamente.";
        }

        return RedirectToAction(nameof(Detalle), new { id = solicitudId });
    }
}
