using System.Net.Http.Headers;
using System.Text.Json;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Web.Services;

public interface ILcpApiClient
{
    // Auth & Onboarding
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto);
    Task<bool> RegistrarEmpresaAsync(RegistroEmpresaDto dto);
    Task<ConfirmarEmailResponseDto> ConfirmarEmailAsync(string token);
    Task<bool> PostulacionChoferAsync(PostulacionChoferDto dto);
    Task<bool> CambiarPasswordAsync(CambiarPasswordDto dto);

    // Darsenas
    Task<List<DarsenaDto>> GetDarsenasAsync();
    Task<DarsenaDto?> GetDarsenaAsync(int id);
    Task<bool> ActualizarEstadoDarsenaAsync(int id, ActualizarEstadoDarsenaDto dto);

    // Contratos
    Task<List<ContratoAlquilerDarsenaDto>> GetContratosAsync();
    Task<ContratoAlquilerDarsenaDto?> CrearContratoAsync(CrearContratoDarsenaDto dto);
    Task<bool> AprobarContratoDarsenaAsync(int id, AprobarSolicitudDarsenaDto? dto = null);
    Task<bool> RechazarContratoDarsenaAsync(int id, RechazarSolicitudDarsenaDto dto);
    Task<bool> CargarFacturaContratoAsync(int id, IFormFile archivo);
    Task<bool> HabilitarContratoDarsenaAsync(int id);

    // Solicitudes
    Task<List<SolicitudOperacionDto>> GetSolicitudesAsync(EstadoSolicitud? estado = null);
    Task<SolicitudOperacionDto?> GetSolicitudAsync(int id);
    Task<SolicitudOperacionDto?> CrearSolicitudAsync(CrearSolicitudDto dto);
    Task<bool> AsignarSolicitudAsync(AsignarSolicitudDto dto);
    Task<bool> CambiarEstadoSolicitudAsync(int id, EstadoSolicitud nuevoEstado);
    Task<bool> CancelarSolicitudAsync(int id, CancelarSolicitudDto? dto = null);
    Task<bool> ActualizarViajeAsync(int id, ActualizarViajeRequestDto dto);
    Task<(bool Exito, string Mensaje)> CompletarDescargaAsync(int id, CompletarDescargaDto dto);

    // Inventario
    Task<List<LoteInventarioDto>> GetInventarioAsync(int? darsenaId = null);

    // Liquidaciones & Auditoria
    Task<List<LiquidacionDto>> GetLiquidacionesAsync(EstadoLiquidacion? estado = null);
    Task<KpisLiquidacionDto?> GetKpisLiquidacionesAsync();
    Task<(bool Exitoso, string Mensaje)> AuditarLiquidacionAsync(int id, AuditarLiquidacionDto dto);

    // Tarifas
    Task<List<TarifaConfiguracionDto>> GetTarifasAsync();
    Task<bool> ActualizarTarifaAsync(ActualizarTarifaDto dto);

    // Usuarios
    Task<List<UsuarioDto>> GetUsuariosAsync();
    Task<bool> CrearUsuarioAsync(UsuarioDto dto);
    Task<(bool Exitoso, string Mensaje)> CrearPersonalAsync(CrearPersonalDto dto);
    Task<(bool Exitoso, string Mensaje)> ReenviarConfirmacionPersonalAsync(int usuarioId);
    Task<bool> ToggleActivoUsuarioAsync(int id);

    // Conductores
    Task<List<ConductorViewModel>> GetConductoresAsync(EstadoPostulacionAlta? estado = null, TipoConductor? tipo = null);
    Task<(bool Exitoso, string Mensaje)> CrearChoferPropioAsync(PostulacionChoferDto dto);
    Task<(bool Exitoso, string Mensaje)> CrearConductorFlotaAsync(CrearConductorFlotaDto dto);
    Task<bool> AprobarConductorAsync(int id);
    Task<bool> RechazarConductorAsync(int id);
    Task<bool> DarDeBajaConductorAsync(int id);

    // Clientes
    Task<List<ClienteEmpresaViewModel>> GetClientesAsync(EstadoPostulacionAlta? estado = null);
    Task<bool> AprobarClienteAsync(int id);
    Task<bool> RechazarClienteAsync(int id);

    // Calendario
    Task<List<TareaCalendarioDto>> GetTareasCalendarioAsync(string? fecha = null);
    Task<TareaCalendarioDto?> CrearTareaCalendarioAsync(CrearTareaCalendarioDto dto);
    Task<bool> CompletarTareaCalendarioAsync(int id);
    Task<bool> EliminarTareaCalendarioAsync(int id);

    // Chat
    Task<List<ConversacionResumenDto>> GetConversacionesAsync();
    Task<List<MensajeChatDto>> GetHistorialChatAsync(string conversacionId);
    Task<MensajeChatDto?> EnviarMensajeChatAsync(EnviarMensajeDto dto);
    Task<List<UsuarioDto>> GetContactosChatAsync();
    Task<bool> MarcarMensajesLeidosAsync(string? conversacionId = null);

    // Documentos
    Task<DocumentoAdjuntoDto?> SubirDocumentoAsync(IFormFile archivo, string tipoDocumento, int? solicitudId = null, int? liquidacionId = null, int? conductorId = null);
    Task<List<DocumentoAdjuntoDto>> GetDocumentosPorLiquidacionAsync(int liquidacionId);
    Task<List<DocumentoAdjuntoDto>> GetDocumentosPorSolicitudAsync(int solicitudId);
    Task<List<DocumentoAdjuntoDto>> GetDocumentosPorConductorAsync(int conductorId);

    // Notificaciones
    Task<List<NotificacionOperacionDto>> GetNotificacionesOperacionesAsync();
    Task<List<NotificacionMensajeDto>> GetNotificacionesMensajesAsync();
}


public class ConductorViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string CUIL { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string CelularWhatsApp { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public string Localidad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string PatenteCamion { get; set; } = string.Empty;
    public decimal CapacidadCargaKg { get; set; }
    public decimal CapacidadVolumenM3 { get; set; }
    public TipoConductor TipoConductor { get; set; }
    public string TipoConductorNombre { get; set; } = string.Empty;
    public int? ClienteEmpresaId { get; set; }
    public string? ClienteRazonSocial { get; set; }
    public EstadoPostulacionAlta EstadoPostulacion { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public DateTime FechaPostulacion { get; set; }
    public int? UsuarioId { get; set; }
    public bool UsuarioActivo { get; set; } = true;
    public int DocumentosCount { get; set; }
    public string? LicenciaArchivoUrl { get; set; }
    public string? SeguroCamionArchivoUrl { get; set; }
    public bool EmailConfirmado { get; set; }
    public DateTime? FechaConfirmacionEmail { get; set; }
}

public class ClienteEmpresaViewModel
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string CUIT { get; set; } = string.Empty;
    public string Rubro { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public string Localidad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string EmailContacto { get; set; } = string.Empty;
    public string TelefonoContacto { get; set; } = string.Empty;
    public EstadoPostulacionAlta EstadoAlta { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public bool EmailConfirmado { get; set; }
    public DateTime? FechaConfirmacionEmail { get; set; }
    public DateTime FechaSolicitud { get; set; }
}
