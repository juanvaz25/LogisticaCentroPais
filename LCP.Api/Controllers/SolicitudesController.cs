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
public class SolicitudesController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly ILiquidacionService _liquidacionService;
    private readonly IHubContext<PatioHub> _patioHub;
    private readonly IHubContext<ChatIncidentesHub> _chatHub;

    public SolicitudesController(LcpDbContext db, ILiquidacionService liquidacionService, IHubContext<PatioHub> patioHub, IHubContext<ChatIncidentesHub> chatHub)
    {
        _db = db;
        _liquidacionService = liquidacionService;
        _patioHub = patioHub;
        _chatHub = chatHub;
    }

    [HttpGet]
    public async Task<ActionResult<List<SolicitudOperacionDto>>> GetSolicitudes([FromQuery] EstadoSolicitud? estado)
    {
        var query = _db.SolicitudesOperacion
            .Include(s => s.ClienteEmpresa)
            .Include(s => s.Darsena)
            .Include(s => s.ConductorAsignado)
            .Include(s => s.ResponsableDeposito)
            .Include(s => s.Viaje)
            .Include(s => s.LoteInventario)
            .AsQueryable();

        // RBAC: Cliente ve sólo sus solicitudes
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId))
            {
                query = query.Where(s => s.ClienteEmpresaId == clienteId);
            }
            else
            {
                return Ok(new List<SolicitudOperacionDto>());
            }
        }
        else if (User.IsInRole(Roles.Conductor))
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            Conductor? cond = null;
            if (int.TryParse(userIdStr, out int userId))
            {
                cond = await _db.Conductores.FirstOrDefaultAsync(c => c.UsuarioId == userId || (userEmail != null && c.Email == userEmail));
            }
            if (cond != null)
            {
                query = query.Where(s => s.ConductorAsignadoId == cond.Id);
            }
            else
            {
                return Ok(new List<SolicitudOperacionDto>());
            }
        }

        if (estado.HasValue)
        {
            query = query.Where(s => s.Estado == estado.Value);
        }

        var list = await query.OrderByDescending(s => s.Id).ToListAsync();

        var dtos = list.Select(s => new SolicitudOperacionDto
        {
            Id = s.Id,
            CodigoSolicitud = s.CodigoSolicitud,
            ClienteEmpresaId = s.ClienteEmpresaId,
            ClienteRazonSocial = s.ClienteEmpresa?.RazonSocial ?? "N/A",
            DarsenaId = s.DarsenaId,
            DarsenaCodigo = s.Darsena?.Codigo ?? $"Dársena #{s.DarsenaId}",
            TipoOperacion = s.TipoOperacion,
            ModalidadTransporte = s.ModalidadTransporte,
            ConductorAsignadoId = s.ConductorAsignadoId,
            ConductorNombreCompleto = s.ConductorAsignado != null ? $"{s.ConductorAsignado.Nombre} {s.ConductorAsignado.Apellido}" : "Sin asignar",
            ConductorPatente = s.ConductorAsignado?.PatenteCamion,
            ResponsableDepositoId = s.ResponsableDepositoId,
            ResponsableDepositoNombre = s.ResponsableDeposito != null ? $"{s.ResponsableDeposito.Nombre} {s.ResponsableDeposito.Apellido}" : "Sin asignar",
            DescripcionMercaderia = s.DescripcionMercaderia,
            PesoTotalKg = s.PesoTotalKg,
            VolumenTotalM3 = s.VolumenTotalM3,
            DistanciaEstimadaKm = s.DistanciaEstimadaKm,
            FechaHoraTurnoProgramado = s.FechaHoraTurnoProgramado,
            FechaHoraIngresoReal = s.FechaHoraIngresoReal,
            FechaHoraEgresoReal = s.FechaHoraEgresoReal,
            Estado = s.Estado,
            ViajeId = s.Viaje?.Id,
            EstadoViaje = s.Viaje?.EstadoViaje,
            ViajeInfo = s.Viaje != null ? new ViajeDto
            {
                Id = s.Viaje.Id,
                SolicitudOperacionId = s.Id,
                CodigoSolicitud = s.CodigoSolicitud,
                ConductorId = s.Viaje.ConductorId,
                ConductorNombre = s.ConductorAsignado != null ? s.ConductorAsignado.NombreCompleto : "Sin asignar",
                PatenteCamion = s.ConductorAsignado != null ? s.ConductorAsignado.PatenteCamion : "",
                Origen = s.Viaje.Origen,
                Destino = s.Viaje.Destino,
                PuertaIngresoAsignada = s.Viaje.PuertaIngresoAsignada,
                EstadoViaje = s.Viaje.EstadoViaje,
                UltimaLatitud = s.Viaje.UltimaLatitud,
                UltimaLongitud = s.Viaje.UltimaLongitud,
                UltimaActualizacionGps = s.Viaje.UltimaActualizacionGps,
                ObservacionesChofer = s.Viaje.ObservacionesChofer
            } : null,
            LoteInventarioId = s.LoteInventarioId,
            LoteCodigo = s.LoteInventario?.CodigoLote,
            CantidadRetirada = s.CantidadRetirada,
            CiudadOrigen = s.CiudadOrigen,
            CiudadDestino = s.CiudadDestino,
            FechaSalidaCliente = s.FechaSalidaCliente,
            ObservacionesOperador = s.ObservacionesOperador
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SolicitudOperacionDto>> GetSolicitud(int id)
    {
        var s = await _db.SolicitudesOperacion
            .Include(x => x.ClienteEmpresa)
            .Include(x => x.Darsena)
            .Include(x => x.ConductorAsignado)
            .Include(x => x.ResponsableDeposito)
            .Include(x => x.Viaje)
            .Include(x => x.LoteInventario)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s == null) return NotFound();

        // RBAC check
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId) && s.ClienteEmpresaId != clienteId)
            {
                return Forbid();
            }
        }

        string convId = $"GRP-SOL-{s.Id}";
        var mensajesChat = await _db.MensajesChat
            .Include(m => m.RemitenteUsuario)
            .Where(m => m.ConversacionId == convId || m.SolicitudOperacionId == s.Id)
            .OrderBy(m => m.FechaHoraEnvio)
            .ToListAsync();

        var historialDtos = mensajesChat.Select(m => new MensajeChatDto
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
        }).ToList();

        return Ok(new SolicitudOperacionDto
        {
            Id = s.Id,
            CodigoSolicitud = s.CodigoSolicitud,
            ClienteEmpresaId = s.ClienteEmpresaId,
            ClienteRazonSocial = s.ClienteEmpresa?.RazonSocial ?? "N/A",
            DarsenaId = s.DarsenaId,
            DarsenaCodigo = s.Darsena?.Codigo ?? $"Dársena #{s.DarsenaId}",
            TipoOperacion = s.TipoOperacion,
            ModalidadTransporte = s.ModalidadTransporte,
            ConductorAsignadoId = s.ConductorAsignadoId,
            ConductorNombreCompleto = s.ConductorAsignado != null ? $"{s.ConductorAsignado.Nombre} {s.ConductorAsignado.Apellido}" : "Sin asignar",
            ConductorPatente = s.ConductorAsignado?.PatenteCamion,
            ResponsableDepositoId = s.ResponsableDepositoId,
            ResponsableDepositoNombre = s.ResponsableDeposito != null ? $"{s.ResponsableDeposito.Nombre} {s.ResponsableDeposito.Apellido}" : "Sin asignar",
            DescripcionMercaderia = s.DescripcionMercaderia,
            PesoTotalKg = s.PesoTotalKg,
            VolumenTotalM3 = s.VolumenTotalM3,
            DistanciaEstimadaKm = s.DistanciaEstimadaKm,
            FechaHoraTurnoProgramado = s.FechaHoraTurnoProgramado,
            FechaHoraIngresoReal = s.FechaHoraIngresoReal,
            FechaHoraEgresoReal = s.FechaHoraEgresoReal,
            Estado = s.Estado,
            ViajeId = s.Viaje?.Id,
            EstadoViaje = s.Viaje?.EstadoViaje,
            ViajeInfo = s.Viaje != null ? new ViajeDto
            {
                Id = s.Viaje.Id,
                SolicitudOperacionId = s.Id,
                CodigoSolicitud = s.CodigoSolicitud,
                ConductorId = s.Viaje.ConductorId,
                ConductorNombre = s.ConductorAsignado != null ? s.ConductorAsignado.NombreCompleto : "Sin asignar",
                PatenteCamion = s.ConductorAsignado != null ? s.ConductorAsignado.PatenteCamion : "",
                Origen = s.Viaje.Origen,
                Destino = s.Viaje.Destino,
                PuertaIngresoAsignada = s.Viaje.PuertaIngresoAsignada,
                EstadoViaje = s.Viaje.EstadoViaje,
                UltimaLatitud = s.Viaje.UltimaLatitud,
                UltimaLongitud = s.Viaje.UltimaLongitud,
                UltimaActualizacionGps = s.Viaje.UltimaActualizacionGps,
                ObservacionesChofer = s.Viaje.ObservacionesChofer
            } : null,
            LoteInventarioId = s.LoteInventarioId,
            LoteCodigo = s.LoteInventario?.CodigoLote,
            CantidadRetirada = s.CantidadRetirada,
            CiudadOrigen = s.CiudadOrigen,
            CiudadDestino = s.CiudadDestino,
            FechaSalidaCliente = s.FechaSalidaCliente,
            ObservacionesOperador = s.ObservacionesOperador,
            HistorialChat = historialDtos
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.Cliente)]
    public async Task<ActionResult<SolicitudOperacionDto>> CrearSolicitud([FromBody] CrearSolicitudDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int clienteId = dto.ClienteEmpresaId;
        if (User.IsInRole(Roles.Cliente))
        {
            var claimClienteId = User.FindFirstValue("ClienteId");
            if (int.TryParse(claimClienteId, out int idPropio))
            {
                clienteId = idPropio;
            }
        }

        // Validar que la dársena pertenezca a un contrato activo del cliente
        var contratoActivo = await _db.ContratosAlquilerDarsena
            .AnyAsync(c => c.ClienteEmpresaId == clienteId &&
                           c.DarsenaId == dto.DarsenaId &&
                           c.Estado == EstadoContratoDarsena.Activo);

        if (!contratoActivo)
        {
            return BadRequest(new { Mensaje = "La dársena seleccionada no posee un contrato de alquiler activo con su empresa." });
        }

        var darsena = await _db.Darsenas.FindAsync(dto.DarsenaId);
        if (darsena == null) return BadRequest(new { Mensaje = "Dársena no encontrada." });

        // Validar límite de espacio/volumen de la dársena para Arribos / Ingresos
        if (dto.TipoOperacion == TipoOperacionSolicitud.ArriboIngreso)
        {
            decimal volumenActual = await _db.LotesInventario
                .Where(l => l.DarsenaId == dto.DarsenaId)
                .SumAsync(l => (decimal?)l.VolumenM3) ?? 0m;

            decimal disponible = Math.Max(0, darsena.CapacidadMaximaM3 - volumenActual);

            if (volumenActual >= darsena.CapacidadMaximaM3)
            {
                return BadRequest(new { Mensaje = $"La dársena {darsena.Codigo} ya se encuentra al 100% de su capacidad máxima ({darsena.CapacidadMaximaM3:N0} m³). No se pueden realizar solicitudes de ingreso hasta despachar mercadería." });
            }

            if (dto.VolumenTotalM3 > disponible)
            {
                return BadRequest(new { Mensaje = $"El volumen solicitado ({dto.VolumenTotalM3:N2} m³) supera la capacidad disponible en la dársena {darsena.Codigo} ({disponible:N2} m³ disponibles de {darsena.CapacidadMaximaM3:N0} m³ totales). No es posible sobrepasar el límite máximo." });
            }
        }

        // Lógica de Retiro desde Stock (si es Despacho / Retiro)
        if (dto.TipoOperacion == TipoOperacionSolicitud.DespachoRetiro && dto.LoteInventarioId.HasValue)
        {
            var lote = await _db.LotesInventario.FirstOrDefaultAsync(l => l.Id == dto.LoteInventarioId.Value && l.ClienteEmpresaId == clienteId);
            if (lote == null)
            {
                return BadRequest(new { Mensaje = "El lote de inventario seleccionado no existe o no pertenece a su empresa." });
            }

            int cant = dto.CantidadRetirada ?? 1;
            if (cant <= 0 || cant > lote.CantidadUnidades)
            {
                return BadRequest(new { Mensaje = $"La cantidad a retirar ({cant}) debe ser mayor a 0 y no superar el stock disponible ({lote.CantidadUnidades} u.)." });
            }

            // Si no se pasaron peso o volumen específicos, prorratear automáticamente
            if (dto.PesoTotalKg <= 0 && lote.CantidadUnidades > 0)
            {
                dto.PesoTotalKg = Math.Round((decimal)cant / lote.CantidadUnidades * lote.PesoKg, 2);
            }
            if (dto.VolumenTotalM3 <= 0 && lote.CantidadUnidades > 0)
            {
                dto.VolumenTotalM3 = Math.Round((decimal)cant / lote.CantidadUnidades * lote.VolumenM3, 2);
            }
            if (string.IsNullOrWhiteSpace(dto.DescripcionMercaderia))
            {
                dto.DescripcionMercaderia = $"Retiro de {cant} u. de {lote.ProductoDescripcion} (Lote: {lote.CodigoLote})";
            }
        }

        // Configuración de rutas pivotadas con Santa Rosa, La Pampa
        string ciudadOrigen;
        string ciudadDestino;
        if (dto.TipoOperacion == TipoOperacionSolicitud.ArriboIngreso)
        {
            ciudadOrigen = string.IsNullOrWhiteSpace(dto.CiudadOrigen) ? "Buenos Aires (CABA)" : dto.CiudadOrigen;
            ciudadDestino = "Santa Rosa, La Pampa (Predio Central LCP)";
        }
        else
        {
            ciudadOrigen = "Santa Rosa, La Pampa (Predio Central LCP)";
            ciudadDestino = string.IsNullOrWhiteSpace(dto.CiudadDestino) ? "Buenos Aires (CABA)" : dto.CiudadDestino;
        }

        var maxId = await _db.SolicitudesOperacion.OrderByDescending(s => s.Id).Select(s => s.Id).FirstOrDefaultAsync();
        string codigo = $"#{maxId + 1049}";

        var solicitud = new SolicitudOperacion
        {
            CodigoSolicitud = codigo,
            ClienteEmpresaId = clienteId,
            DarsenaId = dto.DarsenaId,
            TipoOperacion = dto.TipoOperacion,
            ModalidadTransporte = dto.ModalidadTransporte,
            ConductorAsignadoId = dto.ModalidadTransporte == ModalidadTransporte.ChoferPropio ? dto.ConductorAsignadoId : null,
            DescripcionMercaderia = dto.DescripcionMercaderia,
            PesoTotalKg = dto.PesoTotalKg,
            VolumenTotalM3 = dto.VolumenTotalM3,
            DistanciaEstimadaKm = dto.DistanciaEstimadaKm,
            FechaHoraTurnoProgramado = dto.FechaHoraTurnoProgramado,
            Estado = EstadoSolicitud.Pendiente, // Espera que el operador confirme y programe el turno
            LoteInventarioId = dto.LoteInventarioId,
            CantidadRetirada = dto.CantidadRetirada,
            CiudadOrigen = ciudadOrigen,
            CiudadDestino = ciudadDestino,
            FechaSalidaCliente = dto.FechaSalidaCliente ?? DateTime.UtcNow
        };

        _db.SolicitudesOperacion.Add(solicitud);

        // Si la dársena está libre, actualizar aviso preliminar de turno en espera de confirmación
        if (darsena.EstadoActual == EstadoDarsena.Libre)
        {
            darsena.ProximoTurnoHora = dto.FechaHoraTurnoProgramado.ToString("HH:mm");
            darsena.ProximoTurnoDetalle = $"{dto.TipoOperacion} - {dto.DescripcionMercaderia} (Pendiente confirmación)";
        }

        await _db.SaveChangesAsync();

        // Si ya tiene conductor asignado (ej. chofer propio seleccionado por el cliente), crear el Viaje inicial
        if (solicitud.ConductorAsignadoId.HasValue && solicitud.ConductorAsignadoId.Value > 0)
        {
            var viajeInicial = new Viaje
            {
                SolicitudOperacionId = solicitud.Id,
                ConductorId = solicitud.ConductorAsignadoId.Value,
                Origen = solicitud.CiudadOrigen ?? "Base LCP Santa Rosa",
                Destino = solicitud.CiudadDestino ?? "Santa Rosa, La Pampa",
                PuertaIngresoAsignada = "Calle San Martín 245",
                EstadoViaje = EstadoViaje.Asignado
            };
            _db.Viajes.Add(viajeInicial);
            await _db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetSolicitud), new { id = solicitud.Id }, new SolicitudOperacionDto
        {
            Id = solicitud.Id,
            CodigoSolicitud = solicitud.CodigoSolicitud,
            ClienteEmpresaId = solicitud.ClienteEmpresaId,
            DarsenaId = solicitud.DarsenaId,
            TipoOperacion = solicitud.TipoOperacion,
            ModalidadTransporte = solicitud.ModalidadTransporte,
            DescripcionMercaderia = solicitud.DescripcionMercaderia,
            PesoTotalKg = solicitud.PesoTotalKg,
            VolumenTotalM3 = solicitud.VolumenTotalM3,
            DistanciaEstimadaKm = solicitud.DistanciaEstimadaKm,
            FechaHoraTurnoProgramado = solicitud.FechaHoraTurnoProgramado,
            Estado = solicitud.Estado,
            LoteInventarioId = solicitud.LoteInventarioId,
            CantidadRetirada = solicitud.CantidadRetirada,
            CiudadOrigen = solicitud.CiudadOrigen,
            CiudadDestino = solicitud.CiudadDestino,
            FechaSalidaCliente = solicitud.FechaSalidaCliente
        });
    }

    [HttpPut("{id}/asignar")]
    [Authorize(Roles = "Operador,Administrador")]
    public async Task<ActionResult> AsignarOperador([FromBody] AsignarSolicitudDto dto)
    {
        var solicitud = await _db.SolicitudesOperacion
            .Include(s => s.Darsena)
            .Include(s => s.ClienteEmpresa)
            .FirstOrDefaultAsync(s => s.Id == dto.SolicitudId);

        if (solicitud == null) return NotFound();

        // Validar que la dársena pertenezca obligatoriamente a un contrato activo del cliente solicitante (no se puede operar en dársena de otro cliente ni deshabilitada)
        int darsenaAValidar = dto.DarsenaId.HasValue && dto.DarsenaId.Value > 0 ? dto.DarsenaId.Value : solicitud.DarsenaId;

        var tieneContratoActivo = await _db.ContratosAlquilerDarsena.AnyAsync(c =>
            c.ClienteEmpresaId == solicitud.ClienteEmpresaId &&
            c.DarsenaId == darsenaAValidar &&
            c.Estado == EstadoContratoDarsena.Activo &&
            c.FechaFin >= DateTime.UtcNow);

        if (!tieneContratoActivo)
        {
            return BadRequest(new { Mensaje = "No puede asignar una dársena que no pertenezca a un contrato de alquiler activo del cliente solicitante ni a una dársena deshabilitada." });
        }

        solicitud.DarsenaId = darsenaAValidar;

        var darsena = await _db.Darsenas.FindAsync(darsenaAValidar);

        // Validar límite de volumen de la dársena para Arribos / Ingresos
        if (solicitud.TipoOperacion == TipoOperacionSolicitud.ArriboIngreso)
        {
            if (darsena != null)
            {
                decimal volumenActual = await _db.LotesInventario
                    .Where(l => l.DarsenaId == darsenaAValidar)
                    .SumAsync(l => (decimal?)l.VolumenM3) ?? 0m;

                decimal disponible = Math.Max(0, darsena.CapacidadMaximaM3 - volumenActual);

                if (volumenActual >= darsena.CapacidadMaximaM3)
                {
                    return BadRequest(new { Mensaje = $"No es posible confirmar la solicitud: la dársena {darsena.Codigo} ya se encuentra al 100% de su capacidad máxima ({darsena.CapacidadMaximaM3:N0} m³)." });
                }

                if (solicitud.VolumenTotalM3 > disponible)
                {
                    return BadRequest(new { Mensaje = $"No es posible confirmar la solicitud: el volumen del arribo ({solicitud.VolumenTotalM3:N2} m³) supera la capacidad disponible en la dársena {darsena.Codigo} ({disponible:N2} m³ disponibles de {darsena.CapacidadMaximaM3:N0} m³ totales)." });
                }
            }
        }

        // Si la solicitud es con Chofer Propio del Cliente: SE RESPETA EL CHOFER ELEGIDO POR EL CLIENTE (no se reemplaza)
        if (solicitud.ModalidadTransporte == ModalidadTransporte.ChoferPropio)
        {
            // Mantiene el chofer propio ya asignado por el cliente en la creación
            if (solicitud.ConductorAsignadoId.HasValue && solicitud.ConductorAsignadoId.Value > 0)
            {
                var viajeExistente = await _db.Viajes.FirstOrDefaultAsync(v => v.SolicitudOperacionId == solicitud.Id);
                if (viajeExistente == null)
                {
                    var viajePropio = new Viaje
                    {
                        SolicitudOperacionId = solicitud.Id,
                        ConductorId = solicitud.ConductorAsignadoId.Value,
                        Origen = solicitud.CiudadOrigen ?? "Establecimiento Cliente",
                        Destino = solicitud.CiudadDestino ?? "Santa Rosa, La Pampa (Predio Central LCP)",
                        PuertaIngresoAsignada = "Calle San Martín 245",
                        EstadoViaje = EstadoViaje.Asignado
                    };
                    _db.Viajes.Add(viajePropio);
                }
            }
        }
        else
        {
            // Modalidad Flota LCP: el operador asigna obligatoriamente el chofer de la dotación oficial de LCP
            if (dto.ConductorId.HasValue && dto.ConductorId.Value > 0)
            {
                solicitud.ConductorAsignadoId = dto.ConductorId.Value;

                // Crear o actualizar viaje para el conductor designado de Flota LCP
                var viaje = await _db.Viajes.FirstOrDefaultAsync(v => v.SolicitudOperacionId == solicitud.Id);
                if (viaje == null)
                {
                    viaje = new Viaje
                    {
                        SolicitudOperacionId = solicitud.Id,
                        ConductorId = dto.ConductorId.Value,
                        Origen = solicitud.CiudadOrigen ?? "Base Central LCP Santa Rosa",
                        Destino = solicitud.CiudadDestino ?? "Destino Cliente",
                        PuertaIngresoAsignada = "Calle San Martín 245",
                        EstadoViaje = EstadoViaje.Asignado
                    };
                    _db.Viajes.Add(viaje);
                }
                else
                {
                    viaje.ConductorId = dto.ConductorId.Value;
                    viaje.Origen = solicitud.CiudadOrigen ?? viaje.Origen;
                    viaje.Destino = solicitud.CiudadDestino ?? viaje.Destino;
                }
            }
        }

        if (dto.ResponsableDepositoId.HasValue && dto.ResponsableDepositoId.Value > 0)
        {
            solicitud.ResponsableDepositoId = dto.ResponsableDepositoId.Value;
        }

        if (dto.FechaHoraTurnoProgramado.HasValue)
        {
            solicitud.FechaHoraTurnoProgramado = dto.FechaHoraTurnoProgramado.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.ObservacionesOperador))
        {
            solicitud.ObservacionesOperador = dto.ObservacionesOperador;
        }

        solicitud.Estado = EstadoSolicitud.Programada;

        // Actualizar datos de próximo turno en dársena
        if (darsena != null)
        {
            darsena.ProximoTurnoHora = solicitud.FechaHoraTurnoProgramado.ToString("HH:mm");
            darsena.ProximoTurnoDetalle = $"{solicitud.TipoOperacion} - {solicitud.DescripcionMercaderia} ({solicitud.CodigoSolicitud})";
            if (darsena.EstadoActual == EstadoDarsena.Libre)
            {
                darsena.EstadoActual = EstadoDarsena.TurnoAsignado;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Turno programado para el {solicitud.FechaHoraTurnoProgramado:dd/MM/yyyy HH:mm} hs y asignado correctamente." });
    }

    [HttpPut("{id}/estado")]
    [Authorize(Roles = "Operador,Administrador,Conductor")]
    public async Task<ActionResult> CambiarEstado(int id, [FromBody] EstadoSolicitud nuevoEstado)
    {
        var solicitud = await _db.SolicitudesOperacion
            .Include(s => s.Darsena)
            .Include(s => s.ConductorAsignado)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        solicitud.Estado = nuevoEstado;

        if (nuevoEstado == EstadoSolicitud.EnCurso)
        {
            solicitud.FechaHoraIngresoReal = DateTime.UtcNow;
            if (solicitud.Darsena != null)
            {
                solicitud.Darsena.EstadoActual = EstadoDarsena.Ocupada;
                solicitud.Darsena.DetalleOperacionActual = $"{solicitud.CodigoSolicitud} · {solicitud.ConductorAsignado?.NombreCompleto ?? "En muelle"}";
                await _patioHub.Clients.All.SendAsync("DarsenaActualizada", new DarsenaDto
                {
                    Id = solicitud.Darsena.Id,
                    NumeroDarsena = solicitud.Darsena.NumeroDarsena,
                    Codigo = solicitud.Darsena.Codigo,
                    EstadoActual = solicitud.Darsena.EstadoActual,
                    ClienteAlquilerActualId = solicitud.Darsena.ClienteAlquilerActualId,
                    DetalleOperacionActual = solicitud.Darsena.DetalleOperacionActual
                });
            }
        }
        else if (nuevoEstado == EstadoSolicitud.Completada || nuevoEstado == EstadoSolicitud.Cancelada)
        {
            string convId = $"GRP-SOL-{solicitud.Id}";
            var msgCierre = new MensajeChat
            {
                ConversacionId = convId,
                RemitenteUsuarioId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid) ? uid : 1,
                SolicitudOperacionId = solicitud.Id,
                TextoMensaje = $"🔒 SALA FINALIZADA: La operación pasó a estado {nuevoEstado}. La sala de chat tripartita queda cerrada y archivada.",
                FechaHoraEnvio = DateTime.UtcNow,
                Leido = true
            };
            _db.MensajesChat.Add(msgCierre);
            await _chatHub.Clients.Group(convId).SendAsync("ChatFinalizado", new
            {
                ConversacionId = convId,
                SolicitudId = solicitud.Id,
                Motivo = $"Estado {nuevoEstado}"
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { Mensaje = "Estado actualizado." });
    }

    [HttpPut("{id}/viaje")]
    [HttpPost("{id}/viaje")]
    [Authorize(Roles = "Conductor")]
    public async Task<ActionResult> ActualizarViaje(int id, [FromBody] ActualizarViajeRequestDto dto)
    {
        var solicitud = await _db.SolicitudesOperacion
            .Include(s => s.Darsena)
            .Include(s => s.ConductorAsignado)
            .Include(s => s.Viaje)
            .Include(s => s.ClienteEmpresa)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        // RBAC check estricto: Solo el Conductor asignado a esta solicitud puede registrar hitos
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (int.TryParse(userIdStr, out int userId))
        {
            var cond = await _db.Conductores.FirstOrDefaultAsync(c => c.UsuarioId == userId || (userEmail != null && c.Email == userEmail));
            if (cond == null || solicitud.ConductorAsignadoId != cond.Id)
            {
                return Forbid();
            }
        }
        else
        {
            return Forbid();
        }

        if (solicitud.Viaje == null)
        {
            solicitud.Viaje = new Viaje
            {
                SolicitudOperacionId = solicitud.Id,
                ConductorId = solicitud.ConductorAsignadoId ?? 1,
                Origen = solicitud.CiudadOrigen ?? "Base LCP Santa Rosa",
                Destino = solicitud.CiudadDestino ?? "Santa Rosa, La Pampa",
                PuertaIngresoAsignada = "Calle San Martín 245",
                EstadoViaje = dto.EstadoViaje
            };
            _db.Viajes.Add(solicitud.Viaje);
        }
        else
        {
            solicitud.Viaje.EstadoViaje = dto.EstadoViaje;
        }

        if (dto.Latitud.HasValue && dto.Longitud.HasValue)
        {
            solicitud.Viaje.UltimaLatitud = dto.Latitud.Value;
            solicitud.Viaje.UltimaLongitud = dto.Longitud.Value;
            solicitud.Viaje.UltimaActualizacionGps = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(dto.Observaciones))
        {
            solicitud.Viaje.ObservacionesChofer = dto.Observaciones;
        }

        // Sincronizar estado operativo de la solicitud según el hito del viaje
        if (dto.EstadoViaje == EstadoViaje.EnRuta && solicitud.Estado == EstadoSolicitud.Programada)
        {
            solicitud.Estado = EstadoSolicitud.EnCurso;
        }
        else if (dto.EstadoViaje == EstadoViaje.EnPredioDarsena)
        {
            solicitud.Estado = EstadoSolicitud.EnCurso;
            solicitud.FechaHoraIngresoReal ??= DateTime.UtcNow;
            if (solicitud.Darsena != null)
            {
                solicitud.Darsena.EstadoActual = EstadoDarsena.Ocupada;
                solicitud.Darsena.DetalleOperacionActual = $"{solicitud.CodigoSolicitud} · Camión en dársena ({solicitud.ConductorAsignado?.NombreCompleto ?? "Conductor"})";
                await _patioHub.Clients.All.SendAsync("DarsenaActualizada", new DarsenaDto
                {
                    Id = solicitud.Darsena.Id,
                    NumeroDarsena = solicitud.Darsena.NumeroDarsena,
                    Codigo = solicitud.Darsena.Codigo,
                    EstadoActual = solicitud.Darsena.EstadoActual,
                    ClienteAlquilerActualId = solicitud.Darsena.ClienteAlquilerActualId,
                    DetalleOperacionActual = solicitud.Darsena.DetalleOperacionActual
                });
            }
        }

        // REGLA FUNDAMENTAL DE NEGOCIO Y FLUJO OPERATIVO:
        // En GUARDADO / INGRESO DE PRODUCTOS (TipoOperacionSolicitud.ArriboDescarga):
        // EL CONDUCTOR NUNCA FINALIZA LA SOLICITUD.
        // El conductor reporta su arribo a LCP y posicionamiento en dársena (Paso 5 Flota LCP o Paso 3 Chofer Propio).
        // El camión queda en EstadoViaje.EnPredioDarsena, la dársena en EstadoDarsena.Ocupada, y la solicitud en EstadoSolicitud.EnCurso.
        // Es EXCLUSIVAMENTE el OPERADOR DE PATIO quien descarga físicamente los productos, registra el stock nuevo
        // en LotesInventario (dando stock a la empresa cliente) y confirma la descarga a través de CompletarDescarga.
        // Dicho método genera la liquidación automática y finaliza la solicitud.
        //
        // En DESPACHO / RETIRO (TipoOperacionSolicitud.DespachoRetiro):
        // El operador primero despacha el camión desde LCP. Luego el camionero realiza el viaje y finaliza la misión
        // al entregar en destino (Chofer Propio Paso 4) o al retornar a Base LCP (Flota LCP Paso 5).
        bool esDespacho = solicitud.TipoOperacion == TipoOperacionSolicitud.DespachoRetiro;
        bool esMisionTerminada = false;

        if (esDespacho)
        {
            esMisionTerminada = dto.EstadoViaje == EstadoViaje.DescargadoCompletado
                || (!string.IsNullOrWhiteSpace(dto.HitoAlcanzado) && (
                    dto.HitoAlcanzado.Contains("misión terminada", StringComparison.OrdinalIgnoreCase) ||
                    dto.HitoAlcanzado.Contains("mision terminada", StringComparison.OrdinalIgnoreCase) ||
                    (solicitud.ModalidadTransporte == ModalidadTransporte.FlotaLCP && dto.HitoAlcanzado.Contains("Paso 5", StringComparison.OrdinalIgnoreCase)) ||
                    (solicitud.ModalidadTransporte != ModalidadTransporte.FlotaLCP && dto.HitoAlcanzado.Contains("Paso 4", StringComparison.OrdinalIgnoreCase))
                ));
        }
        else
        {
            // En Guardado / Ingreso: El chofer atraca en dársena cuando llega a Base LCP
            // - Flota LCP: Arriba a dársena en el Paso 5 (retorno a LCP). Los pasos 1, 2, 3, 4 son salida, viaje y carga en origen.
            // - Chofer Propio: Arriba a dársena en el Paso 3.
            bool llegoADarsenaLCP = false;
            if (solicitud.ModalidadTransporte == ModalidadTransporte.FlotaLCP)
            {
                llegoADarsenaLCP = !string.IsNullOrWhiteSpace(dto.HitoAlcanzado) && (
                    dto.HitoAlcanzado.Contains("Paso 5", StringComparison.OrdinalIgnoreCase) ||
                    (dto.HitoAlcanzado.Contains("Calle San Martín", StringComparison.OrdinalIgnoreCase) && dto.HitoAlcanzado.Contains("dársena", StringComparison.OrdinalIgnoreCase))
                );
            }
            else
            {
                llegoADarsenaLCP = !string.IsNullOrWhiteSpace(dto.HitoAlcanzado) && (
                    dto.HitoAlcanzado.Contains("Paso 3", StringComparison.OrdinalIgnoreCase) ||
                    dto.HitoAlcanzado.Contains("dársena", StringComparison.OrdinalIgnoreCase) ||
                    dto.HitoAlcanzado.Contains("darcena", StringComparison.OrdinalIgnoreCase) ||
                    dto.HitoAlcanzado.Contains("predio", StringComparison.OrdinalIgnoreCase)
                );
            }

            if (llegoADarsenaLCP)
            {
                solicitud.Viaje.EstadoViaje = EstadoViaje.EnPredioDarsena;
                solicitud.Estado = EstadoSolicitud.EnCurso;
                solicitud.FechaHoraIngresoReal ??= DateTime.UtcNow;

                if (solicitud.Darsena != null)
                {
                    solicitud.Darsena.EstadoActual = EstadoDarsena.Ocupada;
                    solicitud.Darsena.DetalleOperacionActual = $"{solicitud.CodigoSolicitud} · Camión en dársena para descarga ({solicitud.ConductorAsignado?.NombreCompleto ?? "Conductor"})";
                    await _patioHub.Clients.All.SendAsync("DarsenaActualizada", new DarsenaDto
                    {
                        Id = solicitud.Darsena.Id,
                        NumeroDarsena = solicitud.Darsena.NumeroDarsena,
                        Codigo = solicitud.Darsena.Codigo,
                        EstadoActual = solicitud.Darsena.EstadoActual,
                        ClienteAlquilerActualId = solicitud.Darsena.ClienteAlquilerActualId,
                        DetalleOperacionActual = solicitud.Darsena.DetalleOperacionActual
                    });
                }
            }
            else
            {
                // El viaje sigue en curso en la ruta o en cliente
                solicitud.Viaje.EstadoViaje = EstadoViaje.EnRuta;
                solicitud.Estado = EstadoSolicitud.EnCurso;
            }
        }

        if (esMisionTerminada)
        {
            solicitud.Estado = EstadoSolicitud.Completada;
            solicitud.FechaHoraEgresoReal ??= DateTime.UtcNow;
            solicitud.Viaje.EstadoViaje = EstadoViaje.DescargadoCompletado;

            if (solicitud.Darsena != null && solicitud.Darsena.EstadoActual == EstadoDarsena.Ocupada)
            {
                solicitud.Darsena.EstadoActual = EstadoDarsena.Libre;
                solicitud.Darsena.ProximoTurnoHora = null;
                solicitud.Darsena.ProximoTurnoDetalle = null;
                solicitud.Darsena.DetalleOperacionActual = solicitud.Darsena.ClienteAlquilerActualId.HasValue
                    ? $"Alquilada por {solicitud.ClienteEmpresa?.RazonSocial ?? "Cliente"} · Muelle libre para maniobras"
                    : "Disponible ahora (Sin alquiler)";
            }
        }

        // Registrar hito en el chat tripartito de la operación
        string hitoTexto = !string.IsNullOrWhiteSpace(dto.HitoAlcanzado) ? dto.HitoAlcanzado : dto.EstadoViaje.ToString();
        string convId = $"GRP-SOL-{solicitud.Id}";
        int remitenteId = 1;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int rId)) remitenteId = rId;

        string obsTexto = !string.IsNullOrWhiteSpace(dto.Observaciones) ? $" · Nota: {dto.Observaciones}" : "";
        var msgTracking = new MensajeChat
        {
            ConversacionId = convId,
            RemitenteUsuarioId = remitenteId,
            SolicitudOperacionId = solicitud.Id,
            TextoMensaje = $"🚛 [HOJA DE RUTA] {hitoTexto}{obsTexto}",
            FechaHoraEnvio = DateTime.UtcNow,
            EsIncidenteRuta = dto.EstadoViaje == EstadoViaje.ConIncidenteDemora,
            Leido = false
        };
        _db.MensajesChat.Add(msgTracking);

        if (esMisionTerminada)
        {
            var msgCierre = new MensajeChat
            {
                ConversacionId = convId,
                RemitenteUsuarioId = remitenteId,
                SolicitudOperacionId = solicitud.Id,
                TextoMensaje = "🏁 SALA FINALIZADA: El conductor ha confirmado la conclusión del viaje y finalización de la operación. Misión terminada con éxito. Chat cerrado y archivado.",
                FechaHoraEnvio = DateTime.UtcNow,
                Leido = true
            };
            _db.MensajesChat.Add(msgCierre);

            await _chatHub.Clients.Group(convId).SendAsync("ChatFinalizado", new
            {
                ConversacionId = convId,
                SolicitudId = solicitud.Id,
                Motivo = "Misión concluida exitosamente por el conductor"
            });
        }

        await _db.SaveChangesAsync();

        await _patioHub.Clients.All.SendAsync("ViajeActualizado", new
        {
            SolicitudId = solicitud.Id,
            EstadoViaje = dto.EstadoViaje,
            Hito = hitoTexto,
            Lat = dto.Latitud,
            Lng = dto.Longitud
        });

        return Ok(new { Mensaje = "Viaje actualizado correctamente.", EstadoViaje = dto.EstadoViaje.ToString() });
    }

    [HttpPost("{id}/cancelar")]
    [Authorize(Roles = "Operador,Administrador,Cliente")]
    public async Task<ActionResult> CancelarSolicitud(int id, [FromBody] CancelarSolicitudDto? dto)
    {
        var solicitud = await _db.SolicitudesOperacion
            .Include(s => s.Darsena)
            .Include(s => s.ConductorAsignado)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        // Si es cliente, validar que sea el titular de la solicitud
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId) && solicitud.ClienteEmpresaId != clienteId)
            {
                return Forbid();
            }
        }

        if (solicitud.Estado == EstadoSolicitud.Completada)
        {
            return BadRequest(new { Mensaje = "No se puede cancelar una solicitud que ya ha sido completada." });
        }

        string motivo = !string.IsNullOrWhiteSpace(dto?.Motivo) ? dto.Motivo : "Sin motivo especificado";
        bool eraPendiente = solicitud.Estado == EstadoSolicitud.Pendiente;
        solicitud.Estado = EstadoSolicitud.Cancelada;

        if (User.IsInRole(Roles.Operador) || User.IsInRole(Roles.Administrador))
        {
            string prefijo = eraPendiente ? "Rechazada por Operador" : "Cancelada por Operador";
            solicitud.ObservacionesOperador = string.IsNullOrWhiteSpace(solicitud.ObservacionesOperador)
                ? $"{prefijo}: {motivo}"
                : $"{solicitud.ObservacionesOperador} | {prefijo}: {motivo}";
        }
        else
        {
            solicitud.ObservacionesOperador = string.IsNullOrWhiteSpace(solicitud.ObservacionesOperador)
                ? $"Cancelada por el Cliente: {motivo}"
                : $"{solicitud.ObservacionesOperador} | Cancelada por Cliente: {motivo}";
        }

        if (solicitud.Darsena != null)
        {
            solicitud.Darsena.EstadoActual = EstadoDarsena.Libre;
            solicitud.Darsena.ProximoTurnoHora = null;
            solicitud.Darsena.ProximoTurnoDetalle = null;
            solicitud.Darsena.DetalleOperacionActual = solicitud.Darsena.ClienteAlquilerActualId.HasValue
                ? "Alquilada por Almacén El Molino S.A. · Muelle libre para operar"
                : "Disponible ahora (Sin alquiler)";

            await _patioHub.Clients.All.SendAsync("DarsenaActualizada", new DarsenaDto
            {
                Id = solicitud.Darsena.Id,
                NumeroDarsena = solicitud.Darsena.NumeroDarsena,
                Codigo = solicitud.Darsena.Codigo,
                EstadoActual = solicitud.Darsena.EstadoActual,
                ClienteAlquilerActualId = solicitud.Darsena.ClienteAlquilerActualId,
                DetalleOperacionActual = solicitud.Darsena.DetalleOperacionActual
            });
        }

        var viaje = await _db.Viajes.FirstOrDefaultAsync(v => v.SolicitudOperacionId == solicitud.Id);
        if (viaje != null)
        {
            _db.Viajes.Remove(viaje);
        }

        // Finalizar y archivar chat tripartito de la operación
        string convId = $"GRP-SOL-{solicitud.Id}";
        int userIdRemitente = 1;
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(uidStr, out int parsedUid)) userIdRemitente = parsedUid;

        var msgCierre = new MensajeChat
        {
            ConversacionId = convId,
            RemitenteUsuarioId = userIdRemitente,
            SolicitudOperacionId = solicitud.Id,
            TextoMensaje = $"🔒 SALA FINALIZADA: La operación fue {(eraPendiente ? "rechazada" : "cancelada")} ({solicitud.ObservacionesOperador}). El chat ha quedado cerrado y archivado en los detalles del pedido.",
            FechaHoraEnvio = DateTime.UtcNow,
            Leido = true
        };
        _db.MensajesChat.Add(msgCierre);

        await _db.SaveChangesAsync();

        await _chatHub.Clients.Group(convId).SendAsync("ChatFinalizado", new
        {
            ConversacionId = convId,
            SolicitudId = solicitud.Id,
            Motivo = solicitud.ObservacionesOperador
        });

        return Ok(new { Mensaje = eraPendiente ? "La solicitud ha sido rechazada exitosamente." : "La operación ha sido cancelada exitosamente." });
    }

    [HttpPost("{id}/completar-descarga")]
    [Authorize(Roles = "Operador,Administrador")]
    public async Task<ActionResult> CompletarDescarga(int id, [FromBody] CompletarDescargaDto dto)
    {
        var solicitud = await _db.SolicitudesOperacion
            .Include(s => s.Darsena)
            .Include(s => s.ClienteEmpresa)
            .Include(s => s.Viaje)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int operadorId);
        if (operadorId == 0 || !await _db.Usuarios.AnyAsync(u => u.Id == operadorId))
        {
            operadorId = await _db.Usuarios.Where(u => u.Activo).Select(u => u.Id).FirstOrDefaultAsync();
        }

        int loteIdGenerado = 0;

        // Si es Retiro / Despacho: Deducir stock del lote existente (NO agregar stock)
        // Si es Arribo / Ingreso: Registrar el nuevo stock entrante en la dársena alquilada
        bool esDespacho = solicitud.TipoOperacion == TipoOperacionSolicitud.DespachoRetiro 
                       || dto.EsRetiro;

        // Validación estricta para Arribo / Ingreso: El camión debe haber reportado su llegada a LCP antes de descargar nuevo stock
        if (!esDespacho)
        {
            // Idempotencia: Si la solicitud ya está completada o ya existe un lote de inventario registrado, evitar duplicados
            var loteExistentePrevio = await _db.LotesInventario.FirstOrDefaultAsync(l => l.SolicitudIngresoId == solicitud.Id);
            if (solicitud.Estado == EstadoSolicitud.Completada || loteExistentePrevio != null)
            {
                if (loteExistentePrevio != null && solicitud.LoteInventarioId == null)
                {
                    solicitud.LoteInventarioId = loteExistentePrevio.Id;
                    await _db.SaveChangesAsync();
                }
                return BadRequest(new { Mensaje = "Esta solicitud de ingreso ya fue completada y su stock ya se encuentra ingresado en el depósito. No se puede volver a descargar." });
            }

            var viaje = solicitud.Viaje ?? await _db.Viajes.FirstOrDefaultAsync(v => v.SolicitudOperacionId == solicitud.Id);
            if (solicitud.Viaje == null && viaje != null)
            {
                solicitud.Viaje = viaje;
            }

            bool camionLlego = (solicitud.Estado == EstadoSolicitud.EnCurso) ||
                               (solicitud.FechaHoraIngresoReal.HasValue) ||
                               (viaje != null && (
                                   viaje.EstadoViaje >= EstadoViaje.EnPredioDarsena ||
                                   (viaje.ObservacionesChofer != null && (
                                       viaje.ObservacionesChofer.Contains("Paso 5") ||
                                       viaje.ObservacionesChofer.Contains("Paso 3") ||
                                       viaje.ObservacionesChofer.Contains("Paso 2: Llegada") ||
                                       viaje.ObservacionesChofer.Contains("Arrib") ||
                                       viaje.ObservacionesChofer.Contains("Lleg") ||
                                       viaje.ObservacionesChofer.Contains("posicionado", StringComparison.OrdinalIgnoreCase) ||
                                       viaje.ObservacionesChofer.Contains("San Martín", StringComparison.OrdinalIgnoreCase)
                                   ))
                               ));

            if (!camionLlego)
            {
                return BadRequest(new { Mensaje = "No se puede confirmar la descarga de nuevo stock: El camionero aún no ha indicado su llegada a las instalaciones de LCP en su hoja de ruta." });
            }
        }

        if (esDespacho)
        {
            int? idLoteRetiro = dto.LoteInventarioId ?? solicitud.LoteInventarioId;
            LoteInventario? loteExistente = null;

            if (idLoteRetiro.HasValue && idLoteRetiro.Value > 0)
            {
                loteExistente = await _db.LotesInventario.FindAsync(idLoteRetiro.Value);
            }

            // Si no se especificó o no se encontró, tomar el lote del cliente en esa dársena
            if (loteExistente == null)
            {
                loteExistente = await _db.LotesInventario
                    .Where(l => l.DarsenaId == solicitud.DarsenaId && l.ClienteEmpresaId == solicitud.ClienteEmpresaId)
                    .OrderByDescending(l => l.CantidadUnidades)
                    .FirstOrDefaultAsync();
            }

            int cantARetirar = dto.CantidadRetirada ?? dto.CantidadUnidades;
            if (cantARetirar <= 0) cantARetirar = solicitud.CantidadRetirada ?? 1;

            if (loteExistente != null)
            {
                // Descontar unidades y proporcionales de peso y volumen
                if (loteExistente.CantidadUnidades > 0)
                {
                    decimal proporcion = Math.Min(1m, (decimal)cantARetirar / loteExistente.CantidadUnidades);
                    loteExistente.PesoKg = Math.Max(0, Math.Round(loteExistente.PesoKg - (loteExistente.PesoKg * proporcion), 2));
                    loteExistente.VolumenM3 = Math.Max(0, Math.Round(loteExistente.VolumenM3 - (loteExistente.VolumenM3 * proporcion), 2));
                }
                loteExistente.CantidadUnidades = Math.Max(0, loteExistente.CantidadUnidades - cantARetirar);
                loteIdGenerado = loteExistente.Id;

                solicitud.LoteInventarioId = loteExistente.Id;
                solicitud.CantidadRetirada = cantARetirar;
            }

            // En Despacho: El operador carga los productos y despacha el camión a la ruta.
            // La solicitud permanece EnCurso mientras el camionero realiza el viaje y entrega final.
            solicitud.Estado = EstadoSolicitud.EnCurso;
            if (solicitud.Viaje != null)
            {
                solicitud.Viaje.EstadoViaje = EstadoViaje.EnRuta;
                bool esFlotaLcp = solicitud.ModalidadTransporte == ModalidadTransporte.FlotaLCP;
                solicitud.Viaje.ObservacionesChofer = esFlotaLcp
                    ? $"Paso 1: Salida a la ruta desde Base LCP hacia {solicitud.CiudadDestino ?? "destino"} con carga despachada"
                    : $"Paso 2: Mercadería cargada en dársena. Camión despachado a la ruta hacia {solicitud.CiudadDestino ?? "destino"}";
            }
        }
        else
        {
            decimal volAInsertar = dto.VolumenM3 > 0 ? dto.VolumenM3 : solicitud.VolumenTotalM3;
            var darsena = await _db.Darsenas.FindAsync(solicitud.DarsenaId);
            if (darsena != null)
            {
                decimal volumenActual = await _db.LotesInventario
                    .Where(l => l.DarsenaId == solicitud.DarsenaId)
                    .SumAsync(l => (decimal?)l.VolumenM3) ?? 0m;

                decimal disponible = Math.Max(0, darsena.CapacidadMaximaM3 - volumenActual);

                if (volAInsertar > disponible)
                {
                    return BadRequest(new { Mensaje = $"No se puede completar el ingreso: el volumen a almacenar ({volAInsertar:N2} m³) supera el espacio disponible en la dársena {darsena.Codigo} ({disponible:N2} m³ disponibles de {darsena.CapacidadMaximaM3:N0} m³ totales)." });
                }
            }

            // Arribo / Ingreso: Crear nuevo lote de inventario en la dársena alquilada (Regla 3)
            var lote = new LoteInventario
            {
                ClienteEmpresaId = solicitud.ClienteEmpresaId,
                DarsenaId = solicitud.DarsenaId,
                SolicitudIngresoId = solicitud.Id,
                CodigoLote = string.IsNullOrWhiteSpace(dto.CodigoLote) ? $"LOT-{DateTime.UtcNow:yyyyMMdd}-{solicitud.Id}" : dto.CodigoLote,
                ProductoDescripcion = string.IsNullOrWhiteSpace(dto.ProductoDescripcion) ? solicitud.DescripcionMercaderia : dto.ProductoDescripcion,
                CantidadUnidades = dto.CantidadUnidades > 0 ? dto.CantidadUnidades : 1,
                CantidadBultosCajas = dto.CantidadBultosCajas > 0 ? dto.CantidadBultosCajas : 1,
                PesoKg = dto.PesoKg > 0 ? dto.PesoKg : solicitud.PesoTotalKg,
                VolumenM3 = dto.VolumenM3 > 0 ? dto.VolumenM3 : solicitud.VolumenTotalM3,
                UbicacionInternaDarsena = string.IsNullOrWhiteSpace(dto.UbicacionInternaDarsena) ? "Muelle / Pasillo Central" : dto.UbicacionInternaDarsena,
                FechaIngreso = DateTime.UtcNow,
                OrdenadoPorOperadorId = operadorId
            };

            _db.LotesInventario.Add(lote);
            await _db.SaveChangesAsync();
            loteIdGenerado = lote.Id;

            // Asociar formalmente el lote generado a la solicitud
            solicitud.LoteInventarioId = lote.Id;

            // En Arribo / Ingreso, la descarga y guardado en dársena completa la operación en LCP
            solicitud.Estado = EstadoSolicitud.Completada;
            solicitud.FechaHoraEgresoReal = DateTime.UtcNow;
            if (solicitud.Viaje != null)
            {
                solicitud.Viaje.EstadoViaje = EstadoViaje.DescargadoCompletado;
                solicitud.Viaje.ObservacionesChofer = "Paso 6: Descarga física completada y nuevo stock almacenado en dársena por el Operador";
            }
        }

        // 3. Liberar el muelle de la dársena, pero PRESERVAR EL CONTRATO DE ALQUILER DEL CLIENTE
        if (solicitud.Darsena != null)
        {
            solicitud.Darsena.EstadoActual = EstadoDarsena.Libre;

            // Verificar si la dársena tiene un contrato activo vigente
            var contratoActivo = await _db.ContratosAlquilerDarsena
                .Include(c => c.ClienteEmpresa)
                .FirstOrDefaultAsync(c => c.DarsenaId == solicitud.Darsena.Id &&
                                           c.Estado == EstadoContratoDarsena.Activo &&
                                           c.FechaFin >= DateTime.UtcNow);

            if (contratoActivo != null)
            {
                solicitud.Darsena.ClienteAlquilerActualId = contratoActivo.ClienteEmpresaId;
                solicitud.Darsena.DetalleOperacionActual = $"Alquilada por {contratoActivo.ClienteEmpresa?.RazonSocial} · Muelle libre para maniobras";
            }
            else
            {
                solicitud.Darsena.ClienteAlquilerActualId = null;
                solicitud.Darsena.DetalleOperacionActual = "Disponible ahora (Sin alquiler)";
            }

            solicitud.Darsena.ProximoTurnoHora = null;
            solicitud.Darsena.ProximoTurnoDetalle = null;

            await _patioHub.Clients.All.SendAsync("DarsenaActualizada", new DarsenaDto
            {
                Id = solicitud.Darsena.Id,
                NumeroDarsena = solicitud.Darsena.NumeroDarsena,
                Codigo = solicitud.Darsena.Codigo,
                EstadoActual = solicitud.Darsena.EstadoActual,
                ClienteAlquilerActualId = solicitud.Darsena.ClienteAlquilerActualId,
                ClienteRazonSocial = contratoActivo?.ClienteEmpresa?.RazonSocial,
                DetalleOperacionActual = solicitud.Darsena.DetalleOperacionActual
            });
        }

        string convId = $"GRP-SOL-{solicitud.Id}";
        if (esDespacho)
        {
            // Notificar que se despachó el camión desde muelle LCP y continúa en ruta
            var msgDespacho = new MensajeChat
            {
                ConversacionId = convId,
                RemitenteUsuarioId = operadorId,
                SolicitudOperacionId = solicitud.Id,
                TextoMensaje = $"📦 [DESPACHO EN DÁRSENA] El operador cargó los productos y descontó el stock. El camión sale despachado desde Base LCP. El chofer continuará con la hoja de ruta hasta la entrega final.",
                FechaHoraEnvio = DateTime.UtcNow,
                Leido = false
            };
            _db.MensajesChat.Add(msgDespacho);
        }
        else
        {
            // Finalizar y archivar chat tripartito para Arribo
            var msgCierre = new MensajeChat
            {
                ConversacionId = convId,
                RemitenteUsuarioId = operadorId,
                SolicitudOperacionId = solicitud.Id,
                TextoMensaje = "🔒 SALA FINALIZADA: La descarga y guardado de stock fue completada con éxito en dársena. El chat tripartito ha sido finalizado y archivado en los detalles del pedido.",
                FechaHoraEnvio = DateTime.UtcNow,
                Leido = true
            };
            _db.MensajesChat.Add(msgCierre);

            await _chatHub.Clients.Group(convId).SendAsync("ChatFinalizado", new
            {
                ConversacionId = convId,
                SolicitudId = solicitud.Id,
                Motivo = "Descarga y guardado de stock completado en dársena"
            });
        }

        // 4. Regla 4: Generación 100% automática de la liquidación en estado PendienteDeAuditoria
        var liquidacion = await _liquidacionService.GenerarLiquidacionPorSolicitudCompletadaAsync(solicitud.Id);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Mensaje = esDespacho
                ? "Mercadería cargada y camión despachado a la ruta. Stock descontado del inventario." 
                : "Descarga completada y stock ordenado en dársena.",
            LoteId = loteIdGenerado,
            LiquidacionId = liquidacion.Id,
            LiquidacionComprobante = liquidacion.NumeroComprobante
        });
    }
}
