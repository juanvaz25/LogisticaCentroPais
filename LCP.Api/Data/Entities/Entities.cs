using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LCP.Shared.Enums;

namespace LCP.Api.Data.Entities;

public class Usuario
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Telefono { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    public bool Activo { get; set; } = true;
    public bool DebeCambiarPassword { get; set; } = false;

    [MaxLength(25)]
    public string? Dni { get; set; }

    public DateTime? FechaNacimiento { get; set; }

    [MaxLength(200)]
    public string? Direccion { get; set; }

    [MaxLength(100)]
    public string? Localidad { get; set; }

    [MaxLength(100)]
    public string? Provincia { get; set; }

    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    [MaxLength(50)]
    public string? EstadoCivil { get; set; }

    [MaxLength(150)]
    public string? ContactoEmergenciaNombre { get; set; }

    [MaxLength(50)]
    public string? ContactoEmergenciaTelefono { get; set; }

    [MaxLength(100)]
    public string? PuestoCargo { get; set; }

    public DateTime? FechaIngreso { get; set; }

    [MaxLength(100)]
    public string? ModalidadContratacion { get; set; }

    [MaxLength(500)]
    public string? ObservacionesLegajo { get; set; }

    [MaxLength(500)]
    public string? DocumentoIdentidadUrl { get; set; }

    [MaxLength(500)]
    public string? CvArchivoUrl { get; set; }

    public bool EmailConfirmado { get; set; } = true;

    [MaxLength(100)]
    public string? TokenConfirmacionEmail { get; set; }

    public DateTime? FechaConfirmacionEmail { get; set; }

    public int? ClienteId { get; set; }
    [ForeignKey("ClienteId")]
    public ClienteEmpresa? ClienteEmpresa { get; set; }

    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
}

