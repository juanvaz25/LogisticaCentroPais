using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConductoresController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly Services.IEmailService _emailService;

    public ConductoresController(LcpDbContext db, Services.IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<ActionResult> GetConductores([FromQuery] EstadoPostulacionAlta? estado, [FromQuery] TipoConductor? tipo)
    {
        var query = _db.Conductores
            .Include(c => c.ClienteEmpresa)
            .Include(c => c.Usuario)
            .AsQueryable();

        // RBAC: Cliente ve sólo sus propios choferes
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId))
            {
                query = query.Where(c => c.ClienteEmpresaId == clienteId);
            }
            else
            {
                return Ok(new List<object>());
            }
        }

        if (estado.HasValue)
        {
            query = query.Where(c => c.EstadoPostulacion == estado.Value);
        }

        if (tipo.HasValue)
        {
            query = query.Where(c => c.TipoConductor == tipo.Value);
        }

        var list = await query.OrderByDescending(c => c.Id).ToListAsync();

        var docsCountDict = await _db.DocumentosAdjuntos
            .Where(d => d.ConductorId.HasValue)
            .GroupBy(d => d.ConductorId!.Value)
            .Select(g => new { ConductorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConductorId, x => x.Count);

        return Ok(list.Select(c => new
        {
            c.Id,
            c.Nombre,
            c.Apellido,
            NombreCompleto = c.NombreCompleto,
            c.CUIL,
            c.FechaNacimiento,
            c.CelularWhatsApp,
            c.Email,
            c.Domicilio,
            c.Localidad,
            c.Provincia,
            c.PatenteCamion,
            c.CapacidadCargaKg,
            c.CapacidadVolumenM3,
            c.TipoConductor,
            TipoConductorNombre = c.TipoConductor.ToString(),
            c.ClienteEmpresaId,
            ClienteRazonSocial = c.ClienteEmpresa?.RazonSocial,
            c.EstadoPostulacion,
            EstadoNombre = c.EstadoPostulacion.ToString(),
            c.LicenciaArchivoUrl,
            c.SeguroCamionArchivoUrl,
            c.EmailConfirmado,
            c.FechaConfirmacionEmail,
            c.FechaPostulacion,
            c.UsuarioId,
            UsuarioActivo = c.Usuario?.Activo ?? true,
            DocumentosCount = docsCountDict.GetValueOrDefault(c.Id, 0)
        }));
    }

    [HttpPost("flota-lcp")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> CrearConductorFlota([FromBody] CrearConductorFlotaDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var cuilLimpio = dto.CUIL.Trim();
        if (await _db.Conductores.AnyAsync(c => c.CUIL.Trim() == cuilLimpio))
        {
            return Conflict(new { Mensaje = $"Ya existe un chofer registrado con el CUIL {dto.CUIL}." });
        }

        var emailLimpio = dto.Email.Trim().ToLower();
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == emailLimpio))
        {
            return Conflict(new { Mensaje = $"Ya existe un usuario con el correo electrónico {dto.Email}." });
        }

        // 1. Generar usuario de conductor operativo con acceso habilitado
        var tempPass = DbInitializer.GenerarPasswordProvisoria("LCPChofer");
        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = dto.Email.Trim(),
            PasswordHash = DbInitializer.HashPassword(tempPass),
            Telefono = dto.CelularWhatsApp.Trim(),
            Rol = RolUsuario.Conductor,
            Activo = true,
            DebeCambiarPassword = true,
            FechaAlta = DateTime.UtcNow
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // 2. Crear Conductor de Flota LCP (Inmediatamente Aprobado y Asignable)
        var conductor = new Conductor
        {
            UsuarioId = usuario.Id,
            TipoConductor = TipoConductor.FlotaLCP,
            ClienteEmpresaId = null, // Oficial de la flota LCP
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            CUIL = cuilLimpio,
            FechaNacimiento = dto.FechaNacimiento,
            CelularWhatsApp = dto.CelularWhatsApp.Trim(),
            Email = dto.Email.Trim(),
            Domicilio = dto.Domicilio.Trim(),
            Localidad = dto.Localidad.Trim(),
            Provincia = dto.Provincia.Trim(),
            PatenteCamion = dto.PatenteCamion.ToUpper().Trim(),
            CapacidadCargaKg = dto.CapacidadCargaKg,
            CapacidadVolumenM3 = dto.CapacidadVolumenM3,
            EstadoPostulacion = EstadoPostulacionAlta.Aprobada,
            DeclaracionJuradaAceptada = true,
            EmailConfirmado = true, // Creado directamente por el administrador
            FechaConfirmacionEmail = DateTime.UtcNow,
            FechaPostulacion = DateTime.UtcNow
        };

        _db.Conductores.Add(conductor);
        await _db.SaveChangesAsync();

        // 3. Vincular documentos subidos en caso de existir
        if (dto.DocumentosIds != null && dto.DocumentosIds.Any())
        {
            var docs = await _db.DocumentosAdjuntos
                .Where(d => dto.DocumentosIds.Contains(d.Id))
                .ToListAsync();
            foreach (var doc in docs)
            {
                doc.ConductorId = conductor.Id;
            }
            await _db.SaveChangesAsync();
        }

        // 4. Notificar por email con sus credenciales de acceso auto-generadas
        try
        {
            await _emailService.SendCredencialesAutoGeneradasAsync(usuario.Email, conductor.NombreCompleto, tempPass, "Conductor Flota Oficial LCP");
        }
        catch { }

        return Ok(new
        {
            Mensaje = $"Chofer {conductor.NombreCompleto} incorporado exitosamente a la Flota LCP.",
            ConductorId = conductor.Id,
            UsuarioId = usuario.Id,
            Email = usuario.Email,
            PasswordProvisoria = tempPass
        });
    }

    [HttpPost]
    [Authorize(Roles = "Cliente,Administrador")]
    public async Task<ActionResult> CrearChoferPropio([FromBody] PostulacionChoferDto dto)
    {
        int? clienteId = dto.ClienteEmpresaId;
        if (User.IsInRole(Roles.Cliente))
        {
            var claimClienteId = User.FindFirstValue("ClienteId");
            if (int.TryParse(claimClienteId, out int idPropio))
            {
                clienteId = idPropio;
            }
        }

        // 1. Validar que no exista otro chofer con el mismo CUIL
        var cuilLimpio = dto.CUIL.Trim();
        if (await _db.Conductores.AnyAsync(c => c.CUIL.Trim() == cuilLimpio))
        {
            return Conflict(new { Mensaje = $"Ya existe un chofer registrado en el sistema con el CUIL {dto.CUIL}." });
        }

        // 2. Si se ingresó correo, validar que no esté repetido en usuarios o conductores
        string emailChofer = string.IsNullOrWhiteSpace(dto.Email) 
            ? $"chofer.{dto.PatenteCamion.Replace(" ", "").ToLower()}@propio.lcp" 
            : dto.Email.Trim().ToLower();

        // 3. Crear automáticamente cuenta de Usuario para el Chofer Propio (con acceso al sistema, hoja de ruta y chat)
        var usuarioExistente = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == emailChofer);
        Usuario usuarioChofer;
        string tempPass = DbInitializer.GenerarPasswordProvisoria("Chofer");

        if (usuarioExistente == null)
        {
            usuarioChofer = new Usuario
            {
                Nombre = dto.Nombre.Trim(),
                Apellido = dto.Apellido.Trim(),
                Email = emailChofer,
                PasswordHash = DbInitializer.HashPassword(tempPass),
                Telefono = dto.CelularWhatsApp.Trim(),
                Rol = RolUsuario.Conductor,
                ClienteId = clienteId,
                Activo = false, // Queda inactivo hasta que el Administrador lo apruebe
                DebeCambiarPassword = true,
                EmailConfirmado = true,
                FechaConfirmacionEmail = DateTime.UtcNow,
                FechaAlta = DateTime.UtcNow
            };
            _db.Usuarios.Add(usuarioChofer);
            await _db.SaveChangesAsync();
        }
        else
        {
            usuarioChofer = usuarioExistente;
        }

        var conductor = new Conductor
        {
            UsuarioId = usuarioChofer.Id,
            TipoConductor = TipoConductor.PropioCliente,
            ClienteEmpresaId = clienteId,
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            CUIL = cuilLimpio,
            FechaNacimiento = dto.FechaNacimiento,
            CelularWhatsApp = dto.CelularWhatsApp.Trim(),
            Email = emailChofer,
            Domicilio = string.IsNullOrWhiteSpace(dto.Domicilio) ? "S/D" : dto.Domicilio.Trim(),
            Localidad = string.IsNullOrWhiteSpace(dto.Localidad) ? "Santa Rosa" : dto.Localidad.Trim(),
            Provincia = string.IsNullOrWhiteSpace(dto.Provincia) ? "La Pampa" : dto.Provincia.Trim(),
            PatenteCamion = dto.PatenteCamion.ToUpper().Trim(),
            CapacidadCargaKg = dto.CapacidadCargaKg > 0 ? dto.CapacidadCargaKg : 12000,
            CapacidadVolumenM3 = dto.CapacidadVolumenM3 > 0 ? dto.CapacidadVolumenM3 : 50,
            EstadoPostulacion = EstadoPostulacionAlta.PendienteRevision, // Requiere aprobación del Administrador
            DeclaracionJuradaAceptada = true,
            EmailConfirmado = true,
            FechaConfirmacionEmail = DateTime.UtcNow,
            FechaPostulacion = DateTime.UtcNow,
            LicenciaArchivoUrl = dto.LicenciaArchivoUrl,
            SeguroCamionArchivoUrl = dto.SeguroCamionArchivoUrl
        };

        _db.Conductores.Add(conductor);
        await _db.SaveChangesAsync();

        return Ok(new { 
            Mensaje = $"Chofer {conductor.NombreCompleto} cargado exitosamente. Ha quedado pendiente de validación y aprobación por el Administrador antes de poder ser utilizado en viajes.", 
            conductor.Id,
            UsuarioId = usuarioChofer.Id,
            Email = usuarioChofer.Email
        });
    }

    [HttpPut("{id}/aprobar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> AprobarPostulacion(int id)
    {
        var cond = await _db.Conductores.Include(c => c.ClienteEmpresa).FirstOrDefaultAsync(c => c.Id == id);
        if (cond == null) return NotFound();

        cond.EstadoPostulacion = EstadoPostulacionAlta.Aprobada;
        string tempPass = DbInitializer.GenerarPasswordProvisoria("Chofer");

        // Si ya tiene usuario asignado (ej. chofer cargado por cliente/operador), activarlo y generar credenciales
        if (cond.UsuarioId.HasValue)
        {
            var usuario = await _db.Usuarios.FindAsync(cond.UsuarioId.Value);
            if (usuario != null)
            {
                usuario.Activo = true;
                usuario.DebeCambiarPassword = true;
                usuario.PasswordHash = DbInitializer.HashPassword(tempPass);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            // Si vino por postulación web pública y no tenía usuario creado
            var usuario = new Usuario
            {
                Nombre = cond.Nombre,
                Apellido = cond.Apellido,
                Email = cond.Email,
                PasswordHash = DbInitializer.HashPassword(tempPass),
                Telefono = cond.CelularWhatsApp,
                Rol = RolUsuario.Conductor,
                ClienteId = cond.ClienteEmpresaId,
                Activo = true,
                DebeCambiarPassword = true,
                FechaAlta = DateTime.UtcNow
            };
            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();
            cond.UsuarioId = usuario.Id;
        }

        await _db.SaveChangesAsync();

        // Enviar credenciales auto-generadas al correo del conductor
        try
        {
            string rolDesc = cond.TipoConductor == TipoConductor.PropioCliente && cond.ClienteEmpresa != null
                ? $"Conductor de Transporte Propio - {cond.ClienteEmpresa.RazonSocial}"
                : "Conductor Oficial Flota LCP";

            _ = _emailService.SendCredencialesAutoGeneradasAsync(cond.Email, cond.NombreCompleto, tempPass, rolDesc);
        }
        catch { }

        return Ok(new { Mensaje = $"Chofer {cond.NombreCompleto} aprobado exitosamente. Cuenta habilitada y credenciales enviadas a {cond.Email}." });
    }

    [HttpPut("{id}/rechazar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> RechazarPostulacion(int id)
    {
        var cond = await _db.Conductores.Include(c => c.Usuario).FirstOrDefaultAsync(c => c.Id == id);
        if (cond == null) return NotFound();

        cond.EstadoPostulacion = EstadoPostulacionAlta.Rechazada;
        if (cond.Usuario != null)
        {
            cond.Usuario.Activo = false;
        }
        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Postulación / Chofer {cond.NombreCompleto} rechazada." });
    }

    [HttpPut("{id}/baja")]
    [Authorize(Roles = "Administrador,Cliente,Operador")]
    public async Task<ActionResult> DarDeBajaConductor(int id)
    {
        var cond = await _db.Conductores.Include(c => c.Usuario).FirstOrDefaultAsync(c => c.Id == id);
        if (cond == null) return NotFound();

        // Si es cliente, validar que el chofer pertenezca a su empresa
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId))
            {
                if (cond.ClienteEmpresaId != clienteId)
                {
                    return Forbid();
                }
            }
        }

        cond.EstadoPostulacion = EstadoPostulacionAlta.Rechazada;
        if (cond.Usuario != null)
        {
            cond.Usuario.Activo = false;
        }

        await _db.SaveChangesAsync();
        return Ok(new { Mensaje = $"Chofer {cond.NombreCompleto} dado de baja exitosamente del padrón." });
    }
}
