using System.ComponentModel.DataAnnotations;
using LCP.Shared.Enums;

namespace LCP.Shared.DTOs;

public class DarsenaDto
{
    public int Id { get; set; }
    public int NumeroDarsena { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int CapacidadMaximaPallets { get; set; }
    public decimal CapacidadMaximaM3 { get; set; }
    public EstadoDarsena EstadoActual { get; set; }
    public string EstadoNombre => EstadoActual.ToString();
    public int? ClienteAlquilerActualId { get; set; }
    public string? ClienteRazonSocial { get; set; }
    public string? DetalleOperacionActual { get; set; }
    public string? ProximoTurnoHora { get; set; }
    public string? ProximoTurnoDetalle { get; set; }
    public decimal VolumenOcupadoM3 { get; set; }
    public decimal VolumenDisponibleM3 => Math.Max(0, CapacidadMaximaM3 - VolumenOcupadoM3);
    public bool EstaLlena => CapacidadMaximaM3 > 0 && VolumenOcupadoM3 >= CapacidadMaximaM3;
}

public class ActualizarEstadoDarsenaDto
{
    public int DarsenaId { get; set; }
    public EstadoDarsena NuevoEstado { get; set; }
    public string? DetalleOperacion { get; set; }
}

public class ContratoAlquilerDarsenaDto
{
    public int Id { get; set; }
    public int ClienteEmpresaId { get; set; }
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public int DarsenaId { get; set; }
    public string DarsenaCodigo { get; set; } = string.Empty;
    public PlanAlquilerDarsena Plan { get; set; }
    public string PlanNombre => Plan.ToString();
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public decimal MontoTotalContrato { get; set; }
    public EstadoContratoDarsena Estado { get; set; }
    public string EstadoNombre => Estado switch
    {
        EstadoContratoDarsena.PendienteRevision => "Pendiente de Auditoría",
        EstadoContratoDarsena.AprobadoPendientePago => "Aprobado - Pendiente de Pago",
        EstadoContratoDarsena.FacturaCargadaPendienteHabilitacion => "Factura Cargada - Pendiente de Habilitación",
        EstadoContratoDarsena.Activo => "Habilitado / Activo",
        EstadoContratoDarsena.Vencido => "Vencido",
        EstadoContratoDarsena.Cancelado => "Cancelado",
        EstadoContratoDarsena.Rechazado => "Rechazado",
        _ => Estado.ToString()
    };
    public bool EstaVigente => Estado == EstadoContratoDarsena.Activo && FechaFin >= DateTime.UtcNow;
    public int CapacidadMaximaPallets { get; set; } = 30;
    public decimal CapacidadMaximaM3 { get; set; } = 120m;
    public decimal VolumenOcupadoM3 { get; set; }
    public decimal VolumenDisponibleM3 => Math.Max(0, CapacidadMaximaM3 - VolumenOcupadoM3);
    public bool EstaLlena => CapacidadMaximaM3 > 0 && VolumenOcupadoM3 >= CapacidadMaximaM3;

    public string MercaderiaAlmacenar { get; set; } = string.Empty;
    public string TipoAlmacenamiento { get; set; } = "Ambiente";
    public string? ConsideracionesEspeciales { get; set; }
    public string? ObservacionAuditor { get; set; }
    public string? ComprobantePagoUrl { get; set; }
    public DateTime? FechaAprobacionAuditor { get; set; }
    public DateTime? FechaPagoFactura { get; set; }
}

public class CrearContratoDarsenaDto
{
    [Required]
    public int ClienteEmpresaId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar una dársena.")]
    public int DarsenaId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un plan de alquiler.")]
    public PlanAlquilerDarsena Plan { get; set; } = PlanAlquilerDarsena.Trimestral;

