using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Web.Services;

public class LcpApiClient : ILcpApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LcpApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public LcpApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor, ILogger<LcpApiClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private void AttachBearerToken()
    {
        var token = _httpContextAccessor.HttpContext?.User?.FindFirstValue("JwtToken")
                 ?? _httpContextAccessor.HttpContext?.Request.Cookies["LCP_JWT_TOKEN"];

        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);
            }

            // Si es un error HTTP (ej. 401 Unauthorized por contraseña incorrecta o email no confirmado)
            try
            {
                var errorDto = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);
                if (errorDto != null) return errorDto;
            }
            catch
            {
                // La respuesta no era JSON (ej. 500 Internal Server Error con texto plano/HTML)
            }

            return new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = $"Error del servidor ({response.StatusCode}). Verifique que LCP.Api esté ejecutándose correctamente."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al conectar con la API en LoginAsync. ¿Está encendida LCP.Api en el puerto 5152?");
            return new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = "No se pudo conectar con el servidor central de la API (puerto 5152). Asegúrese de que LCP.Api esté en ejecución."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en LoginAsync");
            return new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = "Ocurrió un error al procesar el inicio de sesión. Por favor reintente."
            };
        }
    }

    public async Task<bool> RegistrarEmpresaAsync(RegistroEmpresaDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/registro-empresa", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al conectar con LCP.Api en RegistrarEmpresaAsync.");
            return false;
        }
    }

    public async Task<ConfirmarEmailResponseDto> ConfirmarEmailAsync(string token)
    {
        try
        {
            var response = await _http.GetAsync($"/api/auth/confirmar-email?token={Uri.EscapeDataString(token)}");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ConfirmarEmailResponseDto>(_jsonOptions);
                return data ?? new ConfirmarEmailResponseDto { Exitoso = true, Mensaje = "Correo electrónico verificado exitosamente." };
            }

            var errData = await response.Content.ReadFromJsonAsync<ConfirmarEmailResponseDto>(_jsonOptions);
            return errData ?? new ConfirmarEmailResponseDto { Exitoso = false, Mensaje = "El enlace de verificación es inválido o ha expirado." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al conectar con LCP.Api en ConfirmarEmailAsync con token {Token}", token);
            return new ConfirmarEmailResponseDto { Exitoso = false, Mensaje = "No se pudo comunicar con el servidor central para confirmar el correo." };
        }
    }

    public async Task<bool> PostulacionChoferAsync(PostulacionChoferDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/postulacion-chofer", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al conectar con LCP.Api en PostulacionChoferAsync.");
            return false;
        }
    }

    public async Task<bool> CambiarPasswordAsync(CambiarPasswordDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/auth/cambiar-password", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al conectar con LCP.Api en CambiarPasswordAsync.");
            return false;
        }
    }

    public async Task<List<DarsenaDto>> GetDarsenasAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/darsenas");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DarsenaDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar con LCP.Api en /api/darsenas. ¿Está encendida la API?");
        }
        return new();
    }

    public async Task<DarsenaDto?> GetDarsenaAsync(int id)
    {
        try
        {
            AttachBearerToken();
            return await _http.GetFromJsonAsync<DarsenaDto>($"/api/darsenas/{id}", _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error al conectar con LCP.Api en GetDarsenaAsync({Id})", id);
            return null;
        }
    }

    public async Task<bool> ActualizarEstadoDarsenaAsync(int id, ActualizarEstadoDarsenaDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/darsenas/{id}/estado", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en ActualizarEstadoDarsenaAsync");
            return false;
        }
    }

    public async Task<List<ContratoAlquilerDarsenaDto>> GetContratosAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/contratos");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ContratoAlquilerDarsenaDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error al obtener contratos desde la API");
        }
        return new();
    }

    public async Task<ContratoAlquilerDarsenaDto?> CrearContratoAsync(CrearContratoDarsenaDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/contratos", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ContratoAlquilerDarsenaDto>(_jsonOptions);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearContratoAsync");
        }
        return null;
    }

    public async Task<bool> AprobarContratoDarsenaAsync(int id, AprobarSolicitudDarsenaDto? dto = null)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/contratos/{id}/aprobar-auditor", dto ?? new AprobarSolicitudDarsenaDto());
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en AprobarContratoDarsenaAsync({Id})", id);
            return false;
        }
    }

    public async Task<bool> RechazarContratoDarsenaAsync(int id, RechazarSolicitudDarsenaDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/contratos/{id}/rechazar-auditor", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en RechazarContratoDarsenaAsync({Id})", id);
            return false;
        }
    }

    public async Task<bool> CargarFacturaContratoAsync(int id, IFormFile archivo)
    {
        try
        {
            AttachBearerToken();
            using var content = new MultipartFormDataContent();
            using var stream = archivo.OpenReadStream();
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType);
            content.Add(streamContent, "archivo", archivo.FileName);

            var response = await _http.PostAsync($"/api/contratos/{id}/cargar-factura", content);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CargarFacturaContratoAsync({Id})", id);
            return false;
        }
    }

    public async Task<bool> HabilitarContratoDarsenaAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/contratos/{id}/habilitar-darsena", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en HabilitarContratoDarsenaAsync({Id})", id);
            return false;
        }
    }

    public async Task<List<SolicitudOperacionDto>> GetSolicitudesAsync(EstadoSolicitud? estado = null)
    {
        try
        {
            AttachBearerToken();
            string url = "/api/solicitudes";
            if (estado.HasValue) url += $"?estado={estado.Value}";

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<SolicitudOperacionDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error al obtener solicitudes desde la API");
        }
        return new();
    }

    public async Task<SolicitudOperacionDto?> GetSolicitudAsync(int id)
    {
        try
        {
            AttachBearerToken();
            return await _http.GetFromJsonAsync<SolicitudOperacionDto>($"/api/solicitudes/{id}", _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetSolicitudAsync");
            return null;
        }
    }

    public async Task<SolicitudOperacionDto?> CrearSolicitudAsync(CrearSolicitudDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/solicitudes", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SolicitudOperacionDto>(_jsonOptions);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearSolicitudAsync");
        }
        return null;
    }

    public async Task<bool> AsignarSolicitudAsync(AsignarSolicitudDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/solicitudes/{dto.SolicitudId}/asignar", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en AsignarSolicitudAsync");
            return false;
        }
    }

    public async Task<bool> CambiarEstadoSolicitudAsync(int id, EstadoSolicitud nuevoEstado)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/solicitudes/{id}/estado", nuevoEstado);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CambiarEstadoSolicitudAsync");
            return false;
        }
    }

    public async Task<bool> CancelarSolicitudAsync(int id, CancelarSolicitudDto? dto = null)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync($"/api/solicitudes/{id}/cancelar", dto ?? new CancelarSolicitudDto { SolicitudId = id });
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CancelarSolicitudAsync");
            return false;
        }
    }

    public async Task<bool> ActualizarViajeAsync(int id, ActualizarViajeRequestDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/solicitudes/{id}/viaje", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en ActualizarViajeAsync({Id})", id);
            return false;
        }
    }

    public async Task<(bool Exito, string Mensaje)> CompletarDescargaAsync(int id, CompletarDescargaDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync($"/api/solicitudes/{id}/completar-descarga", dto);
            if (response.IsSuccessStatusCode)
            {
                var okData = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(_jsonOptions);
                var mensaje = okData?["mensaje"]?.ToString() ?? "Operación completada con éxito en la dársena.";
                return (true, mensaje);
            }

            var errData = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(_jsonOptions);
            var errMensaje = errData?["mensaje"]?.ToString() ?? "No se pudo confirmar la operación. Verifique que el camión haya arribado a la dársena.";
            return (false, errMensaje);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CompletarDescargaAsync");
            return (false, "Error de comunicación con el servicio.");
        }
    }

    public async Task<List<LoteInventarioDto>> GetInventarioAsync(int? darsenaId = null)
    {
        try
        {
            AttachBearerToken();
            string url = "/api/inventario";
            if (darsenaId.HasValue) url += $"?darsenaId={darsenaId.Value}";

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<LoteInventarioDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetInventarioAsync");
        }
        return new();
    }

    public async Task<List<LiquidacionDto>> GetLiquidacionesAsync(EstadoLiquidacion? estado = null)
    {
        try
        {
            AttachBearerToken();
            string url = "/api/liquidaciones";
            if (estado.HasValue) url += $"?estado={estado.Value}";

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<LiquidacionDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetLiquidacionesAsync");
        }
        return new();
    }

    public async Task<KpisLiquidacionDto?> GetKpisLiquidacionesAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/liquidaciones/kpis");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<KpisLiquidacionDto>(_jsonOptions);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetKpisLiquidacionesAsync");
        }
        return null;
    }

    public async Task<(bool Exitoso, string Mensaje)> AuditarLiquidacionAsync(int id, AuditarLiquidacionDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync($"/api/liquidaciones/{id}/auditar", dto);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("mensaje", out var okProp) || doc.RootElement.TryGetProperty("Mensaje", out okProp))
                    {
                        return (true, okProp.GetString() ?? "Liquidación auditada con éxito.");
                    }
                }
                catch { }
                return (true, "Liquidación auditada con éxito.");
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("mensaje", out var msgProp) || doc.RootElement.TryGetProperty("Mensaje", out msgProp))
                {
                    return (false, msgProp.GetString() ?? "Error al auditar la liquidación.");
                }
            }
            catch { }

            return (false, "No se pudo auditar la liquidación.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en AuditarLiquidacionAsync");
            return (false, "Error de comunicación con el servicio.");
        }
    }

    public async Task<List<TarifaConfiguracionDto>> GetTarifasAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/tarifas");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<TarifaConfiguracionDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetTarifasAsync");
        }
        return new();
    }

    public async Task<bool> ActualizarTarifaAsync(ActualizarTarifaDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsJsonAsync("/api/tarifas", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en ActualizarTarifaAsync");
            return false;
        }
    }

    public async Task<List<UsuarioDto>> GetUsuariosAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/usuarios");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<UsuarioDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetUsuariosAsync");
        }
        return new();
    }

    public async Task<bool> CrearUsuarioAsync(UsuarioDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/usuarios", dto);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearUsuarioAsync");
            return false;
        }
    }

    public async Task<(bool Exitoso, string Mensaje)> CrearPersonalAsync(CrearPersonalDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/usuarios", dto);
            if (response.IsSuccessStatusCode)
            {
                var okData = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(_jsonOptions);
                var mensaje = okData?["mensaje"]?.ToString() ?? "Personal registrado exitosamente.";
                return (true, mensaje);
            }

            var errData = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(_jsonOptions);
            var errMensaje = errData?["mensaje"]?.ToString() ?? "No se pudo registrar al personal. Verifique los datos.";
            return (false, errMensaje);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearPersonalAsync");
            return (false, "Error de comunicación con el servicio.");
        }
    }

    public async Task<(bool Exitoso, string Mensaje)> ReenviarConfirmacionPersonalAsync(int usuarioId)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsync($"/api/usuarios/{usuarioId}/reenviar-confirmacion", null);
            if (response.IsSuccessStatusCode)
            {
                var okData = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(_jsonOptions);
                var mensaje = okData?["mensaje"]?.ToString() ?? "Correo reenviado con éxito.";
                return (true, mensaje);
            }

            var errData = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(_jsonOptions);
            var errMensaje = errData?["mensaje"]?.ToString() ?? "No se pudo reenviar la confirmación.";
            return (false, errMensaje);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en ReenviarConfirmacionPersonalAsync");
            return (false, "Error de conexión al reenviar la confirmación.");
        }
    }

    public async Task<bool> ToggleActivoUsuarioAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/usuarios/{id}/toggle-activo", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en ToggleActivoUsuarioAsync");
            return false;
        }
    }

    public async Task<List<ConductorViewModel>> GetConductoresAsync(EstadoPostulacionAlta? estado = null, TipoConductor? tipo = null)
    {
        try
        {
            AttachBearerToken();
            string url = "/api/conductores";
            var query = new List<string>();
            if (estado.HasValue) query.Add($"estado={estado.Value}");
            if (tipo.HasValue) query.Add($"tipo={tipo.Value}");
            if (query.Any()) url += "?" + string.Join("&", query);

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ConductorViewModel>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetConductoresAsync");
        }
        return new();
    }

    public async Task<(bool Exitoso, string Mensaje)> CrearChoferPropioAsync(PostulacionChoferDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/conductores", dto);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Chofer propio registrado correctamente.");
            }

            var errJson = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(errJson);
                if (doc.RootElement.TryGetProperty("mensaje", out var msgProp) || doc.RootElement.TryGetProperty("Mensaje", out msgProp))
                {
                    return (false, msgProp.GetString() ?? "Error al registrar el chofer propio.");
                }
            }
            catch { }

            return (false, $"Error al registrar el chofer (Código HTTP {(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearChoferPropioAsync");
            return (false, "Error de comunicación con el servidor central.");
        }
    }

    public async Task<bool> AprobarConductorAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/conductores/{id}/aprobar", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en AprobarConductorAsync");
            return false;
        }
    }

    public async Task<bool> RechazarConductorAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/conductores/{id}/rechazar", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en RechazarConductorAsync");
            return false;
        }
    }

    public async Task<bool> DarDeBajaConductorAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/conductores/{id}/baja", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en DarDeBajaConductorAsync");
            return false;
        }
    }

    public async Task<List<ClienteEmpresaViewModel>> GetClientesAsync(EstadoPostulacionAlta? estado = null)
    {
        try
        {
            AttachBearerToken();
            string url = "/api/clientes";
            if (estado.HasValue) url += $"?estado={estado.Value}";

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ClienteEmpresaViewModel>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetClientesAsync");
        }
        return new();
    }

    public async Task<bool> AprobarClienteAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/clientes/{id}/aprobar", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en AprobarClienteAsync");
            return false;
        }
    }

    public async Task<bool> RechazarClienteAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/clientes/{id}/rechazar", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en RechazarClienteAsync");
            return false;
        }
    }

    public async Task<List<TareaCalendarioDto>> GetTareasCalendarioAsync(string? fecha = null)
    {
        try
        {
            AttachBearerToken();
            string url = "/api/calendario";
            if (!string.IsNullOrEmpty(fecha)) url += $"?fecha={fecha}";

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<TareaCalendarioDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetTareasCalendarioAsync");
        }
        return new();
    }

    public async Task<TareaCalendarioDto?> CrearTareaCalendarioAsync(CrearTareaCalendarioDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/calendario", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TareaCalendarioDto>(_jsonOptions);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearTareaCalendarioAsync");
        }
        return null;
    }

    public async Task<bool> CompletarTareaCalendarioAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PutAsync($"/api/calendario/{id}/completar", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CompletarTareaCalendarioAsync");
            return false;
        }
    }

    public async Task<bool> EliminarTareaCalendarioAsync(int id)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.DeleteAsync($"/api/calendario/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en EliminarTareaCalendarioAsync");
            return false;
        }
    }

    public async Task<List<ConversacionResumenDto>> GetConversacionesAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/chat/conversaciones");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ConversacionResumenDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetConversacionesAsync");
        }
        return new();
    }

    public async Task<List<MensajeChatDto>> GetHistorialChatAsync(string conversacionId)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync($"/api/chat/historial/{conversacionId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MensajeChatDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetHistorialChatAsync");
        }
        return new();
    }

    public async Task<MensajeChatDto?> EnviarMensajeChatAsync(EnviarMensajeDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/chat/enviar", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MensajeChatDto>(_jsonOptions);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en EnviarMensajeChatAsync");
        }
        return null;
    }

    public async Task<DocumentoAdjuntoDto?> SubirDocumentoAsync(IFormFile archivo, string tipoDocumento, int? solicitudId = null, int? liquidacionId = null, int? conductorId = null)
    {
        try
        {
            AttachBearerToken();
            using var content = new MultipartFormDataContent();
            using var stream = archivo.OpenReadStream();
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(archivo.ContentType);
            content.Add(streamContent, "archivo", archivo.FileName);
            content.Add(new StringContent(tipoDocumento), "tipoDocumento");

            if (solicitudId.HasValue) content.Add(new StringContent(solicitudId.Value.ToString()), "solicitudOperacionId");
            if (liquidacionId.HasValue) content.Add(new StringContent(liquidacionId.Value.ToString()), "liquidacionId");
            if (conductorId.HasValue) content.Add(new StringContent(conductorId.Value.ToString()), "conductorId");

            var response = await _http.PostAsync("/api/documentos/subir", content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DocumentoAdjuntoDto>(_jsonOptions);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en SubirDocumentoAsync");
        }
        return null;
    }

    public async Task<List<UsuarioDto>> GetContactosChatAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/chat/contactos");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<UsuarioDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetContactosChatAsync");
        }
        return new();
    }

    public async Task<List<DocumentoAdjuntoDto>> GetDocumentosPorLiquidacionAsync(int liquidacionId)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync($"/api/documentos/liquidacion/{liquidacionId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DocumentoAdjuntoDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetDocumentosPorLiquidacionAsync");
        }
        return new();
    }

    public async Task<(bool Exitoso, string Mensaje)> CrearConductorFlotaAsync(CrearConductorFlotaDto dto)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.PostAsJsonAsync("/api/conductores/flota-lcp", dto);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Conductor de Flota Oficial LCP registrado y credenciales enviadas.");
            }

            var errJson = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(errJson);
                if (doc.RootElement.TryGetProperty("mensaje", out var msgProp) || doc.RootElement.TryGetProperty("Mensaje", out msgProp))
                {
                    return (false, msgProp.GetString() ?? "Error al registrar el chofer.");
                }
            }
            catch { }

            return (false, $"Error al registrar el chofer (Código HTTP {(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en CrearConductorFlotaAsync");
            return (false, "Error de comunicación con el servidor central.");
        }
    }

    public async Task<List<DocumentoAdjuntoDto>> GetDocumentosPorSolicitudAsync(int solicitudId)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync($"/api/documentos/solicitud/{solicitudId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DocumentoAdjuntoDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetDocumentosPorSolicitudAsync");
        }
        return new();
    }

    public async Task<List<DocumentoAdjuntoDto>> GetDocumentosPorConductorAsync(int conductorId)
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync($"/api/documentos/conductor/{conductorId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DocumentoAdjuntoDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetDocumentosPorConductorAsync({Id})", conductorId);
        }
        return new();
    }

    public async Task<List<NotificacionOperacionDto>> GetNotificacionesOperacionesAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/notificaciones/operaciones");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<NotificacionOperacionDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetNotificacionesOperacionesAsync");
        }
        return new();
    }

    public async Task<List<NotificacionMensajeDto>> GetNotificacionesMensajesAsync()
    {
        try
        {
            AttachBearerToken();
            var response = await _http.GetAsync("/api/notificaciones/mensajes");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<NotificacionMensajeDto>>(_jsonOptions) ?? new();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en GetNotificacionesMensajesAsync");
        }
        return new();
    }

    public async Task<bool> MarcarMensajesLeidosAsync(string? conversacionId = null)
    {
        try
        {
            AttachBearerToken();
            var url = string.IsNullOrEmpty(conversacionId)
                ? "/api/chat/marcar-todos-leidos"
                : $"/api/chat/marcar-leidos/{Uri.EscapeDataString(conversacionId)}";
            var response = await _http.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error en MarcarMensajesLeidosAsync");
            return false;
        }
    }
}

