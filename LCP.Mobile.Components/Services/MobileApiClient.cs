using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Mobile.Components.Services;

/// <summary>
/// Cliente HTTP unificado para la aplicación móvil que consume directamente los endpoints
/// de LCP.Api (el mismo backend utilizado por LCP.Web MVC).
/// </summary>
public class MobileApiClient : IMobileApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStorage _tokenStorage;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MobileApiClient(HttpClient http, ITokenStorage tokenStorage)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(15);
        _tokenStorage = tokenStorage;
    }

    private async Task PrepareHttpClientAsync()
    {
        var baseUrl = await _tokenStorage.GetApiBaseUrlAsync();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:5152";
        }
        if (!baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }

        if (_http.BaseAddress == null || _http.BaseAddress.ToString().TrimEnd('/') != baseUrl.TrimEnd('/'))
        {
            _http.BaseAddress = new Uri(baseUrl);
        }

        var token = await _tokenStorage.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    // ==========================================
    // 1. AUTENTICACIÓN & PERFIL
    // ==========================================

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync("api/Auth/login", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);
                if (result != null && result.Exitoso && result.Usuario != null)
                {
                    var exp = result.Expiracion > DateTime.UtcNow ? result.Expiracion : DateTime.UtcNow.AddMinutes(30);
                    await _tokenStorage.SetTokenAsync(result.Token, exp);
                    await _tokenStorage.SetUserAsync(result.Usuario);
                    return result;
                }
            }

            try
            {
                var errResult = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);
                if (errResult != null) return errResult;
            }
            catch { }

            return new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Credenciales incorrectas o cuenta no habilitada en LCP.Api."
                    : $"Error del servidor LCP.Api (HTTP {(int)response.StatusCode})."
            };
        }
        catch (Exception ex)
        {
            return new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = $"No se pudo conectar con el servidor LCP.Api ({_http.BaseAddress}): {ex.Message}"
            };
        }
    }

    public async Task<UsuarioDto?> GetCurrentProfileAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync("api/Auth/me");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UsuarioDto>(_jsonOptions);
                if (user != null)
                {
                    await _tokenStorage.SetUserAsync(user);
                    return user;
                }
            }
        }
        catch { }

        return await _tokenStorage.GetUserAsync();
    }

    // ==========================================
    // 2. SOLICITUDES DE OPERACIÓN
    // ==========================================

    public async Task<List<SolicitudOperacionDto>> GetSolicitudesAsync(string? estado = null, int? clienteId = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/solicitudes";
            if (!string.IsNullOrWhiteSpace(estado))
            {
                url += $"?estado={Uri.EscapeDataString(estado)}";
            }

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<SolicitudOperacionDto>>(_jsonOptions);
                if (list != null)
                {
                    if (clienteId.HasValue)
                    {
                        list = list.Where(s => s.ClienteEmpresaId == clienteId.Value).ToList();
                    }
                    return list;
                }
            }
        }
        catch { }

        return new List<SolicitudOperacionDto>();
    }

    public async Task<SolicitudOperacionDto?> GetSolicitudByIdAsync(int id)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync($"api/solicitudes/{id}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SolicitudOperacionDto>(_jsonOptions);
            }
        }
        catch { }

        return null;
    }

    public async Task<SolicitudOperacionDto?> CrearSolicitudAsync(CrearSolicitudDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync("api/solicitudes", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SolicitudOperacionDto>(_jsonOptions);
            }
        }
        catch { }

        return null;
    }

    public async Task<bool> AsignarSolicitudAsync(AsignarSolicitudDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PutAsJsonAsync($"api/solicitudes/{dto.SolicitudId}/asignar", dto);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CancelarSolicitudAsync(CancelarSolicitudDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync($"api/solicitudes/{dto.SolicitudId}/cancelar", dto);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CompletarDescargaAsync(CompletarDescargaDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync($"api/solicitudes/{dto.SolicitudId}/completar-descarga", dto);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 3. DÁRSENAS
    // ==========================================

    public async Task<List<DarsenaDto>> GetDarsenasAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync("api/darsenas");
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<DarsenaDto>>(_jsonOptions);
                if (list != null) return list;
            }
        }
        catch { }

        return new List<DarsenaDto>();
    }

    public async Task<bool> ActualizarEstadoDarsenaAsync(ActualizarEstadoDarsenaDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PutAsJsonAsync($"api/darsenas/{dto.DarsenaId}/estado", dto);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 4. INVENTARIO
    // ==========================================

    public async Task<List<LoteInventarioDto>> GetInventarioAsync(int? clienteId = null, int? darsenaId = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/inventario";
            if (darsenaId.HasValue)
            {
                url += $"?darsenaId={darsenaId.Value}";
            }

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<LoteInventarioDto>>(_jsonOptions);
                if (list != null)
                {
                    if (clienteId.HasValue)
                    {
                        list = list.Where(l => l.ClienteEmpresaId == clienteId.Value).ToList();
                    }
                    return list;
                }
            }
        }
        catch { }

        return new List<LoteInventarioDto>();
    }

    // ==========================================
    // 5. VIAJES / CONDUCTOR
    // ==========================================

    public async Task<List<ViajeDto>> GetMisViajesAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync("api/solicitudes");
            if (response.IsSuccessStatusCode)
            {
                var sols = await response.Content.ReadFromJsonAsync<List<SolicitudOperacionDto>>(_jsonOptions);
                if (sols != null)
                {
                    var viajes = new List<ViajeDto>();
                    foreach (var s in sols)
                    {
                        if (s.ConductorAsignadoId.HasValue || s.Estado == EstadoSolicitud.Programada || s.Estado == EstadoSolicitud.EnCurso)
                        {
                            viajes.Add(new ViajeDto
                            {
                                Id = s.ViajeId ?? s.Id,
                                SolicitudOperacionId = s.Id,
                                CodigoSolicitud = s.CodigoSolicitud,
                                ConductorId = s.ConductorAsignadoId ?? 0,
                                ConductorNombre = s.ConductorNombreCompleto ?? "Conductor",
                                PatenteCamion = s.ConductorPatente ?? "S/P",
                                Origen = s.CiudadOrigen ?? "Base LCP",
                                Destino = s.CiudadDestino ?? "Santa Rosa",
                                PuertaIngresoAsignada = "Puerta Principal",
                                EstadoViaje = s.EstadoViaje ?? (s.Estado == EstadoSolicitud.EnCurso ? EstadoViaje.EnRuta : EstadoViaje.Asignado)
                            });
                        }
                    }
                    return viajes;
                }
            }
        }
        catch { }

        return new List<ViajeDto>();
    }

    public async Task<ViajeDto?> GetViajeByIdAsync(int id)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync($"api/solicitudes/{id}");
            if (response.IsSuccessStatusCode)
            {
                var s = await response.Content.ReadFromJsonAsync<SolicitudOperacionDto>(_jsonOptions);
                if (s != null)
                {
                    return new ViajeDto
                    {
                        Id = s.ViajeId ?? s.Id,
                        SolicitudOperacionId = s.Id,
                        CodigoSolicitud = s.CodigoSolicitud,
                        ConductorId = s.ConductorAsignadoId ?? 0,
                        ConductorNombre = s.ConductorNombreCompleto ?? "Conductor",
                        PatenteCamion = s.ConductorPatente ?? "S/P",
                        Origen = s.CiudadOrigen ?? "Base LCP",
                        Destino = s.CiudadDestino ?? "Santa Rosa",
                        PuertaIngresoAsignada = "Puerta Principal",
                        EstadoViaje = s.EstadoViaje ?? (s.Estado == EstadoSolicitud.EnCurso ? EstadoViaje.EnRuta : EstadoViaje.Asignado)
                    };
                }
            }
        }
        catch { }

        var list = await GetMisViajesAsync();
        return list.FirstOrDefault(v => v.Id == id || v.SolicitudOperacionId == id);
    }

    public async Task<bool> ActualizarEstadoViajeAsync(int viajeId, ActualizarViajeRequestDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PutAsJsonAsync($"api/solicitudes/{viajeId}/viaje", dto);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 6. LIQUIDACIONES / FINANZAS
    // ==========================================

    public async Task<List<LiquidacionDto>> GetLiquidacionesAsync(int? clienteId = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/liquidaciones";
            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<LiquidacionDto>>(_jsonOptions);
                if (list != null)
                {
                    if (clienteId.HasValue)
                    {
                        list = list.Where(l => l.ClienteEmpresaId == clienteId.Value).ToList();
                    }
                    return list;
                }
            }
        }
        catch { }

        return new List<LiquidacionDto>();
    }

    public async Task<bool> AuditarLiquidacionAsync(AuditarLiquidacionDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PutAsJsonAsync($"api/liquidaciones/{dto.LiquidacionId}/auditar", dto);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 7. CALENDARIO
    // ==========================================

    public async Task<List<TareaCalendarioDto>> GetTareasCalendarioAsync(DateTime? fecha = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/calendario";
            if (fecha.HasValue)
            {
                url += $"?fecha={fecha.Value:yyyy-MM-dd}";
            }

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<TareaCalendarioDto>>(_jsonOptions);
                if (list != null) return list;
            }
        }
        catch { }

        return new List<TareaCalendarioDto>();
    }

    public async Task<TareaCalendarioDto?> CrearTareaCalendarioAsync(CrearTareaCalendarioDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync("api/calendario", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TareaCalendarioDto>(_jsonOptions);
            }
        }
        catch { }

        return null;
    }

    public async Task<bool> CambiarEstadoTareaAsync(int tareaId, string nuevoEstado)
    {
        try
        {
            await PrepareHttpClientAsync();
            if (nuevoEstado.Equals("Completada", StringComparison.OrdinalIgnoreCase))
            {
                var response = await _http.PutAsync($"api/calendario/{tareaId}/completar", null);
                return response.IsSuccessStatusCode;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 8. CHAT & INCIDENTES
    // ==========================================

    public async Task<List<ConversacionResumenDto>> GetConversacionesChatAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync("api/chat/conversaciones");
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<ConversacionResumenDto>>(_jsonOptions);
                if (list != null) return list;
            }
        }
        catch { }

        return new List<ConversacionResumenDto>();
    }

    public async Task<List<MensajeChatDto>> GetHistorialChatAsync(string conversacionId)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync($"api/chat/historial/{Uri.EscapeDataString(conversacionId)}");
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<MensajeChatDto>>(_jsonOptions);
                if (list != null) return list;
            }
        }
        catch { }

        return new List<MensajeChatDto>();
    }

    public async Task<MensajeChatDto?> EnviarMensajeChatAsync(EnviarMensajeDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync("api/chat/enviar", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MensajeChatDto>(_jsonOptions);
            }
        }
        catch { }

        return null;
    }

    // ==========================================
    // 9. CONTRATOS DE ALQUILER
    // ==========================================

    public async Task<List<ContratoAlquilerDarsenaDto>> GetMisContratosAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.GetAsync("api/contratos");
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<ContratoAlquilerDarsenaDto>>(_jsonOptions);
                if (list != null) return list;
            }
        }
        catch { }

        return new List<ContratoAlquilerDarsenaDto>();
    }
}