    [Required(ErrorMessage = "Debe especificar qué cosas va a querer almacenar.")]
    public string MercaderiaAlmacenar { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe especificar el tipo de almacenamiento (Frío, Calor, Ambiente, Congelado).")]
    public string TipoAlmacenamiento { get; set; } = "Ambiente";

    public string? ConsideracionesEspeciales { get; set; }
}

public class AprobarSolicitudDarsenaDto
{
    public string? ObservacionAuditor { get; set; }
}

public class RechazarSolicitudDarsenaDto
{
    [Required(ErrorMessage = "Debe indicar el motivo del rechazo.")]
    public string Motivo { get; set; } = string.Empty;
}

public class SolicitudOperacionDto
{
    public int Id { get; set; }
    public string CodigoSolicitud { get; set; } = string.Empty;
    public int ClienteEmpresaId { get; set; }
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public int DarsenaId { get; set; }
    public string DarsenaCodigo { get; set; } = string.Empty;
    public TipoOperacionSolicitud TipoOperacion { get; set; }
    public string TipoOperacionNombre => TipoOperacion == TipoOperacionSolicitud.ArriboIngreso ? "Arribo / Ingreso" : "Despacho / Retiro";
    public ModalidadTransporte ModalidadTransporte { get; set; }
    public string ModalidadNombre => ModalidadTransporte == ModalidadTransporte.ChoferPropio ? "Chofer Propio" : "Flota LCP";
    public int? ConductorAsignadoId { get; set; }
    public string? ConductorNombreCompleto { get; set; }
    public string? ConductorPatente { get; set; }
    public int? ResponsableDepositoId { get; set; }
    public string? ResponsableDepositoNombre { get; set; }
    public string DescripcionMercaderia { get; set; } = string.Empty;
    public decimal PesoTotalKg { get; set; }
    public decimal VolumenTotalM3 { get; set; }
    public decimal DistanciaEstimadaKm { get; set; }
    public DateTime FechaHoraTurnoProgramado { get; set; }
    public DateTime? FechaHoraIngresoReal { get; set; }
    public DateTime? FechaHoraEgresoReal { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public string EstadoNombre => Estado.ToString();
    public int? ViajeId { get; set; }
    public EstadoViaje? EstadoViaje { get; set; }
    public int? LoteInventarioId { get; set; }
    public string? LoteCodigo { get; set; }
    public int? CantidadRetirada { get; set; }
    public string? CiudadOrigen { get; set; }
    public string? CiudadDestino { get; set; }
    public DateTime? FechaSalidaCliente { get; set; }
    public string? ObservacionesOperador { get; set; }
    public bool EsDobleViaje => ModalidadTransporte == ModalidadTransporte.FlotaLCP;
    public decimal DistanciaTotalKm => EsDobleViaje ? (DistanciaEstimadaKm > 0 ? DistanciaEstimadaKm : 0) : DistanciaEstimadaKm;
    public decimal DistanciaTramoSimpleKm => EsDobleViaje ? (DistanciaEstimadaKm > 0 ? Math.Round(DistanciaEstimadaKm / 2m, 0) : 0) : DistanciaEstimadaKm;

    /// <summary>
    /// Tiempo estimado de viaje en horas según distancia por tramo a 70 km/h promedio en camión
    /// </summary>
    public double HorasViajeEstimadas => DistanciaTramoSimpleKm > 0 ? Math.Max(0.5, (double)DistanciaTramoSimpleKm / 70.0) : 0;

    /// <summary>
    /// Estimado de llegada a LCP o al destino según fecha y hora de salida establecida
    /// </summary>
    public DateTime? FechaHoraEstimadaLlegada => FechaSalidaCliente.HasValue && HorasViajeEstimadas > 0
        ? FechaSalidaCliente.Value.AddHours(HorasViajeEstimadas)
        : null;

    /// <summary>
    /// En doble viaje Flota LCP: llegada estimada al destino del cliente
    /// </summary>
    public DateTime? FechaHoraEstimadaLlegadaDestino => EsDobleViaje && FechaSalidaCliente.HasValue && HorasViajeEstimadas > 0
        ? FechaSalidaCliente.Value.AddHours(HorasViajeEstimadas)
        : null;

    /// <summary>
    /// En doble viaje Flota LCP: retorno estimado a la Base LCP (ida + 2 horas de descarga + vuelta)
    /// </summary>
    public DateTime? FechaHoraEstimadaRetornoLCP => EsDobleViaje && FechaSalidaCliente.HasValue && HorasViajeEstimadas > 0
        ? FechaSalidaCliente.Value.AddHours((HorasViajeEstimadas * 2) + 2.0)
        : null;

    public string ModalidadDetalleTexto => EsDobleViaje ? "Doble Viaje (Ida y Vuelta - Flota LCP)" : "Viaje Simple Directo (Chofer Propio)";
    public ViajeDto? ViajeInfo { get; set; }
    public List<MensajeChatDto> HistorialChat { get; set; } = new();
}

public class CrearSolicitudDto
{
    [Required(ErrorMessage = "Debe especificar la empresa cliente.")]
    public int ClienteEmpresaId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar una dársena asignada en alquiler.")]
    public int DarsenaId { get; set; }

