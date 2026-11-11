using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize]
public class CalendarioController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public CalendarioController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tareas = await _apiClient.GetTareasCalendarioAsync();
        var usuarios = await _apiClient.GetUsuariosAsync();

        // Permitir asignar tanto a Operadores como a Auditores y Administradores
        ViewBag.UsuariosInternos = usuarios
            .Where(u => u.Rol == RolUsuario.Operador || u.Rol == RolUsuario.Auditor || u.Rol == RolUsuario.Administrador)
            .OrderBy(u => u.Rol)
            .ThenBy(u => u.NombreCompleto)
            .ToList();

        return View(tareas);
    }

    [HttpGet]
    public async Task<IActionResult> GetEventosJson()
    {
        var tareas = await _apiClient.GetTareasCalendarioAsync();
        var eventos = tareas.Select(t =>
        {
            bool esReunion = t.TipoActividad?.Equals("Reunion", StringComparison.OrdinalIgnoreCase) == true;
            bool esCompletada = t.EstadoTarea == "Completada";

            string colorFondo;
            string colorBorde;
            string icono;

            if (esCompletada)
            {
                colorFondo = "#10B981"; // Verde esmeralda
                colorBorde = "#059669";
                icono = "✓ ";
            }
            else if (esReunion)
            {
                colorFondo = "#6366F1"; // Indigo / Violeta para Reuniones
                colorBorde = "#4F46E5";
                icono = "👥 ";
            }
            else
            {
                colorFondo = "#28536B"; // Azul marino corporativo LCP para Tareas
                colorBorde = "#1B3B4D";
                icono = "📌 ";
            }

            return new
            {
                id = t.Id,
                title = $"{icono}{t.Titulo}",
                start = $"{t.Fecha:yyyy-MM-dd}T{t.HoraInicio:HH:mm:ss}",
                end = $"{t.Fecha:yyyy-MM-dd}T{t.HoraFin:HH:mm:ss}",
                backgroundColor = colorFondo,
                borderColor = colorBorde,
                textColor = "#FFFFFF",
                extendedProps = new
                {
                    id = t.Id,
                    titulo = t.Titulo,
                    detalle = t.Detalle,
                    tipo = t.TipoActividad ?? "Tarea",
                    responsable = t.ResponsableNombre,
                    responsableRol = t.ResponsableRol,
                    responsableId = t.ResponsableUsuarioId,
                    estado = t.EstadoTarea,
                    horaInicio = t.HoraInicio.ToString("HH:mm"),
                    horaFin = t.HoraFin.ToString("HH:mm"),
                    fecha = t.Fecha.ToString("yyyy-MM-dd"),
                    fechaFormateada = t.Fecha.ToString("dd/MM/yyyy")
                }
            };
        });

        return Json(eventos);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CargarActividad(CrearTareaCalendarioDto model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Complete todos los campos requeridos de la actividad.";
            return RedirectToAction(nameof(Index));
        }

        var creada = await _apiClient.CrearTareaCalendarioAsync(model);
        if (creada != null)
        {
            string tipoStr = (creada.TipoActividad == "Reunion") ? "Reunión" : "Tarea";
            TempData["Mensaje"] = $"{tipoStr} '{creada.Titulo}' programada con éxito para el {creada.Fecha:dd/MM/yyyy}.";
        }
        else
        {
            TempData["Error"] = "Error al programar la actividad.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompletarActividad(int id)
    {
        var exito = await _apiClient.CompletarTareaCalendarioAsync(id);
        if (exito)
        {
            TempData["Mensaje"] = "Actividad marcada como completada con éxito.";
        }
        else
        {
            TempData["Error"] = "No se pudo actualizar la actividad.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarActividad(int id)
    {
        var exito = await _apiClient.EliminarTareaCalendarioAsync(id);
        if (exito)
        {
            TempData["Mensaje"] = "Actividad eliminada del calendario.";
        }
        else
        {
            TempData["Error"] = "No se pudo eliminar la actividad.";
        }
        return RedirectToAction(nameof(Index));
    }
}
