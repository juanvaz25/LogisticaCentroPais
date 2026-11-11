using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Mobile.Components.Services;

public class MobileApiClient : IMobileApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStorage _tokenStorage;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // In-memory fallback mock state for offline / demo mode
    private static readonly List<DarsenaDto> _mockDarsenas = new();
    private static readonly List<SolicitudOperacionDto> _mockSolicitudes = new();
    private static readonly List<LoteInventarioDto> _mockInventario = new();
    private static readonly List<LiquidacionDto> _mockLiquidaciones = new();
    private static readonly List<TareaCalendarioDto> _mockTareas = new();
    private static readonly List<MensajeChatDto> _mockMensajes = new();
    private static readonly List<ViajeDto> _mockViajes = new();
    private static bool _mockInitialized = false;

    public MobileApiClient(HttpClient http, ITokenStorage tokenStorage)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(2.5);
        _tokenStorage = tokenStorage;
        EnsureMockData();
    }

    private async Task PrepareHttpClientAsync()
    {
        var baseUrl = await _tokenStorage.GetApiBaseUrlAsync();
        if (_http.BaseAddress == null || _http.BaseAddress.ToString().TrimEnd('/') != baseUrl.TrimEnd('/'))
        {
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
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
        }
        catch
        {
            // API not reachable, fallback to demo authentication
        }

        // Demo login fallback by email or role
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        RolUsuario rol = RolUsuario.Administrador;
        string nombre = "Roberto Gómez";
        string empresa = "Logística Centro País";
        int? clienteId = null;

        if (email.Contains("operador"))
        {
            rol = RolUsuario.Operador;
            nombre = "Martín Benítez";
        }
        else if (email.Contains("auditor"))
        {
            rol = RolUsuario.Auditor;
            nombre = "Lucía Pereyra";
        }
        else if (email.Contains("cliente") || email.Contains("distribuidora") || email.Contains("empresa"))
        {
            rol = RolUsuario.Cliente;
            nombre = "Carlos Rossi";
            empresa = "Distribuidora Pampeana S.A.";
            clienteId = 1;
        }
        else if (email.Contains("conductor") || email.Contains("chofer"))
        {
            rol = RolUsuario.Conductor;
            nombre = "Esteban Morales";
        }

        var demoUser = new UsuarioDto
        {
            Id = rol == RolUsuario.Administrador ? 1 : rol == RolUsuario.Operador ? 2 : rol == RolUsuario.Auditor ? 3 : rol == RolUsuario.Cliente ? 4 : 5,
            Nombre = nombre.Split(' ')[0],
            Apellido = nombre.Split(' ').Length > 1 ? nombre.Split(' ')[1] : "",
            Email = request.Email,
            Telefono = "2954-412233",
            Rol = rol,
            Activo = true,
            EmailConfirmado = true,
            ClienteId = clienteId,
            EmpresaRazonSocial = empresa,
            Dni = "34.567.890",
            PuestoCargo = rol.ToString(),
            FechaIngreso = DateTime.UtcNow.AddMonths(-14),
            Localidad = "Santa Rosa",
            Provincia = "La Pampa",
            FechaAlta = DateTime.UtcNow.AddMonths(-18)
        };

        var fakeToken = "demo_jwt_token_" + Guid.NewGuid().ToString("N");
        var expiration = DateTime.UtcNow.AddMinutes(30);
        await _tokenStorage.SetTokenAsync(fakeToken, expiration);
        await _tokenStorage.SetUserAsync(demoUser);

        return new LoginResponseDto
        {
            Exitoso = true,
            Mensaje = "Sesión iniciada correctamente",
            Token = fakeToken,
            Expiracion = expiration,
            Usuario = demoUser
        };
    }

    public async Task<UsuarioDto?> GetCurrentProfileAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<UsuarioDto>("api/Usuarios/perfil", _jsonOptions);
            if (res != null) return res;
        }
        catch { }

        return await _tokenStorage.GetUserAsync();
    }

    public async Task<List<SolicitudOperacionDto>> GetSolicitudesAsync(string? estado = null, int? clienteId = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/Solicitudes";
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(estado)) query.Add($"estado={estado}");
            if (clienteId.HasValue) query.Add($"clienteId={clienteId.Value}");
            if (query.Any()) url += "?" + string.Join("&", query);

            var result = await _http.GetFromJsonAsync<List<SolicitudOperacionDto>>(url, _jsonOptions);
            if (result != null && result.Count > 0) return result;
        }
        catch { }

        // Mock fallback
        var list = _mockSolicitudes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoSolicitud>(estado, true, out var parsedEstado))
        {
            list = list.Where(s => s.Estado == parsedEstado);
        }
        if (clienteId.HasValue)
        {
            list = list.Where(s => s.ClienteEmpresaId == clienteId.Value);
        }
        return list.OrderByDescending(s => s.Id).ToList();
    }

    public async Task<SolicitudOperacionDto?> GetSolicitudByIdAsync(int id)
    {
        try
        {
            await PrepareHttpClientAsync();
            var result = await _http.GetFromJsonAsync<SolicitudOperacionDto>($"api/Solicitudes/{id}", _jsonOptions);
            if (result != null) return result;
        }
        catch { }

        return _mockSolicitudes.FirstOrDefault(s => s.Id == id);
    }

    public async Task<SolicitudOperacionDto?> CrearSolicitudAsync(CrearSolicitudDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var response = await _http.PostAsJsonAsync("api/Solicitudes", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SolicitudOperacionDto>(_jsonOptions);
            }
        }
        catch { }

        // Local creation
        var newId = _mockSolicitudes.Count > 0 ? _mockSolicitudes.Max(s => s.Id) + 1 : 101;
        var nueva = new SolicitudOperacionDto
        {
            Id = newId,
            CodigoSolicitud = $"SOL-2026-{newId:D4}",
            ClienteEmpresaId = dto.ClienteEmpresaId > 0 ? dto.ClienteEmpresaId : 1,
            ClienteRazonSocial = "Distribuidora Pampeana S.A.",
            DarsenaId = dto.DarsenaId,
            DarsenaCodigo = $"Dársena {dto.DarsenaId}",
            TipoOperacion = dto.TipoOperacion,
            ModalidadTransporte = dto.ModalidadTransporte,
            DescripcionMercaderia = dto.DescripcionMercaderia,
            PesoTotalKg = dto.PesoTotalKg,
            VolumenTotalM3 = dto.VolumenTotalM3,
            DistanciaEstimadaKm = dto.DistanciaEstimadaKm,
            FechaHoraTurnoProgramado = dto.FechaHoraTurnoProgramado,
            CiudadOrigen = dto.CiudadOrigen ?? "Santa Rosa",
            CiudadDestino = dto.CiudadDestino ?? "General Pico",
            Estado = EstadoSolicitud.Pendiente,
            FechaSalidaCliente = dto.FechaSalidaCliente ?? DateTime.UtcNow.AddHours(2)
        };
        _mockSolicitudes.Insert(0, nueva);
        return nueva;
    }

    public async Task<bool> AsignarSolicitudAsync(AsignarSolicitudDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PostAsJsonAsync($"api/Solicitudes/{dto.SolicitudId}/asignar", dto);
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var item = _mockSolicitudes.FirstOrDefault(s => s.Id == dto.SolicitudId);
        if (item != null)
        {
            item.Estado = EstadoSolicitud.Programada;
            if (dto.DarsenaId.HasValue) item.DarsenaId = dto.DarsenaId.Value;
            if (dto.ConductorId.HasValue) item.ConductorAsignadoId = dto.ConductorId.Value;
            item.ConductorNombreCompleto = "Esteban Morales (Mercedes-Benz Actros)";
            return true;
        }
        return false;
    }

    public async Task<bool> CancelarSolicitudAsync(CancelarSolicitudDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PostAsJsonAsync($"api/Solicitudes/{dto.SolicitudId}/cancelar", dto);
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var item = _mockSolicitudes.FirstOrDefault(s => s.Id == dto.SolicitudId);
        if (item != null)
        {
            item.Estado = EstadoSolicitud.Cancelada;
            return true;
        }
        return false;
    }

    public async Task<bool> CompletarDescargaAsync(CompletarDescargaDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PostAsJsonAsync("api/Solicitudes/completar-descarga", dto);
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var item = _mockSolicitudes.FirstOrDefault(s => s.Id == dto.SolicitudId);
        if (item != null)
        {
            item.Estado = EstadoSolicitud.Completada;
            item.FechaHoraEgresoReal = DateTime.UtcNow;

            // Update inventario
            var lote = new LoteInventarioDto
            {
                Id = _mockInventario.Count + 1,
                ClienteEmpresaId = item.ClienteEmpresaId,
                ClienteRazonSocial = item.ClienteRazonSocial,
                DarsenaId = item.DarsenaId,
                DarsenaCodigo = item.DarsenaCodigo,
                CodigoLote = string.IsNullOrWhiteSpace(dto.CodigoLote) ? $"LOT-{DateTime.UtcNow:yyyyMMdd}-{item.Id}" : dto.CodigoLote,
                ProductoDescripcion = string.IsNullOrWhiteSpace(dto.ProductoDescripcion) ? item.DescripcionMercaderia : dto.ProductoDescripcion,
                CantidadUnidades = dto.CantidadUnidades > 0 ? dto.CantidadUnidades : 150,
                CantidadBultosCajas = dto.CantidadBultosCajas > 0 ? dto.CantidadBultosCajas : 25,
                PesoKg = dto.PesoKg > 0 ? dto.PesoKg : item.PesoTotalKg,
                VolumenM3 = dto.VolumenM3 > 0 ? dto.VolumenM3 : item.VolumenTotalM3,
                UbicacionInternaDarsena = string.IsNullOrWhiteSpace(dto.UbicacionInternaDarsena) ? "Estantería A-03" : dto.UbicacionInternaDarsena,
                FechaIngreso = DateTime.UtcNow,
                OrdenadoPorOperadorNombre = "Martín Benítez"
            };
            _mockInventario.Insert(0, lote);

            // Create liquidation
            var liq = new LiquidacionDto
            {
                Id = _mockLiquidaciones.Count + 1,
                NumeroComprobante = $"LIQ-{DateTime.UtcNow.Year}-{_mockLiquidaciones.Count + 101}",
                ClienteEmpresaId = item.ClienteEmpresaId,
                ClienteRazonSocial = item.ClienteRazonSocial,
                TipoLiquidacion = TipoLiquidacion.ServicioTransporteYPatio,
                SolicitudOperacionId = item.Id,
                CodigoSolicitud = item.CodigoSolicitud,
                ImporteTotal = 145000m,
                Estado = EstadoLiquidacion.PendienteDeAuditoria,
                FechaEmision = DateTime.UtcNow,
                VariablesAplicadasResumen = $"Peso {item.PesoTotalKg}kg, Volumen {item.VolumenTotalM3}m3, Distancia {item.DistanciaEstimadaKm}km"
            };
            _mockLiquidaciones.Insert(0, liq);

            return true;
        }
        return false;
    }

    public async Task<List<DarsenaDto>> GetDarsenasAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<List<DarsenaDto>>("api/Darsenas", _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        return _mockDarsenas;
    }

    public async Task<bool> ActualizarEstadoDarsenaAsync(ActualizarEstadoDarsenaDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PutAsJsonAsync("api/Darsenas/estado", dto);
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var d = _mockDarsenas.FirstOrDefault(x => x.Id == dto.DarsenaId);
        if (d != null)
        {
            d.EstadoActual = dto.NuevoEstado;
            d.DetalleOperacionActual = dto.DetalleOperacion ?? $"Actualizado a {dto.NuevoEstado}";
            return true;
        }
        return false;
    }

    public async Task<List<LoteInventarioDto>> GetInventarioAsync(int? clienteId = null, int? darsenaId = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/Inventario";
            var query = new List<string>();
            if (clienteId.HasValue) query.Add($"clienteId={clienteId.Value}");
            if (darsenaId.HasValue) query.Add($"darsenaId={darsenaId.Value}");
            if (query.Any()) url += "?" + string.Join("&", query);

            var res = await _http.GetFromJsonAsync<List<LoteInventarioDto>>(url, _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        var list = _mockInventario.AsEnumerable();
        if (clienteId.HasValue) list = list.Where(l => l.ClienteEmpresaId == clienteId.Value);
        if (darsenaId.HasValue) list = list.Where(l => l.DarsenaId == darsenaId.Value);
        return list.ToList();
    }

    public async Task<List<ViajeDto>> GetMisViajesAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<List<ViajeDto>>("api/Conductores/mis-viajes", _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        return _mockViajes;
    }

    public async Task<ViajeDto?> GetViajeByIdAsync(int id)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<ViajeDto>($"api/Conductores/viaje/{id}", _jsonOptions);
            if (res != null) return res;
        }
        catch { }

        return _mockViajes.FirstOrDefault(v => v.Id == id) ?? _mockViajes.FirstOrDefault();
    }

    public async Task<bool> ActualizarEstadoViajeAsync(int viajeId, ActualizarViajeRequestDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PutAsJsonAsync($"api/Conductores/viaje/{viajeId}/estado", dto);
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var v = _mockViajes.FirstOrDefault(x => x.Id == viajeId);
        if (v != null)
        {
            v.EstadoViaje = dto.EstadoViaje;
            v.ObservacionesChofer = dto.Observaciones;
            return true;
        }
        return false;
    }

    public async Task<List<LiquidacionDto>> GetLiquidacionesAsync(int? clienteId = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/Liquidaciones";
            if (clienteId.HasValue) url += $"?clienteId={clienteId.Value}";
            var res = await _http.GetFromJsonAsync<List<LiquidacionDto>>(url, _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        var list = _mockLiquidaciones.AsEnumerable();
        if (clienteId.HasValue) list = list.Where(l => l.ClienteEmpresaId == clienteId.Value);
        return list.ToList();
    }

    public async Task<bool> AuditarLiquidacionAsync(AuditarLiquidacionDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PostAsJsonAsync("api/Liquidaciones/auditar", dto);
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var item = _mockLiquidaciones.FirstOrDefault(l => l.Id == dto.LiquidacionId);
        if (item != null)
        {
            item.Estado = dto.NuevoEstado;
            item.ObservacionAuditoria = dto.Observacion;
            item.FechaAuditoria = DateTime.UtcNow;
            item.AuditadoPorNombre = "Lucía Pereyra (Auditor)";
            return true;
        }
        return false;
    }

    public async Task<List<TareaCalendarioDto>> GetTareasCalendarioAsync(DateTime? fecha = null)
    {
        try
        {
            await PrepareHttpClientAsync();
            var url = "api/Calendario";
            if (fecha.HasValue) url += $"?fecha={fecha.Value:yyyy-MM-dd}";
            var res = await _http.GetFromJsonAsync<List<TareaCalendarioDto>>(url, _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        return _mockTareas;
    }

    public async Task<TareaCalendarioDto?> CrearTareaCalendarioAsync(CrearTareaCalendarioDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PostAsJsonAsync("api/Calendario", dto);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<TareaCalendarioDto>(_jsonOptions);
            }
        }
        catch { }

        var t = new TareaCalendarioDto
        {
            Id = _mockTareas.Count + 1,
            Titulo = dto.Titulo,
            Detalle = dto.Detalle,
            Fecha = dto.Fecha,
            HoraInicio = dto.HoraInicio,
            HoraFin = dto.HoraFin,
            ResponsableUsuarioId = dto.ResponsableUsuarioId,
            ResponsableNombre = "Martín Benítez",
            ResponsableRol = "Operador",
            TipoActividad = dto.TipoActividad,
            EstadoTarea = "Pendiente"
        };
        _mockTareas.Add(t);
        return t;
    }

    public async Task<bool> CambiarEstadoTareaAsync(int tareaId, string nuevoEstado)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PutAsJsonAsync("api/Calendario/estado", new ActualizarEstadoTareaDto { TareaId = tareaId, NuevoEstado = nuevoEstado });
            if (res.IsSuccessStatusCode) return true;
        }
        catch { }

        var item = _mockTareas.FirstOrDefault(t => t.Id == tareaId);
        if (item != null)
        {
            item.EstadoTarea = nuevoEstado;
            return true;
        }
        return false;
    }

    public async Task<List<ConversacionResumenDto>> GetConversacionesChatAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<List<ConversacionResumenDto>>("api/Chat/conversaciones", _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        return new List<ConversacionResumenDto>
        {
            new() {
                ConversacionId = "solicitud_101",
                Titulo = "SOL-2026-0101 • Distribuidora Pampeana",
                Subtitulo = "Operación Dársena 2 - Chofer Esteban Morales",
                UltimoMensaje = "Camión arribando al predio, balanza de ingreso",
                HoraUltimoMensaje = "08:42",
                MensajesNoLeidos = 1,
                Activo = true,
                SolicitudId = 101
            },
            new() {
                ConversacionId = "solicitud_102",
                Titulo = "SOL-2026-0102 • Frigorífico del Valle",
                Subtitulo = "Despacho Dársena 4 - Chofer Javier Díaz",
                UltimoMensaje = "Pallets verificados y acondicionados en frío",
                HoraUltimoMensaje = "07:15",
                MensajesNoLeidos = 0,
                Activo = true,
                SolicitudId = 102
            }
        };
    }

    public async Task<List<MensajeChatDto>> GetHistorialChatAsync(string conversacionId)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<List<MensajeChatDto>>($"api/Chat/historial/{conversacionId}", _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        return _mockMensajes.Where(m => m.ConversacionId == conversacionId).ToList();
    }

    public async Task<MensajeChatDto?> EnviarMensajeChatAsync(EnviarMensajeDto dto)
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.PostAsJsonAsync("api/Chat/enviar", dto);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<MensajeChatDto>(_jsonOptions);
            }
        }
        catch { }

        var user = await _tokenStorage.GetUserAsync();
        var msg = new MensajeChatDto
        {
            Id = _mockMensajes.Count + 1,
            ConversacionId = dto.ConversacionId,
            RemitenteUsuarioId = user?.Id ?? 1,
            RemitenteNombre = user?.NombreCompleto ?? "Usuario",
            RemitenteRol = user?.Rol.ToString() ?? "Operador",
            TextoMensaje = dto.TextoMensaje,
            EsIncidenteRuta = dto.EsIncidenteRuta,
            FechaHoraEnvio = DateTime.UtcNow
        };
        _mockMensajes.Add(msg);
        return msg;
    }

    public async Task<List<ContratoAlquilerDarsenaDto>> GetMisContratosAsync()
    {
        try
        {
            await PrepareHttpClientAsync();
            var res = await _http.GetFromJsonAsync<List<ContratoAlquilerDarsenaDto>>("api/Contratos/mis-contratos", _jsonOptions);
            if (res != null && res.Count > 0) return res;
        }
        catch { }

        return new List<ContratoAlquilerDarsenaDto>
        {
            new() {
                Id = 1,
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                DarsenaId = 2,
                DarsenaCodigo = "Dársena 2",
                Plan = PlanAlquilerDarsena.Semestral,
                FechaInicio = DateTime.UtcNow.AddMonths(-2),
                FechaFin = DateTime.UtcNow.AddMonths(4),
                MontoTotalContrato = 820000m,
                Estado = EstadoContratoDarsena.Activo,
                CapacidadMaximaM3 = 120m,
                VolumenOcupadoM3 = 45m,
                MercaderiaAlmacenar = "Alimentos no perecederos y bebidas"
            }
        };
    }

    private static void EnsureMockData()
    {
        if (_mockInitialized) return;
        _mockInitialized = true;

        // Darsenas 1 a 6
        _mockDarsenas.AddRange(new[]
        {
            new DarsenaDto { Id = 1, NumeroDarsena = 1, Codigo = "Dársena 1", CapacidadMaximaPallets = 32, CapacidadMaximaM3 = 140m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible para turnos", VolumenOcupadoM3 = 0 },
            new DarsenaDto { Id = 2, NumeroDarsena = 2, Codigo = "Dársena 2", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Ocupada, ClienteRazonSocial = "Distribuidora Pampeana S.A.", DetalleOperacionActual = "Descargando SOL-2026-0101", VolumenOcupadoM3 = 75m, ProximoTurnoHora = "11:30" },
            new DarsenaDto { Id = 3, NumeroDarsena = 3, Codigo = "Dársena 3", CapacidadMaximaPallets = 28, CapacidadMaximaM3 = 110m, EstadoActual = EstadoDarsena.TurnoAsignado, DetalleOperacionActual = "Turno asignado a Frigorífico del Valle", VolumenOcupadoM3 = 30m, ProximoTurnoHora = "14:00" },
            new DarsenaDto { Id = 4, NumeroDarsena = 4, Codigo = "Dársena 4", CapacidadMaximaPallets = 36, CapacidadMaximaM3 = 150m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora", VolumenOcupadoM3 = 20m },
            new DarsenaDto { Id = 5, NumeroDarsena = 5, Codigo = "Dársena 5", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora", VolumenOcupadoM3 = 0 },
            new DarsenaDto { Id = 6, NumeroDarsena = 6, Codigo = "Dársena 6", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Mantenimiento, DetalleOperacionActual = "Mantenimiento preventivo rampa hidráulica", VolumenOcupadoM3 = 0 }
        });

        // Solicitudes
        _mockSolicitudes.AddRange(new[]
        {
            new SolicitudOperacionDto
            {
                Id = 101,
                CodigoSolicitud = "SOL-2026-0101",
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                DarsenaId = 2,
                DarsenaCodigo = "Dársena 2",
                TipoOperacion = TipoOperacionSolicitud.ArriboIngreso,
                ModalidadTransporte = ModalidadTransporte.FlotaLCP,
                ConductorAsignadoId = 5,
                ConductorNombreCompleto = "Esteban Morales (Camión Volvo FH 460)",
                ConductorPatente = "AF-782-LP",
                ResponsableDepositoId = 2,
                ResponsableDepositoNombre = "Martín Benítez",
                DescripcionMercaderia = "20 Pallets de Harina 000 y Alimentos Envasados",
                PesoTotalKg = 8500m,
                VolumenTotalM3 = 28.5m,
                DistanciaEstimadaKm = 145m,
                CiudadOrigen = "General Pico",
                CiudadDestino = "Santa Rosa (Base LCP)",
                FechaHoraTurnoProgramado = DateTime.UtcNow.AddHours(2),
                Estado = EstadoSolicitud.EnCurso,
                FechaSalidaCliente = DateTime.UtcNow.AddHours(-1.5)
            },
            new SolicitudOperacionDto
            {
                Id = 102,
                CodigoSolicitud = "SOL-2026-0102",
                ClienteEmpresaId = 2,
                ClienteRazonSocial = "Frigorífico del Valle S.R.L.",
                DarsenaId = 3,
                DarsenaCodigo = "Dársena 3",
                TipoOperacion = TipoOperacionSolicitud.DespachoRetiro,
                ModalidadTransporte = ModalidadTransporte.ChoferPropio,
                ConductorAsignadoId = 6,
                ConductorNombreCompleto = "Javier Díaz (Scania R450)",
                ConductorPatente = "AE-512-AB",
                ResponsableDepositoId = 2,
                ResponsableDepositoNombre = "Martín Benítez",
                DescripcionMercaderia = "12 Pallets de Productos Lácteos Refrigerados",
                PesoTotalKg = 4200m,
                VolumenTotalM3 = 18m,
                DistanciaEstimadaKm = 80m,
                CiudadOrigen = "Santa Rosa (Base LCP)",
                CiudadDestino = "Toay",
                FechaHoraTurnoProgramado = DateTime.UtcNow.AddHours(5),
                Estado = EstadoSolicitud.Programada
            },
            new SolicitudOperacionDto
            {
                Id = 103,
                CodigoSolicitud = "SOL-2026-0098",
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                DarsenaId = 2,
                DarsenaCodigo = "Dársena 2",
                TipoOperacion = TipoOperacionSolicitud.ArriboIngreso,
                ModalidadTransporte = ModalidadTransporte.ChoferPropio,
                DescripcionMercaderia = "Cajas de Aceite de Girasol y Conservas",
                PesoTotalKg = 6100m,
                VolumenTotalM3 = 22m,
                DistanciaEstimadaKm = 120m,
                CiudadOrigen = "Realicó",
                CiudadDestino = "Santa Rosa (Base LCP)",
                FechaHoraTurnoProgramado = DateTime.UtcNow.AddDays(-1),
                FechaHoraEgresoReal = DateTime.UtcNow.AddDays(-1).AddHours(3),
                Estado = EstadoSolicitud.Completada
            }
        });

        // Inventario
        _mockInventario.AddRange(new[]
        {
            new LoteInventarioDto
            {
                Id = 1,
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                DarsenaId = 2,
                DarsenaCodigo = "Dársena 2",
                CodigoLote = "LOT-PAM-2026-01",
                ProductoDescripcion = "Harina 000 Molino Cañuelas (Bolsas 25kg)",
                CantidadUnidades = 320,
                CantidadBultosCajas = 320,
                PesoKg = 8000m,
                VolumenM3 = 24m,
                UbicacionInternaDarsena = "Racks A1 - Nivel 1 y 2",
                FechaIngreso = DateTime.UtcNow.AddDays(-4),
                OrdenadoPorOperadorNombre = "Martín Benítez"
            },
            new LoteInventarioDto
            {
                Id = 2,
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                DarsenaId = 2,
                DarsenaCodigo = "Dársena 2",
                CodigoLote = "LOT-PAM-2026-02",
                ProductoDescripcion = "Aceite de Girasol Natura 1.5L (Cajas x 12)",
                CantidadUnidades = 600,
                CantidadBultosCajas = 50,
                PesoKg = 900m,
                VolumenM3 = 6.5m,
                UbicacionInternaDarsena = "Racks B2 - Nivel 1",
                FechaIngreso = DateTime.UtcNow.AddDays(-2),
                OrdenadoPorOperadorNombre = "Martín Benítez"
            }
        });

        // Viajes
        _mockViajes.AddRange(new[]
        {
            new ViajeDto
            {
                Id = 1,
                SolicitudOperacionId = 101,
                CodigoSolicitud = "SOL-2026-0101",
                ConductorId = 5,
                ConductorNombre = "Esteban Morales",
                PatenteCamion = "AF-782-LP",
                Origen = "General Pico",
                Destino = "Santa Rosa (Base LCP)",
                PuertaIngresoAsignada = "Portón Norte Dársena 2",
                EstadoViaje = EstadoViaje.EnRuta,
                UltimaLatitud = -36.1245,
                UltimaLongitud = -64.2812,
                UltimaActualizacionGps = DateTime.UtcNow.AddMinutes(-5),
                ObservacionesChofer = "Tránsito fluido por Ruta Provincial 1"
            }
        });

        // Liquidaciones
        _mockLiquidaciones.AddRange(new[]
        {
            new LiquidacionDto
            {
                Id = 1,
                NumeroComprobante = "LIQ-2026-0089",
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                ClienteCUIT = "30-71458923-4",
                TipoLiquidacion = TipoLiquidacion.ServicioTransporteYPatio,
                SolicitudOperacionId = 103,
                CodigoSolicitud = "SOL-2026-0098",
                DescripcionMercaderia = "Aceite de Girasol y Conservas",
                PesoTotalKg = 6100m,
                VolumenTotalM3 = 22m,
                VariablesAplicadasResumen = "6100 kg x $50 + 22 m³ x $8000 + 120 km x $1200 + Descarga Operador $15000",
                SubtotalPeso = 305000m,
                SubtotalVolumen = 176000m,
                SubtotalDistancia = 144000m,
                SubtotalTiempoDepositoOAlquiler = 15000m,
                ImporteTotal = 640000m,
                Estado = EstadoLiquidacion.PendienteDeAuditoria,
                FechaEmision = DateTime.UtcNow.AddDays(-1)
            },
            new LiquidacionDto
            {
                Id = 2,
                NumeroComprobante = "LIQ-2026-0072",
                ClienteEmpresaId = 1,
                ClienteRazonSocial = "Distribuidora Pampeana S.A.",
                TipoLiquidacion = TipoLiquidacion.CanonAlquilerDarsena,
                DarsenaCodigo = "Dársena 2",
                VariablesAplicadasResumen = "Plan Semestral Dársena 2 (6 meses)",
                ImporteTotal = 820000m,
                Estado = EstadoLiquidacion.Validada,
                AuditadoPorNombre = "Lucía Pereyra (Auditor)",
                FechaAuditoria = DateTime.UtcNow.AddDays(-15),
                FechaEmision = DateTime.UtcNow.AddDays(-16)
            }
        });

        // Tareas Calendario
        _mockTareas.AddRange(new[]
        {
            new TareaCalendarioDto
            {
                Id = 1,
                Titulo = "Inspección de Descarga SOL-0101",
                Detalle = "Verificar precintos de camión y control térmico en Dársena 2",
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                HoraInicio = new TimeOnly(10, 0),
                HoraFin = new TimeOnly(11, 30),
                ResponsableUsuarioId = 2,
                ResponsableNombre = "Martín Benítez",
                ResponsableRol = "Operador",
                SolicitudOperacionId = 101,
                TipoActividad = "Tarea",
                EstadoTarea = "Pendiente"
            },
            new TareaCalendarioDto
            {
                Id = 2,
                Titulo = "Reunión de Coordinación Logística Semanal",
                Detalle = "Revisión de volumen de ocupación de dársenas y nuevas altas de clientes",
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                HoraInicio = new TimeOnly(14, 0),
                HoraFin = new TimeOnly(15, 0),
                ResponsableUsuarioId = 1,
                ResponsableNombre = "Roberto Gómez",
                ResponsableRol = "Administrador",
                TipoActividad = "Reunion",
                EstadoTarea = "Pendiente"
            },
            new TareaCalendarioDto
            {
                Id = 3,
                Titulo = "Auditoría de Liquidaciones Flota LCP",
                Detalle = "Validación de comprobantes de flete y pesaje",
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                HoraInicio = new TimeOnly(16, 0),
                HoraFin = new TimeOnly(17, 30),
                ResponsableUsuarioId = 3,
                ResponsableNombre = "Lucía Pereyra",
                ResponsableRol = "Auditor",
                TipoActividad = "Tarea",
                EstadoTarea = "Pendiente"
            }
        });

        // Chat
        _mockMensajes.AddRange(new[]
        {
            new MensajeChatDto
            {
                Id = 1,
                ConversacionId = "solicitud_101",
                RemitenteUsuarioId = 5,
                RemitenteNombre = "Esteban Morales",
                RemitenteRol = "Conductor",
                TextoMensaje = "Buenos días equipo. Salí de General Pico a las 07:30 hs con carga completa.",
                FechaHoraEnvio = DateTime.UtcNow.AddHours(-1)
            },
            new MensajeChatDto
            {
                Id = 2,
                ConversacionId = "solicitud_101",
                RemitenteUsuarioId = 2,
                RemitenteNombre = "Martín Benítez",
                RemitenteRol = "Operador",
                TextoMensaje = "Recibido Esteban. Dársena 2 despejada y lista para tu arribo.",
                FechaHoraEnvio = DateTime.UtcNow.AddMinutes(-35)
            },
            new MensajeChatDto
            {
                Id = 3,
                ConversacionId = "solicitud_101",
                RemitenteUsuarioId = 5,
                RemitenteNombre = "Esteban Morales",
                RemitenteRol = "Conductor",
                TextoMensaje = "Camión arribando al predio, balanza de ingreso.",
                FechaHoraEnvio = DateTime.UtcNow.AddMinutes(-10)
            }
        });
    }
}