    [Required]
    public TipoOperacionSolicitud TipoOperacion { get; set; }

    [Required]
    public ModalidadTransporte ModalidadTransporte { get; set; }

    public int? ConductorAsignadoId { get; set; }

    [Required(ErrorMessage = "Describa la mercadería a transportar / ingresar.")]
    public string DescripcionMercaderia { get; set; } = string.Empty;

    [Range(0.01, 10000000, ErrorMessage = "El peso debe ser mayor a 0 kg.")]
    public decimal PesoTotalKg { get; set; }

    [Range(0.001, 10000000, ErrorMessage = "El volumen debe ser mayor a 0 m³.")]
    public decimal VolumenTotalM3 { get; set; }

    public decimal DistanciaEstimadaKm { get; set; }

    public DateTime FechaHoraTurnoProgramado { get; set; } = DateTime.UtcNow.AddDays(1);

    // Campos nuevos: Retiro desde stock y Rutas fijas con Santa Rosa
    public int? LoteInventarioId { get; set; }
    public int? CantidadRetirada { get; set; }
    public string? CiudadOrigen { get; set; }
    public string? CiudadDestino { get; set; }
    public DateTime? FechaSalidaCliente { get; set; }
}

public class AsignarSolicitudDto
{
    public int SolicitudId { get; set; }
    public int? DarsenaId { get; set; }
    public int? ConductorId { get; set; }
    public int? ResponsableDepositoId { get; set; }
    public DateTime? FechaHoraTurnoProgramado { get; set; }
    public string? ObservacionesOperador { get; set; }
}

public class CancelarSolicitudDto
{
    public int SolicitudId { get; set; }
    public string? Motivo { get; set; }
}

public class CompletarDescargaDto
{
    public int SolicitudId { get; set; }
    public bool EsRetiro { get; set; } = false;
    public int? LoteInventarioId { get; set; }
    public int? CantidadRetirada { get; set; }
    public string CodigoLote { get; set; } = string.Empty;
    public string ProductoDescripcion { get; set; } = string.Empty;
    public int CantidadUnidades { get; set; }
    public int CantidadBultosCajas { get; set; }
    public decimal PesoKg { get; set; }
    public decimal VolumenM3 { get; set; }
    public string UbicacionInternaDarsena { get; set; } = string.Empty;
}

public class LoteInventarioDto
{
    public int Id { get; set; }
    public int ClienteEmpresaId { get; set; }
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public int DarsenaId { get; set; }
    public string DarsenaCodigo { get; set; } = string.Empty;
    public int? SolicitudIngresoId { get; set; }
    public string CodigoLote { get; set; } = string.Empty;
    public string ProductoDescripcion { get; set; } = string.Empty;
    public int CantidadUnidades { get; set; }
    public int CantidadBultosCajas { get; set; }
    public decimal PesoKg { get; set; }
    public decimal VolumenM3 { get; set; }
    public string UbicacionInternaDarsena { get; set; } = string.Empty;
    public DateTime FechaIngreso { get; set; }
    public int OrdenadoPorOperadorId { get; set; }
    public string OrdenadoPorOperadorNombre { get; set; } = string.Empty;
}

public class ViajeDto
{
    public int Id { get; set; }
    public int SolicitudOperacionId { get; set; }
    public string CodigoSolicitud { get; set; } = string.Empty;
    public int ConductorId { get; set; }
    public string ConductorNombre { get; set; } = string.Empty;
    public string PatenteCamion { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string PuertaIngresoAsignada { get; set; } = string.Empty;
    public EstadoViaje EstadoViaje { get; set; }
    public double? UltimaLatitud { get; set; }
    public double? UltimaLongitud { get; set; }
    public DateTime? UltimaActualizacionGps { get; set; }
    public string? ObservacionesChofer { get; set; }
}

public class ActualizarViajeRequestDto
{
    public EstadoViaje EstadoViaje { get; set; }
    public string? Observaciones { get; set; }
    public string? HitoAlcanzado { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
}
