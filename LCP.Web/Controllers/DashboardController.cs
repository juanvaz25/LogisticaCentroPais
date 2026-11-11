using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public DashboardController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(Roles.Conductor))
        {
            return RedirectToAction("Index", "Conductor");
        }

        var darsenas = await _apiClient.GetDarsenasAsync();
        var tareas = await _apiClient.GetTareasCalendarioAsync(DateTime.UtcNow.ToString("yyyy-MM-dd"));
        var solicitudes = await _apiClient.GetSolicitudesAsync();

        ViewBag.Darsenas = darsenas;
        ViewBag.TareasHoy = tareas;
        ViewBag.Solicitudes = solicitudes;

        if (User.IsInRole(Roles.Auditor) || User.IsInRole(Roles.Administrador))
        {
            ViewBag.KpisLiquidaciones = await _apiClient.GetKpisLiquidacionesAsync();
        }

        return View();
    }
}
