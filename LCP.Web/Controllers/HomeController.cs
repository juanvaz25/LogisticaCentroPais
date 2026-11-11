using Microsoft.AspNetCore.Mvc;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public HomeController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var darsenas = await _apiClient.GetDarsenasAsync();
        return View(darsenas);
    }

    [HttpGet]
    public IActionResult PostulacionChofer()
    {
        return View(new PostulacionChoferDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostulacionChofer(PostulacionChoferDto model, IFormFile? archivoLicencia, IFormFile? archivoSeguro)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (archivoLicencia != null)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoLicencia, "LicenciaConducir");
            if (doc != null) model.LicenciaArchivoUrl = doc.RutaUrl;
        }

        if (archivoSeguro != null)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoSeguro, "SeguroCamion");
            if (doc != null) model.SeguroCamionArchivoUrl = doc.RutaUrl;
        }

        var exito = await _apiClient.PostulacionChoferAsync(model);
        if (exito)
        {
            TempData["MensajeExito"] = "¡Tu postulación ha sido recibida con éxito! Te enviamos un correo de verificación. Por favor revisá tu casilla y presioná el botón de confirmación para validar tu email y remitir tu solicitud al Administrador.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", "Ocurrió un error al procesar la postulación. Verifica que el CUIL no esté registrado.");
        return View(model);
    }

    [HttpGet]
    public IActionResult RegistroEmpresa()
    {
        return View(new RegistroEmpresaDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistroEmpresa(RegistroEmpresaDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var exito = await _apiClient.RegistrarEmpresaAsync(model);
        if (exito)
        {
            TempData["MensajeExito"] = "Solicitud de alta registrada con éxito. Hemos enviado un correo de verificación a tu casilla. Es obligatorio que presiones el botón de confirmación en el correo recibido para que tu solicitud sea remitida a la administración y se generen tus credenciales.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", "Ocurrió un error al registrar la empresa. Verifica que el CUIT o Email no existan previamente.");
        return View(model);
    }
}
