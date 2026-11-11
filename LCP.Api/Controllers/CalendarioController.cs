using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Shared.DTOs;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarioController : ControllerBase
{
    private readonly LcpDbContext _db;

    public CalendarioController(LcpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<TareaCalendarioDto>>> GetTareas([FromQuery] string? fecha, [FromQuery] string? start, [FromQuery] string? end)
    {
        var query = _db.TareasCalendario
            .Include(t => t.ResponsableUsuario)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(fecha) && DateOnly.TryParse(fecha, out var fechaDate))
        {
            query = query.Where(t => t.Fecha == fechaDate);
        }
        else if (!string.IsNullOrWhiteSpace(start) && DateTime.TryParse(start, out var startDt))
        {
            var startDate = DateOnly.FromDateTime(startDt);
            query = query.Where(t => t.Fecha >= startDate);
            if (!string.IsNullOrWhiteSpace(end) && DateTime.TryParse(end, out var endDt))
            {
                var endDate = DateOnly.FromDateTime(endDt);
                query = query.Where(t => t.Fecha <= endDate);
            }
        }

        var tareas = await query.OrderBy(t => t.Fecha).ThenBy(t => t.HoraInicio).ToListAsync();

        return Ok(tareas.Select(t => new TareaCalendarioDto
        {
            Id = t.Id,
            Titulo = t.Titulo,
            Detalle = t.Detalle ?? "",
            Fecha = t.Fecha,
            HoraInicio = t.HoraInicio,
            HoraFin = t.HoraFin,
            ResponsableUsuarioId = t.ResponsableUsuarioId,
            ResponsableNombre = t.ResponsableUsuario?.NombreCompleto ?? "Sin asignar",
            ResponsableRol = t.ResponsableUsuario?.Rol.ToString() ?? "",
            SolicitudOperacionId = t.SolicitudOperacionId,
            EstadoTarea = t.EstadoTarea,
            TipoActividad = string.IsNullOrEmpty(t.TipoActividad) ? "Tarea" : t.TipoActividad
        }).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<TareaCalendarioDto>> CrearTarea([FromBody] CrearTareaCalendarioDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tarea = new TareaCalendario
        {
            Titulo = dto.Titulo,
            Detalle = dto.Detalle ?? "",
            Fecha = dto.Fecha,
            HoraInicio = dto.HoraInicio,
            HoraFin = dto.HoraFin,
            ResponsableUsuarioId = dto.ResponsableUsuarioId,
            SolicitudOperacionId = dto.SolicitudOperacionId,
            EstadoTarea = "Pendiente",
            TipoActividad = string.IsNullOrWhiteSpace(dto.TipoActividad) ? "Tarea" : dto.TipoActividad
        };

        _db.TareasCalendario.Add(tarea);
        await _db.SaveChangesAsync();

        var responsable = await _db.Usuarios.FindAsync(dto.ResponsableUsuarioId);

        return Ok(new TareaCalendarioDto
        {
            Id = tarea.Id,
            Titulo = tarea.Titulo,
            Detalle = tarea.Detalle,
            Fecha = tarea.Fecha,
            HoraInicio = tarea.HoraInicio,
            HoraFin = tarea.HoraFin,
            ResponsableUsuarioId = tarea.ResponsableUsuarioId,
            ResponsableNombre = responsable?.NombreCompleto ?? "Sin asignar",
            ResponsableRol = responsable?.Rol.ToString() ?? "",
            SolicitudOperacionId = tarea.SolicitudOperacionId,
            EstadoTarea = tarea.EstadoTarea,
            TipoActividad = tarea.TipoActividad
        });
    }

    [HttpPut("{id}/completar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> CompletarTarea(int id)
    {
        var tarea = await _db.TareasCalendario.FindAsync(id);
        if (tarea == null) return NotFound();

        tarea.EstadoTarea = "Completada";
        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Actividad '{tarea.Titulo}' marcada como completada." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> EliminarTarea(int id)
    {
        var tarea = await _db.TareasCalendario.FindAsync(id);
        if (tarea == null) return NotFound();

        _db.TareasCalendario.Remove(tarea);
        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = "Actividad eliminada correctamente." });
    }
}