public class ClienteEmpresa
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string RazonSocial { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CUIT { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Rubro { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Domicilio { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Localidad { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Provincia { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string EmailContacto { get; set; } = string.Empty;

    [MaxLength(50)]
    public string TelefonoContacto { get; set; } = string.Empty;

    public EstadoPostulacionAlta EstadoAlta { get; set; } = EstadoPostulacionAlta.PendienteConfirmacionEmail;

    public bool EmailConfirmado { get; set; } = false;

    [MaxLength(100)]
    public string? TokenConfirmacionEmail { get; set; }

    public DateTime? FechaConfirmacionEmail { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public ICollection<ContratoAlquilerDarsena> Contratos { get; set; } = new List<ContratoAlquilerDarsena>();
    public ICollection<SolicitudOperacion> Solicitudes { get; set; } = new List<SolicitudOperacion>();
    public ICollection<LoteInventario> LotesInventario { get; set; } = new List<LoteInventario>();
}

public class Conductor
{
    public int Id { get; set; }

    public int? UsuarioId { get; set; }
    [ForeignKey("UsuarioId")]
    public Usuario? Usuario { get; set; }

    public TipoConductor TipoConductor { get; set; } = TipoConductor.FlotaLCP;

    public int? ClienteEmpresaId { get; set; }
    [ForeignKey("ClienteEmpresaId")]
    public ClienteEmpresa? ClienteEmpresa { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CUIL { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    [MaxLength(50)]
    public string CelularWhatsApp { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Domicilio { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Localidad { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Provincia { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PatenteCamion { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CapacidadCargaKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CapacidadVolumenM3 { get; set; }

    public EstadoPostulacionAlta EstadoPostulacion { get; set; } = EstadoPostulacionAlta.PendienteConfirmacionEmail;

    public bool DeclaracionJuradaAceptada { get; set; }

    [MaxLength(500)]
    public string? LicenciaArchivoUrl { get; set; }

    [MaxLength(500)]
    public string? SeguroCamionArchivoUrl { get; set; }

    public bool EmailConfirmado { get; set; } = false;

    [MaxLength(100)]
    public string? TokenConfirmacionEmail { get; set; }

    public DateTime? FechaConfirmacionEmail { get; set; }

    public DateTime FechaPostulacion { get; set; } = DateTime.UtcNow;

    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
}

public class TarifaConfiguracion
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string CodigoConcepto { get; set; } = string.Empty;

    [MaxLength(250)]
    public string Descripcion { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ValorUnitario { get; set; }

    public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;

    public int ActualizadoPorUsuarioId { get; set; }
    [ForeignKey("ActualizadoPorUsuarioId")]
    public Usuario? ActualizadoPorUsuario { get; set; }
}

public class Darsena
{
    public int Id { get; set; }

    public int NumeroDarsena { get; set; }

    [Required]
    [MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    public int CapacidadMaximaPallets { get; set; } = 30;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CapacidadMaximaM3 { get; set; } = 120.0m;

    public EstadoDarsena EstadoActual { get; set; } = EstadoDarsena.Libre;

    public int? ClienteAlquilerActualId { get; set; }
    [ForeignKey("ClienteAlquilerActualId")]
    public ClienteEmpresa? ClienteAlquilerActual { get; set; }

    [MaxLength(300)]
    public string? DetalleOperacionActual { get; set; }

    [MaxLength(50)]
    public string? ProximoTurnoHora { get; set; }

    [MaxLength(200)]
    public string? ProximoTurnoDetalle { get; set; }
}

public class ContratoAlquilerDarsena
{
    public int Id { get; set; }

    public int ClienteEmpresaId { get; set; }
    [ForeignKey("ClienteEmpresaId")]
    public ClienteEmpresa? ClienteEmpresa { get; set; }

    public int DarsenaId { get; set; }
    [ForeignKey("DarsenaId")]
    public Darsena? Darsena { get; set; }

    public PlanAlquilerDarsena Plan { get; set; }

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MontoTotalContrato { get; set; }

    public EstadoContratoDarsena Estado { get; set; } = EstadoContratoDarsena.PendienteRevision;

    [MaxLength(200)]
    public string? MercaderiaAlmacenar { get; set; } = "Carga General";

    [MaxLength(100)]
    public string? TipoAlmacenamiento { get; set; } = "Ambiente";

    [MaxLength(500)]
    public string? ConsideracionesEspeciales { get; set; }

    [MaxLength(500)]
    public string? ObservacionAuditor { get; set; }

    [MaxLength(250)]
    public string? ComprobantePagoUrl { get; set; }

    public DateTime? FechaAprobacionAuditor { get; set; }
    public DateTime? FechaPagoFactura { get; set; }
}

public class SolicitudOperacion
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string CodigoSolicitud { get; set; } = string.Empty;

    public int ClienteEmpresaId { get; set; }
    [ForeignKey("ClienteEmpresaId")]
    public ClienteEmpresa? ClienteEmpresa { get; set; }

    public int DarsenaId { get; set; }
    [ForeignKey("DarsenaId")]
    public Darsena? Darsena { get; set; }

    public TipoOperacionSolicitud TipoOperacion { get; set; }

    public ModalidadTransporte ModalidadTransporte { get; set; }

    public int? ConductorAsignadoId { get; set; }
    [ForeignKey("ConductorAsignadoId")]
    public Conductor? ConductorAsignado { get; set; }

    public int? ResponsableDepositoId { get; set; }
    [ForeignKey("ResponsableDepositoId")]
    public Usuario? ResponsableDeposito { get; set; }

    [MaxLength(500)]
    public string DescripcionMercaderia { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PesoTotalKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VolumenTotalM3 { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DistanciaEstimadaKm { get; set; }

    public DateTime FechaHoraTurnoProgramado { get; set; }
    public DateTime? FechaHoraIngresoReal { get; set; }
    public DateTime? FechaHoraEgresoReal { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public int? LoteInventarioId { get; set; }
    [ForeignKey("LoteInventarioId")]
    public LoteInventario? LoteInventario { get; set; }

    public int? CantidadRetirada { get; set; }

    [MaxLength(150)]
    public string? CiudadOrigen { get; set; }

    [MaxLength(150)]
    public string? CiudadDestino { get; set; }

    public DateTime? FechaSalidaCliente { get; set; }

    [MaxLength(500)]
    public string? ObservacionesOperador { get; set; }

    public Viaje? Viaje { get; set; }

    public ICollection<DocumentoAdjunto> Documentos { get; set; } = new List<DocumentoAdjunto>();
}

public class Viaje
{
    public int Id { get; set; }

    public int SolicitudOperacionId { get; set; }
    [ForeignKey("SolicitudOperacionId")]
    public SolicitudOperacion? SolicitudOperacion { get; set; }

    public int ConductorId { get; set; }
    [ForeignKey("ConductorId")]
    public Conductor? Conductor { get; set; }

    [MaxLength(200)]
    public string Origen { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Destino { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PuertaIngresoAsignada { get; set; } = "Calle San Martín 245";

    public EstadoViaje EstadoViaje { get; set; } = EstadoViaje.Asignado;

    public double? UltimaLatitud { get; set; }
    public double? UltimaLongitud { get; set; }
    public DateTime? UltimaActualizacionGps { get; set; }

    [MaxLength(500)]
    public string? ObservacionesChofer { get; set; }
}

public class LoteInventario
{
    public int Id { get; set; }

    public int ClienteEmpresaId { get; set; }
    [ForeignKey("ClienteEmpresaId")]
    public ClienteEmpresa? ClienteEmpresa { get; set; }

    public int DarsenaId { get; set; }
    [ForeignKey("DarsenaId")]
    public Darsena? Darsena { get; set; }

    public int? SolicitudIngresoId { get; set; }
    [ForeignKey("SolicitudIngresoId")]
    public SolicitudOperacion? SolicitudIngreso { get; set; }

    [Required]
    [MaxLength(100)]
    public string CodigoLote { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string ProductoDescripcion { get; set; } = string.Empty;

    public int CantidadUnidades { get; set; }
    public int CantidadBultosCajas { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PesoKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VolumenM3 { get; set; }

    [MaxLength(150)]
    public string UbicacionInternaDarsena { get; set; } = string.Empty;

    public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;

    public int OrdenadoPorOperadorId { get; set; }
    [ForeignKey("OrdenadoPorOperadorId")]
    public Usuario? OrdenadoPorOperador { get; set; }
}

public class Liquidacion
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string NumeroComprobante { get; set; } = string.Empty;

    public int ClienteEmpresaId { get; set; }
    [ForeignKey("ClienteEmpresaId")]
    public ClienteEmpresa? ClienteEmpresa { get; set; }

    public TipoLiquidacion TipoLiquidacion { get; set; }

    public int? ContratoAlquilerDarsenaId { get; set; }
    [ForeignKey("ContratoAlquilerDarsenaId")]
    public ContratoAlquilerDarsena? ContratoAlquilerDarsena { get; set; }

    public int? SolicitudOperacionId { get; set; }
    [ForeignKey("SolicitudOperacionId")]
    public SolicitudOperacion? SolicitudOperacion { get; set; }

    [MaxLength(250)]
    public string VariablesAplicadasResumen { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubtotalPeso { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubtotalVolumen { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubtotalDistancia { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubtotalTiempoDepositoOAlquiler { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ImporteTotal { get; set; }

    public EstadoLiquidacion Estado { get; set; } = EstadoLiquidacion.PendienteDeAuditoria;

    public int? AuditadoPorUsuarioId { get; set; }
    [ForeignKey("AuditadoPorUsuarioId")]
    public Usuario? AuditadoPorUsuario { get; set; }

    public DateTime? FechaAuditoria { get; set; }

    [MaxLength(500)]
    public string? ObservacionAuditoria { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentoAdjunto> Documentos { get; set; } = new List<DocumentoAdjunto>();
}

public class TareaCalendario
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Detalle { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    public int ResponsableUsuarioId { get; set; }
    [ForeignKey("ResponsableUsuarioId")]
    public Usuario? ResponsableUsuario { get; set; }

    public int? SolicitudOperacionId { get; set; }
    [ForeignKey("SolicitudOperacionId")]
    public SolicitudOperacion? SolicitudOperacion { get; set; }

    [MaxLength(50)]
    public string EstadoTarea { get; set; } = "Pendiente";

    [MaxLength(50)]
    public string TipoActividad { get; set; } = "Tarea";
}

public class DocumentoAdjunto
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string TipoDocumento { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string NombreArchivo { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string RutaUrl { get; set; } = string.Empty;

    public int? SolicitudOperacionId { get; set; }
    [ForeignKey("SolicitudOperacionId")]
    public SolicitudOperacion? SolicitudOperacion { get; set; }

    public int? LiquidacionId { get; set; }
    [ForeignKey("LiquidacionId")]
    public Liquidacion? Liquidacion { get; set; }

    public int? ConductorId { get; set; }
    [ForeignKey("ConductorId")]
    public Conductor? Conductor { get; set; }

    public int SubidoPorUsuarioId { get; set; }
    [ForeignKey("SubidoPorUsuarioId")]
    public Usuario? SubidoPorUsuario { get; set; }

    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}

public class MensajeChat
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ConversacionId { get; set; } = string.Empty;

    public int RemitenteUsuarioId { get; set; }
    [ForeignKey("RemitenteUsuarioId")]
    public Usuario? RemitenteUsuario { get; set; }

    public int? DestinatarioUsuarioId { get; set; }
    [ForeignKey("DestinatarioUsuarioId")]
    public Usuario? DestinatarioUsuario { get; set; }

    public int? SolicitudOperacionId { get; set; }
    [ForeignKey("SolicitudOperacionId")]
    public SolicitudOperacion? SolicitudOperacion { get; set; }

    [Required]
    public string TextoMensaje { get; set; } = string.Empty;

    public bool EsIncidenteRuta { get; set; } = false;

    public bool Leido { get; set; } = false;

    public DateTime FechaHoraEnvio { get; set; } = DateTime.UtcNow;
}
