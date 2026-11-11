using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class AdminController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public AdminController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Tarifas()
    {
        var tarifas = await _apiClient.GetTarifasAsync();
        return View(tarifas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarTarifa(ActualizarTarifaDto model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Valor tarifario inválido.";
            return RedirectToAction(nameof(Tarifas));
        }

        var exito = await _apiClient.ActualizarTarifaAsync(model);
        if (exito)
        {
            TempData["Mensaje"] = "Tarifa actualizada con éxito.";
        }
        else
        {
            TempData["Error"] = "No se pudo actualizar la tarifa.";
        }

        return RedirectToAction(nameof(Tarifas));
    }

    [HttpGet]
    public async Task<IActionResult> Usuarios()
    {
        var usuarios = await _apiClient.GetUsuariosAsync();
        return View(usuarios);
    }

    [HttpGet]
    public IActionResult NuevoPersonal()
    {
        var model = new CrearPersonalDto
        {
            FechaNacimiento = DateTime.Today.AddYears(-26),
            FechaIngreso = DateTime.Today,
            Localidad = "Santa Rosa",
            Provincia = "La Pampa",
            CodigoPostal = "6300",
            EstadoCivil = "Soltero/a",
            ModalidadContratacion = "Tiempo Indeterminado (Efectivo)"
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoPersonal(CrearPersonalDto model, IFormFile? archivoDni, IFormFile? archivoCv)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Subir imagen del documento de identidad si se adjuntó
        if (archivoDni != null && archivoDni.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoDni, "DocumentoIdentidadDNI");
            if (doc != null) model.DocumentoIdentidadUrl = doc.RutaUrl;
        }

        // Subir archivo del CV (Curriculum Vitae) si se adjuntó
        if (archivoCv != null && archivoCv.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoCv, "CurriculumVitaeCV");
            if (doc != null) model.CvArchivoUrl = doc.RutaUrl;
        }

        var (exito, mensaje) = await _apiClient.CrearPersonalAsync(model);
        if (exito)
        {
            TempData["Mensaje"] = $"Personal {model.Nombre} {model.Apellido} incorporado con éxito. Se envió un correo telemático a {model.Email} para la validación obligatoria de su casilla y la recepción de sus credenciales provisorias.";
            return RedirectToAction(nameof(Usuarios));
        }

        ModelState.AddModelError("", mensaje);
        TempData["Error"] = mensaje;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReenviarConfirmacion(int id)
    {
        var (exito, mensaje) = await _apiClient.ReenviarConfirmacionPersonalAsync(id);
        if (exito)
        {
            TempData["Mensaje"] = mensaje;
        }
        else
        {
            TempData["Error"] = mensaje;
        }
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearUsuario(UsuarioDto model)
    {
        var exito = await _apiClient.CrearUsuarioAsync(model);
        if (exito)
        {
            TempData["Mensaje"] = $"Usuario {model.Nombre} {model.Apellido} creado correctamente.";
        }
        else
        {
            TempData["Error"] = "Error al crear el usuario. Verifique que el correo no esté duplicado.";
        }

        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActivo(int id)
    {
        await _apiClient.ToggleActivoUsuarioAsync(id);
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpGet]
    public async Task<IActionResult> Aprobaciones()
    {
        var empresasPendientes = await _apiClient.GetClientesAsync(EstadoPostulacionAlta.PendienteRevision);
        var choferesPendientes = await _apiClient.GetConductoresAsync(estado: EstadoPostulacionAlta.PendienteRevision);

        ViewBag.EmpresasPendientes = empresasPendientes;
        ViewBag.ChoferesPendientes = choferesPendientes;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarEmpresa(int id)
    {
        var exito = await _apiClient.AprobarClienteAsync(id);
        if (exito) TempData["Mensaje"] = "Empresa cliente aprobada y accesos habilitados.";
        return RedirectToAction(nameof(Aprobaciones));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarEmpresa(int id)
    {
        var exito = await _apiClient.RechazarClienteAsync(id);
        if (exito) TempData["Mensaje"] = "Empresa cliente rechazada.";
        return RedirectToAction(nameof(Aprobaciones));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarChofer(int id)
    {
        var exito = await _apiClient.AprobarConductorAsync(id);
        if (exito) TempData["Mensaje"] = "Postulación de chofer aprobada exitosamente.";
        return RedirectToAction(nameof(Aprobaciones));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarChofer(int id)
    {
        var exito = await _apiClient.RechazarConductorAsync(id);
        if (exito) TempData["Mensaje"] = "Postulación rechazada.";
        return RedirectToAction(nameof(Aprobaciones));
    }

    [HttpGet]
    public async Task<IActionResult> FlotaLCP()
    {
        var choferesFlota = await _apiClient.GetConductoresAsync(tipo: TipoConductor.FlotaLCP);
        return View(choferesFlota);
    }

    [HttpGet]
    public IActionResult NuevoChoferFlota()
    {
        var model = new CrearConductorFlotaDto
        {
            FechaNacimiento = DateTime.Now.AddYears(-30).Date,
            Localidad = "Santa Rosa",
            Provincia = "La Pampa",
            CapacidadCargaKg = 15000,
            CapacidadVolumenM3 = 60
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoChoferFlota(CrearConductorFlotaDto model, IFormFile? archivoLicencia, IFormFile? archivoCedula, IFormFile? archivoSeguro)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Subir los documentos respaldatorios de la unidad y el chofer
        if (archivoLicencia != null && archivoLicencia.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoLicencia, "LicenciaConducirLINTI");
            if (doc != null) model.DocumentosIds.Add(doc.Id);
        }

        if (archivoCedula != null && archivoCedula.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoCedula, "CedulaVerdeCamion");
            if (doc != null) model.DocumentosIds.Add(doc.Id);
        }

        if (archivoSeguro != null && archivoSeguro.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoSeguro, "SeguroCamionART");
            if (doc != null) model.DocumentosIds.Add(doc.Id);
        }

        var (exito, mensaje) = await _apiClient.CrearConductorFlotaAsync(model);
        if (exito)
        {
            TempData["Mensaje"] = $"Chofer {model.Nombre} {model.Apellido} incorporado con éxito a la Flota Oficial de LCP (Camión {model.PatenteCamion.ToUpper()}). Cuenta de acceso activada ({model.Email}) con credenciales enviadas por correo electrónico.";
            return RedirectToAction(nameof(FlotaLCP));
        }

        ModelState.AddModelError("", mensaje);
        TempData["Error"] = mensaje;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DocumentosChofer(int id)
    {
        var docs = await _apiClient.GetDocumentosPorConductorAsync(id);
        return Json(docs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirDocumentoChofer(int conductorId, IFormFile archivo, string tipoDocumento)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "Seleccione un archivo válido para adjuntar.";
            return RedirectToAction(nameof(FlotaLCP));
        }

        var doc = await _apiClient.SubirDocumentoAsync(archivo, tipoDocumento, conductorId: conductorId);
        if (doc != null)
        {
            TempData["Mensaje"] = $"Documento '{archivo.FileName}' ({tipoDocumento}) adjuntado correctamente al legajo.";
        }
        else
        {
            TempData["Error"] = "No se pudo subir el archivo.";
        }

        return RedirectToAction(nameof(FlotaLCP));
    }
}
