using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Mobile.Components.Services;

public interface IMobileApiClient
{
    // Auth
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<UsuarioDto?> GetCurrentProfileAsync();

    // Solicitudes
    Task<List<SolicitudOperacionDto>> GetSolicitudesAsync(string? estado = null, int? clienteId = null);
    Task<SolicitudOperacionDto?> GetSolicitudByIdAsync(int id);
    Task<SolicitudOperacionDto?> CrearSolicitudAsync(CrearSolicitudDto dto);
    Task<bool> AsignarSolicitudAsync(AsignarSolicitudDto dto);
    Task<bool> CancelarSolicitudAsync(CancelarSolicitudDto dto);
    Task<bool> CompletarDescargaAsync(CompletarDescargaDto dto);

    // Darsenas
    Task<List<DarsenaDto>> GetDarsenasAsync();
    Task<bool> ActualizarEstadoDarsenaAsync(ActualizarEstadoDarsenaDto dto);

    // Inventario
    Task<List<LoteInventarioDto>> GetInventarioAsync(int? clienteId = null, int? darsenaId = null);

    // Viajes / Conductor
    Task<List<ViajeDto>> GetMisViajesAsync();
    Task<ViajeDto?> GetViajeByIdAsync(int id);
    Task<bool> ActualizarEstadoViajeAsync(int viajeId, ActualizarViajeRequestDto dto);

    // Liquidaciones
    Task<List<LiquidacionDto>> GetLiquidacionesAsync(int? clienteId = null);
    Task<bool> AuditarLiquidacionAsync(AuditarLiquidacionDto dto);

    // Calendario
    Task<List<TareaCalendarioDto>> GetTareasCalendarioAsync(DateTime? fecha = null);
    Task<TareaCalendarioDto?> CrearTareaCalendarioAsync(CrearTareaCalendarioDto dto);
    Task<bool> CambiarEstadoTareaAsync(int tareaId, string nuevoEstado);

    // Chat
    Task<List<ConversacionResumenDto>> GetConversacionesChatAsync();
    Task<List<MensajeChatDto>> GetHistorialChatAsync(string conversacionId);
    Task<MensajeChatDto?> EnviarMensajeChatAsync(EnviarMensajeDto dto);

    // Auxiliares
    Task<List<ContratoAlquilerDarsenaDto>> GetMisContratosAsync();
}

