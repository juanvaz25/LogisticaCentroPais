using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly Services.IEmailService _emailService;

    public ClientesController(LcpDbContext db, Services.IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Operador,Auditor")]
    public async Task<ActionResult> GetClientes([FromQuery] EstadoPostulacionAlta? estado)
    {
        var query = _db.ClientesEmpresa.AsQueryable();

        if (estado.HasValue)
        {
            query = query.Where(c => c.EstadoAlta == estado.Value);
        }

        var list = await query.OrderByDescending(c => c.Id).ToListAsync();

        return Ok(list.Select(c => new
        {
            c.Id,
            c.RazonSocial,
            c.CUIT,
            c.Rubro,
            c.Domicilio,
            c.Localidad,
            c.Provincia,
            c.EmailContacto,
            c.TelefonoContacto,
            c.EstadoAlta,
            EstadoNombre = c.EstadoAlta.ToString(),
            c.EmailConfirmado,
            c.FechaConfirmacionEmail,
            c.FechaSolicitud
        }));
    }

    [HttpPut("{id}/aprobar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> AprobarEmpresa(int id)
    {
        var empresa = await _db.ClientesEmpresa.FindAsync(id);
        if (empresa == null) return NotFound();

        empresa.EstadoAlta = EstadoPostulacionAlta.Aprobada;

        // Activar y generar credenciales de acceso provisorias para los usuarios asociados a esta empresa
        var usuarios = await _db.Usuarios.Where(u => u.ClienteId == id).ToListAsync();
        foreach (var u in usuarios)
        {
            var passwordProvisoria = DbInitializer.GenerarPasswordProvisoria();
            u.PasswordHash = DbInitializer.HashPassword(passwordProvisoria);
            u.Activo = true;
            u.DebeCambiarPassword = true;

            // Enviar credenciales autogeneradas al correo registrado para su primer ingreso
            _ = _emailService.SendCredencialesAutoGeneradasAsync(u.Email, empresa.RazonSocial, passwordProvisoria, "Cliente / Empresa");
        }

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Empresa {empresa.RazonSocial} aprobada y credenciales de acceso enviadas por correo electrónico." });
    }

    [HttpPut("{id}/rechazar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> RechazarEmpresa(int id)
    {
        var empresa = await _db.ClientesEmpresa.FindAsync(id);
        if (empresa == null) return NotFound();

        empresa.EstadoAlta = EstadoPostulacionAlta.Rechazada;

        // Eliminar cuentas de usuario provisionales asociadas a la empresa rechazada
        var usuarios = await _db.Usuarios.Where(u => u.ClienteId == id).ToListAsync();
        if (usuarios.Any())
        {
            _db.Usuarios.RemoveRange(usuarios);
        }

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Empresa {empresa.RazonSocial} rechazada." });
    }
}
