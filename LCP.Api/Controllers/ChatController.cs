using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Api.Hubs;
using LCP.Api.Services;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly IHubContext<ChatIncidentesHub> _chatHub;
    private readonly IChatAuthorizationService _chatAuth;

    public ChatController(
        LcpDbContext db,
        IHubContext<ChatIncidentesHub> chatHub,
        IChatAuthorizationService chatAuth)
    {
        _db = db;
        _chatHub = chatHub;
        _chatAuth = chatAuth;
    }

    [HttpGet("conversaciones")]
    public async Task<ActionResult<List<ConversacionResumenDto>>> GetConversaciones()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int currentUserId);

        var ultimosMensajes = await _db.MensajesChat
            .Include(m => m.RemitenteUsuario)
            .OrderByDescending(m => m.FechaHoraEnvio)
            .ToListAsync();

        var resumenes = new Dictionary<string, ConversacionResumenDto>();

        // 1. Grupos Tripartitos por cada Operación / Solicitud a la que pertenezca estrictamente el usuario
        var querySols = _db.SolicitudesOperacion
            .Include(s => s.ClienteEmpresa)
            .Include(s => s.ConductorAsignado)
            .AsQueryable();

        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (!int.TryParse(clienteIdStr, out int clienteId))
            {
                var usr = await _db.Usuarios.FindAsync(currentUserId);
                if (usr?.ClienteId != null) clienteId = usr.ClienteId.Value;
            }
            querySols = querySols.Where(s => s.ClienteEmpresaId == clienteId);
        }
        else if (User.IsInRole(Roles.Conductor))
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var cond = await _db.Conductores.FirstOrDefaultAsync(c => c.UsuarioId == currentUserId || (userEmail != null && c.Email == userEmail));
            if (cond != null)
            {
                querySols = querySols.Where(s => s.ConductorAsignadoId == cond.Id);
            }
            else
            {
                querySols = querySols.Where(s => false);
            }
        }
        else if (!User.IsInRole(Roles.Administrador) && !User.IsInRole(Roles.Operador))
        {
            querySols = querySols.Where(s => false);
        }

        var solicitudes = await querySols
            .OrderByDescending(s => s.Id)
            .Take(25)
            .ToListAsync();

        foreach (var s in solicitudes)
        {
            string convId = $"GRP-SOL-{s.Id}";
            var mensajesGrupo = ultimosMensajes.Where(m => m.ConversacionId == convId).ToList();
            var ultimo = mensajesGrupo.FirstOrDefault();

            string choferNombre = s.ConductorAsignado != null ? s.ConductorAsignado.Nombre : "Chofer LCP";
            string clienteNombre = s.ClienteEmpresa?.RazonSocial ?? "Cliente";
            bool finalizada = s.Estado == EstadoSolicitud.Completada || s.Estado == EstadoSolicitud.Cancelada;

            resumenes[convId] = new ConversacionResumenDto
            {
                ConversacionId = convId,
                Titulo = $"Operación {s.CodigoSolicitud} · {clienteNombre}" + (finalizada ? $" ({(s.Estado == EstadoSolicitud.Completada ? "Completada" : "Cancelada")})" : ""),
                Subtitulo = finalizada 
                    ? $"🔒 Sala cerrada · {s.Estado}" 
                    : $"Grupo Tripartito: {clienteNombre} + {choferNombre} + Operador",
                UltimoMensaje = ultimo?.TextoMensaje ?? (finalizada ? "Operación finalizada. Chat cerrado." : "Sala grupal operativa abierta para coordinación."),
                HoraUltimoMensaje = ultimo != null ? ultimo.FechaHoraEnvio.ToString("HH:mm") : s.FechaHoraTurnoProgramado.ToString("dd/MM"),
                MensajesNoLeidos = mensajesGrupo.Count(m => !m.Leido && m.RemitenteUsuarioId != currentUserId),
                Activo = !finalizada,
                Finalizada = finalizada,
                SolicitudId = s.Id
            };
        }

        // 2. Contactos Directos 1 a 1 EXCLUSIVAMENTE donde el usuario actual sea uno de los participantes
        var usuarios = await _db.Usuarios.Where(u => u.Activo && u.Id != currentUserId).ToListAsync();
        foreach (var u in usuarios)
        {
            // ID canónico 1 a 1: DIRECT-{min}-{max}
            int min = Math.Min(currentUserId, u.Id);
            int max = Math.Max(currentUserId, u.Id);
            string directId = $"DIRECT-{min}-{max}";

            if (!resumenes.ContainsKey(directId))
            {
                var directMsgs = ultimosMensajes.Where(m => m.ConversacionId == directId).ToList();
                var ultDirect = directMsgs.FirstOrDefault();

                resumenes[directId] = new ConversacionResumenDto
                {
                    ConversacionId = directId,
                    Titulo = $"{u.NombreCompleto} ({u.Rol})",
                    Subtitulo = $"Chat directo · {u.Email}",
                    UltimoMensaje = ultDirect?.TextoMensaje ?? "Iniciá una conversación directa...",
                    HoraUltimoMensaje = ultDirect != null ? ultDirect.FechaHoraEnvio.ToString("HH:mm") : "",
                    MensajesNoLeidos = directMsgs.Count(m => !m.Leido && m.RemitenteUsuarioId != currentUserId),
                    Activo = true,
                    SolicitudId = null
                };
            }
        }

        // Las conversaciones de otros usuarios NO se exponen jamás. Estricta privacidad.
        return Ok(resumenes.Values.OrderByDescending(r => !string.IsNullOrEmpty(r.HoraUltimoMensaje)).ToList());
    }

    [HttpGet("contactos")]
    public async Task<ActionResult<List<UsuarioDto>>> GetContactos()
    {
        var users = await _db.Usuarios
            .Where(u => u.Activo)
            .OrderBy(u => u.Rol)
            .ThenBy(u => u.Apellido)
            .ToListAsync();

        return Ok(users.Select(u => new UsuarioDto
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Apellido = u.Apellido,
            Email = u.Email,
            Telefono = u.Telefono,
            Rol = u.Rol,
            Activo = u.Activo
        }).ToList());
    }

    [HttpGet("historial/{conversacionId}")]
    public async Task<ActionResult<List<MensajeChatDto>>> GetHistorial(string conversacionId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int currentUserId);

        // Control de Privacidad: Validar acceso estricto a la sala o chat directo
        if (!await _chatAuth.PuedeAccederAConversacionAsync(conversacionId, currentUserId, User))
        {
            return Forbid();
        }

        var mensajes = await _db.MensajesChat
            .Include(m => m.RemitenteUsuario)
            .Where(m => m.ConversacionId == conversacionId)
            .OrderBy(m => m.FechaHoraEnvio)
            .ToListAsync();

        // Al consultar el historial de la sala, marcar como leídos los mensajes dirigidos al usuario actual
        var noLeidos = mensajes.Where(m => !m.Leido && m.RemitenteUsuarioId != currentUserId).ToList();
        if (noLeidos.Any())
        {
            foreach (var m in noLeidos)
            {
                m.Leido = true;
            }
            await _db.SaveChangesAsync();
        }

        return Ok(mensajes.Select(m => new MensajeChatDto
        {
            Id = m.Id,
            ConversacionId = m.ConversacionId,
            RemitenteUsuarioId = m.RemitenteUsuarioId,
            RemitenteNombre = m.RemitenteUsuario?.NombreCompleto ?? "Usuario",
            RemitenteRol = m.RemitenteUsuario?.Rol.ToString() ?? "",
            DestinatarioUsuarioId = m.DestinatarioUsuarioId,
            SolicitudOperacionId = m.SolicitudOperacionId,
            TextoMensaje = m.TextoMensaje,
            EsIncidenteRuta = m.EsIncidenteRuta,
            Leido = m.Leido,
            FechaHoraEnvio = m.FechaHoraEnvio
        }).ToList());
    }

    [HttpPost("marcar-leidos/{conversacionId}")]
    public async Task<IActionResult> MarcarLeidos(string conversacionId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int currentUserId);

        if (!await _chatAuth.PuedeAccederAConversacionAsync(conversacionId, currentUserId, User))
        {
            return Forbid();
        }

        var noLeidos = await _db.MensajesChat
            .Where(m => m.ConversacionId == conversacionId && !m.Leido && m.RemitenteUsuarioId != currentUserId)
            .ToListAsync();

        if (noLeidos.Any())
        {
            foreach (var m in noLeidos)
            {
                m.Leido = true;
            }
            await _db.SaveChangesAsync();
        }

        return Ok(new { Exitoso = true, MensajesMarcados = noLeidos.Count });
    }

    [HttpPost("marcar-todos-leidos")]
    public async Task<IActionResult> MarcarTodosLeidos()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int currentUserId);

        var noLeidos = await _db.MensajesChat
            .Where(m => !m.Leido && m.RemitenteUsuarioId != currentUserId)
            .ToListAsync();

        int count = 0;
        foreach (var m in noLeidos)
        {
            if (await _chatAuth.PuedeAccederAConversacionAsync(m.ConversacionId, currentUserId, User))
            {
                m.Leido = true;
                count++;
            }
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return Ok(new { Exitoso = true, MensajesMarcados = count });
    }

    [HttpPost("enviar")]
    public async Task<ActionResult<MensajeChatDto>> EnviarMensaje([FromBody] EnviarMensajeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int remitenteId);

        // Control de Privacidad: Validar acceso legítimo a la conversación
        if (!await _chatAuth.PuedeAccederAConversacionAsync(dto.ConversacionId, remitenteId, User))
        {
            return Forbid();
        }

        // Si es chat directo, deducir automáticamente el destinatario
        if (dto.ConversacionId.StartsWith("DIRECT-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = dto.ConversacionId.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[1], out int u1) && int.TryParse(parts[2], out int u2))
            {
                dto.DestinatarioUsuarioId = remitenteId == u1 ? u2 : u1;
            }
        }

        // Si es una sala de grupo de operación (GRP-SOL-...), validar que no esté finalizada
        if (dto.ConversacionId.StartsWith("GRP-SOL-", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(dto.ConversacionId.Replace("GRP-SOL-", "", StringComparison.OrdinalIgnoreCase), out int solId))
            {
                var sol = await _db.SolicitudesOperacion.FindAsync(solId);
                if (sol != null)
                {
                    dto.SolicitudOperacionId = sol.Id;
                    if (sol.Estado == EstadoSolicitud.Completada || sol.Estado == EstadoSolicitud.Cancelada)
                    {
                        return BadRequest(new { Mensaje = $"La operación {sol.CodigoSolicitud} ya fue {sol.Estado.ToString().ToLower()}. La sala de chat tripartita ha sido finalizada y cerrada." });
                    }
                }
            }
        }

        var remitente = await _db.Usuarios.FindAsync(remitenteId);

        var mensaje = new MensajeChat
        {
            ConversacionId = dto.ConversacionId,
            RemitenteUsuarioId = remitenteId,
            DestinatarioUsuarioId = dto.DestinatarioUsuarioId,
            SolicitudOperacionId = dto.SolicitudOperacionId,
            TextoMensaje = dto.TextoMensaje,
            EsIncidenteRuta = dto.EsIncidenteRuta,
            Leido = false,
            FechaHoraEnvio = DateTime.UtcNow
        };

        _db.MensajesChat.Add(mensaje);
        await _db.SaveChangesAsync();

        var mensajeDto = new MensajeChatDto
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            RemitenteUsuarioId = mensaje.RemitenteUsuarioId,
            RemitenteNombre = remitente?.NombreCompleto ?? "Usuario",
            RemitenteRol = remitente?.Rol.ToString() ?? "",
            DestinatarioUsuarioId = mensaje.DestinatarioUsuarioId,
            SolicitudOperacionId = mensaje.SolicitudOperacionId,
            TextoMensaje = mensaje.TextoMensaje,
            EsIncidenteRuta = mensaje.EsIncidenteRuta,
            Leido = mensaje.Leido,
            FechaHoraEnvio = mensaje.FechaHoraEnvio
        };

        // Enviar exclusivamente a los miembros de la sala o conversación
        await _chatHub.Clients.Group(dto.ConversacionId).SendAsync("RecibirMensaje", mensajeDto);

        if (dto.EsIncidenteRuta)
        {
            await _chatHub.Clients.Group(dto.ConversacionId).SendAsync("AlertaIncidente", mensajeDto);
        }

        return Ok(mensajeDto);
    }
}
