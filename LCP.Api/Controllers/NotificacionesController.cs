using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly LcpDbContext _db;

    public NotificacionesController(LcpDbContext db)
    {
        _db = db;
    }

    [HttpGet("operaciones")]
    public async Task<ActionResult<List<NotificacionOperacionDto>>> GetNotificacionesOperaciones()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int currentUserId);

        bool esAdmin = User.IsInRole(Roles.Administrador);
        bool esOperador = User.IsInRole(Roles.Operador);
        bool esAuditor = User.IsInRole(Roles.Auditor);
        bool esCliente = User.IsInRole(Roles.Cliente);
        bool esConductor = User.IsInRole(Roles.Conductor);

        int? clienteId = null;
        if (esCliente)
        {
            var cidStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(cidStr, out int cid))
            {
                clienteId = cid;
            }
            else
            {
                var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == currentUserId);
                clienteId = usuario?.ClienteId;
            }
        }

        int? conductorId = null;
        if (esConductor)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var cond = await _db.Conductores.FirstOrDefaultAsync(c => c.UsuarioId == currentUserId || (userEmail != null && c.Email == userEmail));
            conductorId = cond?.Id;
        }

        var lista = new List<NotificacionOperacionDto>();

        // 1. SOLICITUDES DE OPERACIÓN
        // Excluimos al Auditor (el auditor atiende contratos y liquidaciones contables, no maniobras de patio ni turnos)
        if (!esAuditor)
        {
            var querySols = _db.SolicitudesOperacion
                .Include(s => s.ClienteEmpresa)
                .Include(s => s.Darsena)
                .Include(s => s.ConductorAsignado)
                .Include(s => s.Viaje)
                .AsQueryable();

            if (esCliente && clienteId.HasValue)
            {
                querySols = querySols.Where(s => s.ClienteEmpresaId == clienteId.Value);
            }
            else if (esConductor)
            {
                if (conductorId.HasValue)
                {
                    querySols = querySols.Where(s => s.ConductorAsignadoId == conductorId.Value);
                }
                else
                {
                    querySols = querySols.Where(s => false);
                }
            }
            else if (esOperador)
            {
                // El operador gestiona solicitudes pendientes de asignación y viajes/maniobras en curso
                querySols = querySols.Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCurso);
            }

            var solicitudes = await querySols
                .OrderByDescending(s => s.Id)
                .Take(15)
                .ToListAsync();

            foreach (var s in solicitudes)
            {
                var notif = new NotificacionOperacionDto
                {
                    Id = s.Id,
                    Codigo = s.CodigoSolicitud,
                    Url = esConductor ? $"/Conductor/HojaDeRuta/{s.Id}" : $"/Solicitudes/Detalle/{s.Id}",
                    FechaHora = s.FechaHoraTurnoProgramado != default ? s.FechaHoraTurnoProgramado : (s.FechaSalidaCliente ?? DateTime.Now)
                };

                switch (s.Estado)
                {
                    case EstadoSolicitud.Pendiente:
                        if (esConductor) continue; // Un conductor no atiende solicitudes sin programar
                        notif.Tipo = "Solicitud";
                        notif.Icono = "⏳";
                        notif.Titulo = esOperador || esAdmin
                            ? $"{s.CodigoSolicitud} · Pendiente de Turno"
                            : $"{s.CodigoSolicitud} · Solicitud Ingresada";
                        notif.Mensaje = esOperador || esAdmin
                            ? $"{s.ClienteEmpresa?.RazonSocial}: requiere asignación de muelle/turno."
                            : "Tu solicitud fue recibida. Aguarda programación de turno por LCP.";
                        notif.Estado = "Pendiente";
                        break;

                    case EstadoSolicitud.Programada:
                        notif.Tipo = "Turno";
                        notif.Icono = "📅";
                        notif.Titulo = esConductor
                            ? $"{s.CodigoSolicitud} · Nuevo Viaje Asignado"
                            : $"{s.CodigoSolicitud} · Turno Asignado";
                        notif.Mensaje = esConductor
                            ? $"Destino: {s.Darsena?.Codigo ?? "Patio LCP"} para las {s.FechaHoraTurnoProgramado:HH:mm} hs."
                            : $"Programado en {s.Darsena?.Codigo ?? "Dársena"} para {s.FechaHoraTurnoProgramado:dd/MM HH:mm} hs.";
                        notif.Estado = "Programada";
                        break;

                    case EstadoSolicitud.EnCurso:
                        if (s.Viaje != null && s.Viaje.EstadoViaje == EstadoViaje.EnPredioDarsena)
                        {
                            notif.Tipo = "Patio";
                            notif.Icono = "📍";
                            notif.Titulo = $"{s.CodigoSolicitud} · Arribó a Dársena";
                            notif.Mensaje = $"Camión de {s.ClienteEmpresa?.RazonSocial} en dársena {s.Darsena?.Codigo ?? "Patio"}. Listo para operar.";
                            if (esOperador) notif.Url = "/Patio/Darsenas";
                        }
                        else if (s.Viaje != null && s.Viaje.EstadoViaje == EstadoViaje.ConIncidenteDemora)
                        {
                            notif.Tipo = "Alerta Ruta";
                            notif.Icono = "⚠️";
                            notif.Titulo = $"{s.CodigoSolicitud} · Incidente/Demora Reportada";
                            notif.Mensaje = s.Viaje.ObservacionesChofer ?? "Chofer reportó novedad o demora en ruta.";
                        }
                        else
                        {
                            notif.Tipo = "Operación";
                            notif.Icono = "🚛";
                            notif.Titulo = $"{s.CodigoSolicitud} · En Ejecución";
                            notif.Mensaje = $"Operando en {s.Darsena?.Codigo ?? "Patio"}. Chofer: {s.ConductorAsignado?.NombreCompleto ?? "Asignado"}.";
                        }
                        notif.Estado = "En Curso";
                        break;

                    case EstadoSolicitud.Completada:
                        if (esOperador) continue; // Las completadas no son tareas pendientes para el operador
                        notif.Tipo = "Finalizada";
                        notif.Icono = "✓";
                        notif.Titulo = $"{s.CodigoSolicitud} · Completada";
                        notif.Mensaje = $"Operación de {(s.TipoOperacion == TipoOperacionSolicitud.ArriboIngreso ? "descarga" : "retiro")} finalizada exitosamente.";
                        notif.Estado = "Completada";
                        break;

                    case EstadoSolicitud.Cancelada:
                        if (esOperador) continue;
                        notif.Tipo = "Cancelada";
                        notif.Icono = "⛔";
                        notif.Titulo = $"{s.CodigoSolicitud} · Solicitud Cancelada";
                        notif.Mensaje = "La operación fue rechazada o cancelada.";
                        notif.Estado = "Cancelada";
                        break;
                }

                notif.TiempoHace = CalcularTiempoHace(notif.FechaHora);
                lista.Add(notif);
            }
        }

        // 2. CONTRATOS DE ALQUILER DE DÁRSENAS
        // Corresponden EXCLUSIVAMENTE a Auditor, Administrador y al Cliente respectivo.
        // Ni el Operador ni el Conductor deben recibir alertas de contratos de dársenas.
        if (esAuditor || esAdmin || esCliente)
        {
            var queryContratos = _db.ContratosAlquilerDarsena
                .Include(c => c.ClienteEmpresa)
                .Include(c => c.Darsena)
                .AsQueryable();

            if (esCliente && clienteId.HasValue)
            {
                queryContratos = queryContratos.Where(c => c.ClienteEmpresaId == clienteId.Value);
            }
            else if (esAuditor)
            {
                queryContratos = queryContratos.Where(c =>
                    c.Estado == EstadoContratoDarsena.PendienteRevision ||
                    c.Estado == EstadoContratoDarsena.FacturaCargadaPendienteHabilitacion);
            }

            var contratos = await queryContratos
                .OrderByDescending(c => c.Id)
                .Take(10)
                .ToListAsync();

            foreach (var c in contratos)
            {
                var notif = new NotificacionOperacionDto
                {
                    Id = 10000 + c.Id,
                    Codigo = c.Darsena?.Codigo ?? "Dársena",
                    Url = "/Facturacion",
                    FechaHora = c.FechaPagoFactura ?? c.FechaAprobacionAuditor ?? c.FechaInicio
                };

                if (c.Estado == EstadoContratoDarsena.FacturaCargadaPendienteHabilitacion)
                {
                    notif.Tipo = "Factura";
                    notif.Icono = "📄";
                    notif.Titulo = $"{c.Darsena?.Codigo} · Factura de Alquiler";
                    notif.Mensaje = esCliente
                        ? "Comprobante cargado. Pendiente de verificación por auditoría LCP."
                        : $"{c.ClienteEmpresa?.RazonSocial} cargó la factura. Lista para auditar y habilitar muelle.";
                    notif.Estado = "Pendiente Habilitación";
                }
                else if (c.Estado == EstadoContratoDarsena.PendienteRevision)
                {
                    notif.Tipo = "Dársena";
                    notif.Icono = "🛡️";
                    notif.Titulo = $"{c.Darsena?.Codigo} · Solicitud de Dársena";
                    notif.Mensaje = esCliente
                        ? "Tu solicitud de alquiler está en revisión técnica por el Auditor LCP."
                        : $"{c.ClienteEmpresa?.RazonSocial} solicitó dársena. Requiere evaluación técnica de compatibilidad.";
                    notif.Estado = "Evaluación Auditor";
                }
                else if (c.Estado == EstadoContratoDarsena.Activo && esCliente)
                {
                    notif.Tipo = "Dársena";
                    notif.Icono = "🟢";
                    notif.Titulo = $"{c.Darsena?.Codigo} · Habilitada para Operar";
                    notif.Mensaje = "Contrato activo. Tu dársena exclusiva está operativa para recepción y despacho.";
                    notif.Estado = "Activo";
                }
                else
                {
                    continue;
                }

                notif.TiempoHace = CalcularTiempoHace(notif.FechaHora);
                lista.Add(notif);
            }
        }

        // 3. LIQUIDACIONES DE SERVICIOS (Para Auditor o Admin)
        if (esAuditor || esAdmin)
        {
            var liquidacionesPendientes = await _db.Liquidaciones
                .Include(l => l.ClienteEmpresa)
                .Where(l => l.Estado == EstadoLiquidacion.PendienteDeAuditoria)
                .OrderByDescending(l => l.Id)
                .Take(5)
                .ToListAsync();

            foreach (var l in liquidacionesPendientes)
            {
                var notif = new NotificacionOperacionDto
                {
                    Id = 20000 + l.Id,
                    Codigo = l.NumeroComprobante,
                    Url = "/Facturacion",
                    FechaHora = DateTime.Now,
                    Tipo = "Liquidación",
                    Icono = "💵",
                    Titulo = $"{l.NumeroComprobante} · Liquidación Pendiente",
                    Mensaje = $"Liquidación para {l.ClienteEmpresa?.RazonSocial} aguarda validación contable.",
                    Estado = "Pendiente Auditoría"
                };
                notif.TiempoHace = CalcularTiempoHace(notif.FechaHora);
                lista.Add(notif);
            }
        }

        var ordenadas = lista.OrderByDescending(n => n.FechaHora).Take(12).ToList();
        return Ok(ordenadas);
    }

    [HttpGet("mensajes")]
    public async Task<ActionResult<List<NotificacionMensajeDto>>> GetNotificacionesMensajes()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int currentUserId);

        bool esAdmin = User.IsInRole(Roles.Administrador);
        bool esOperador = User.IsInRole(Roles.Operador);
        bool esCliente = User.IsInRole(Roles.Cliente);
        bool esConductor = User.IsInRole(Roles.Conductor);

        int? clienteId = null;
        if (esCliente)
        {
            var cidStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(cidStr, out int cid))
            {
                clienteId = cid;
            }
            else
            {
                var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == currentUserId);
                clienteId = usuario?.ClienteId;
            }
        }

        int? conductorId = null;
        if (esConductor)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var cond = await _db.Conductores.FirstOrDefaultAsync(c => c.UsuarioId == currentUserId || (userEmail != null && c.Email == userEmail));
            conductorId = cond?.Id;
        }

        // Filtrar mensajes NO LEÍDOS donde el usuario actual NO es el remitente
        var query = _db.MensajesChat
            .Include(m => m.RemitenteUsuario)
            .Include(m => m.SolicitudOperacion)
            .ThenInclude(s => s!.ClienteEmpresa)
            .Where(m => !m.Leido && m.RemitenteUsuarioId != currentUserId);

        var mensajesCandidatos = await query
            .OrderByDescending(m => m.FechaHoraEnvio)
            .Take(50)
            .ToListAsync();

        var lista = new List<NotificacionMensajeDto>();

        foreach (var m in mensajesCandidatos)
        {
            bool esParaUsuario = false;

            // 1. Mensaje con destinatario explícito
            if (m.DestinatarioUsuarioId.HasValue)
            {
                if (m.DestinatarioUsuarioId.Value == currentUserId)
                {
                    esParaUsuario = true;
                }
            }
            // 2. Chat Directo 1 a 1 canonical DIRECT-{min}-{max}
            else if (m.ConversacionId.StartsWith("DIRECT-"))
            {
                var parts = m.ConversacionId.Split('-');
                if (parts.Length == 3 &&
                    int.TryParse(parts[1], out int u1) &&
                    int.TryParse(parts[2], out int u2))
                {
                    if (currentUserId == u1 || currentUserId == u2)
                    {
                        esParaUsuario = true;
                    }
                }
            }
            // 3. Sala Tripartita de Operación GRP-SOL-{id}
            else if (m.ConversacionId.StartsWith("GRP-SOL-"))
            {
                if (esAdmin || esOperador)
                {
                    esParaUsuario = true;
                }
                else if (esCliente && clienteId.HasValue && m.SolicitudOperacion != null && m.SolicitudOperacion.ClienteEmpresaId == clienteId.Value)
                {
                    esParaUsuario = true;
                }
                else if (esConductor && conductorId.HasValue && m.SolicitudOperacion != null && m.SolicitudOperacion.ConductorAsignadoId == conductorId.Value)
                {
                    esParaUsuario = true;
                }
            }

            if (esParaUsuario)
            {
                lista.Add(new NotificacionMensajeDto
                {
                    Id = m.Id,
                    RemitenteNombre = m.RemitenteUsuario?.NombreCompleto ?? "Usuario LCP",
                    RemitenteRol = m.RemitenteUsuario?.Rol.ToString() ?? "Personal",
                    ConversacionId = m.ConversacionId,
                    Asunto = m.SolicitudOperacion != null ? $"Operación {m.SolicitudOperacion.CodigoSolicitud}" : "Mensaje Directo",
                    TextoMensaje = m.TextoMensaje,
                    Url = $"/Inbox?conv={m.ConversacionId}",
                    FechaHora = m.FechaHoraEnvio,
                    TiempoHace = CalcularTiempoHace(m.FechaHoraEnvio),
                    EsIncidente = m.EsIncidenteRuta,
                    Leida = m.Leido
                });
            }
        }

        return Ok(lista.Take(12).ToList());
    }

    private static string CalcularTiempoHace(DateTime fecha)
    {
        var diff = DateTime.Now - fecha;
        if (diff.TotalMinutes < 1) return "Recién";
        if (diff.TotalMinutes < 60) return $"Hace {Math.Floor(diff.TotalMinutes)} min";
        if (diff.TotalHours < 24) return $"Hace {Math.Floor(diff.TotalHours)} h";
        if (diff.TotalDays < 7) return $"Hace {Math.Floor(diff.TotalDays)} d";
        return fecha.ToString("dd/MM");
    }
}
