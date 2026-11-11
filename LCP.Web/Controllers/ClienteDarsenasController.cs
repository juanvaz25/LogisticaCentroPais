using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;
using LCP.Web.Models;
using LCP.Web.Services;

namespace LCP.Web.Controllers;

[Authorize(Roles = "Cliente,Operador,Administrador")]
public class ClienteDarsenasController : Controller
{
    private readonly ILcpApiClient _apiClient;

    public ClienteDarsenasController(ILcpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> MisContratos()
    {
        var contratos = await _apiClient.GetContratosAsync();
        var darsenas = await _apiClient.GetDarsenasAsync();
        var tarifas = await _apiClient.GetTarifasAsync();

        ViewBag.DarsenasDisponibles = darsenas.Where(d => d.ClienteAlquilerActualId == null && d.EstadoActual != EstadoDarsena.Mantenimiento).ToList();
        ViewBag.Tarifas = tarifas;

        return View(contratos);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlquilarDarsena(int darsenaId, PlanAlquilerDarsena plan, string mercaderiaAlmacenar, string tipoAlmacenamiento, string? consideracionesEspeciales)
    {
        int clienteId = 1;
        var claimClienteId = User.FindFirstValue("ClienteId");
        if (int.TryParse(claimClienteId, out int cid)) clienteId = cid;

        if (string.IsNullOrWhiteSpace(mercaderiaAlmacenar) || string.IsNullOrWhiteSpace(tipoAlmacenamiento))
        {
            TempData["Error"] = "Debe especificar la mercadería a almacenar y el tipo de almacenamiento requerido.";
            return RedirectToAction(nameof(MisContratos));
        }

        var dto = new CrearContratoDarsenaDto
        {
            ClienteEmpresaId = clienteId,
            DarsenaId = darsenaId,
            Plan = plan,
            MercaderiaAlmacenar = mercaderiaAlmacenar,
            TipoAlmacenamiento = tipoAlmacenamiento,
            ConsideracionesEspeciales = consideracionesEspeciales
        };

        var contrato = await _apiClient.CrearContratoAsync(dto);
        if (contrato != null)
        {
            TempData["Mensaje"] = $"¡Solicitud para Dársena #{darsenaId} enviada exitosamente! El Auditor evaluará los requerimientos de almacenamiento. Una vez aceptada, podrá cargar la factura/comprobante de pago para habilitar su uso.";
        }
        else
        {
            TempData["Error"] = "No se pudo crear la solicitud. Verifique que la dársena esté disponible o no posea una solicitud activa.";
        }

        return RedirectToAction(nameof(MisContratos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CargarFacturaContrato(int contratoId, IFormFile archivoFactura)
    {
        if (archivoFactura == null || archivoFactura.Length == 0)
        {
            TempData["Error"] = "Debe seleccionar un archivo válido de factura o comprobante de pago.";
            return RedirectToAction(nameof(MisContratos));
        }

        var ok = await _apiClient.CargarFacturaContratoAsync(contratoId, archivoFactura);
        if (ok)
        {
            TempData["Mensaje"] = "¡Factura/comprobante cargado exitosamente! La dársena ha sido formalmente asignada y habilitada para operar.";
        }
        else
        {
            TempData["Error"] = "Ocurrió un error al registrar el comprobante de pago. Verifique el estado de la solicitud.";
        }

        return RedirectToAction(nameof(MisContratos));
    }

    [HttpGet]
    public async Task<IActionResult> MiStock(int? darsenaId)
    {
        var stock = await _apiClient.GetInventarioAsync();
        var contratos = await _apiClient.GetContratosAsync();
        var darsenasInfo = await _apiClient.GetDarsenasAsync();

        var viewModel = new MiStockViewModel
        {
            DarsenaSeleccionadaId = darsenaId
        };

        // Contratos del cliente (activos preferentemente, o todos si no hay activos)
        var contratosCliente = contratos
            .Where(c => c.Estado == EstadoContratoDarsena.Activo && c.FechaFin >= DateTime.UtcNow)
            .ToList();

        if (!contratosCliente.Any())
        {
            contratosCliente = contratos.ToList();
        }

        // Si es Operador/Administrador sin contratos propios asignados, mostrar dársenas del predio con inventario
        if ((User.IsInRole(Roles.Operador) || User.IsInRole(Roles.Administrador)) && !contratosCliente.Any())
        {
            foreach (var d in darsenasInfo)
            {
                var lotesDarsena = stock.Where(l => l.DarsenaId == d.Id).ToList();
                viewModel.Darsenas.Add(new DarsenaInventarioItemViewModel
                {
                    DarsenaId = d.Id,
                    DarsenaCodigo = d.Codigo,
                    ClienteRazonSocial = d.ClienteRazonSocial ?? "Sin inquilino asignado",
                    CapacidadPallets = d.CapacidadMaximaPallets,
                    CapacidadM3 = d.CapacidadMaximaM3,
                    EstaVigente = true,
                    Lotes = lotesDarsena
                });
            }
        }
        else
        {
            // Para el cliente: cada dársena que tiene alquilada es su espacio de acopio/inventario
            foreach (var c in contratosCliente)
            {
                var dInfo = darsenasInfo.FirstOrDefault(d => d.Id == c.DarsenaId);
                var lotesDarsena = stock.Where(l => l.DarsenaId == c.DarsenaId).ToList();

                viewModel.Darsenas.Add(new DarsenaInventarioItemViewModel
                {
                    DarsenaId = c.DarsenaId,
                    DarsenaCodigo = c.DarsenaCodigo,
                    ContratoId = c.Id,
                    ClienteRazonSocial = c.ClienteRazonSocial,
                    Plan = c.Plan,
                    FechaInicio = c.FechaInicio,
                    FechaFin = c.FechaFin,
                    EstaVigente = c.EstaVigente,
                    EstadoContrato = c.Estado,
                    CapacidadPallets = dInfo?.CapacidadMaximaPallets ?? 30,
                    CapacidadM3 = dInfo?.CapacidadMaximaM3 ?? 120m,
                    Lotes = lotesDarsena
                });
            }

            // Si existen lotes en alguna dársena que no estuviera en los contratos activos listados
            var darsenasIdsConLotes = stock.Select(l => l.DarsenaId).Distinct().ToList();
            foreach (var did in darsenasIdsConLotes)
            {
                if (!viewModel.Darsenas.Any(d => d.DarsenaId == did))
                {
                    var lotesExtra = stock.Where(l => l.DarsenaId == did).ToList();
                    var dInfo = darsenasInfo.FirstOrDefault(d => d.Id == did);
                    viewModel.Darsenas.Add(new DarsenaInventarioItemViewModel
                    {
                        DarsenaId = did,
                        DarsenaCodigo = dInfo?.Codigo ?? $"Dársena #{did}",
                        ClienteRazonSocial = lotesExtra.FirstOrDefault()?.ClienteRazonSocial ?? "Mi Empresa",
                        CapacidadPallets = dInfo?.CapacidadMaximaPallets ?? 30,
                        CapacidadM3 = dInfo?.CapacidadMaximaM3 ?? 120m,
                        EstaVigente = true,
                        Lotes = lotesExtra
                    });
                }
            }
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> MisChoferes()
    {
        var choferes = await _apiClient.GetConductoresAsync(tipo: TipoConductor.PropioCliente);
        return View(choferes);
    }

    [HttpGet]
    public IActionResult RegistrarChofer()
    {
        var model = new PostulacionChoferDto
        {
            FechaNacimiento = DateTime.Now.AddYears(-30).Date,
            Localidad = "Santa Rosa",
            Provincia = "La Pampa",
            CapacidadCargaKg = 14000,
            CapacidadVolumenM3 = 60,
            DeclaracionJuradaAceptada = true
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarChofer(PostulacionChoferDto model, IFormFile? archivoLicencia, IFormFile? archivoSeguro)
    {
        int? clienteId = null;
        var claimClienteId = User.FindFirstValue("ClienteId");
        if (int.TryParse(claimClienteId, out int cid)) clienteId = cid;
        model.ClienteEmpresaId = clienteId;
        model.TipoConductor = TipoConductor.PropioCliente;
        model.DeclaracionJuradaAceptada = true;

        if (string.IsNullOrWhiteSpace(model.Nombre) || string.IsNullOrWhiteSpace(model.Apellido) ||
            string.IsNullOrWhiteSpace(model.CUIL) || string.IsNullOrWhiteSpace(model.PatenteCamion))
        {
            TempData["Error"] = "Complete los campos obligatorios (*) del chofer y camión.";
            return View(model);
        }

        // Subir documentación respaldatoria si fue adjuntada
        if (archivoLicencia != null && archivoLicencia.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoLicencia, "LicenciaConducirLINTI");
            if (doc != null) model.LicenciaArchivoUrl = doc.RutaUrl;
        }

        if (archivoSeguro != null && archivoSeguro.Length > 0)
        {
            var doc = await _apiClient.SubirDocumentoAsync(archivoSeguro, "SeguroCamionART");
            if (doc != null) model.SeguroCamionArchivoUrl = doc.RutaUrl;
        }

        var (exito, mensaje) = await _apiClient.CrearChoferPropioAsync(model);
        if (exito)
        {
            TempData["Mensaje"] = $"Chofer {model.Nombre} {model.Apellido} (Camión {model.PatenteCamion.ToUpper()}) registrado exitosamente. Ha quedado pendiente de validación y aprobación por la Administración de LCP antes de poder utilizarse en viajes.";
            return RedirectToAction(nameof(MisChoferes));
        }

        TempData["Error"] = mensaje;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DarDeBajaChofer(int id)
    {
        var exito = await _apiClient.DarDeBajaConductorAsync(id);
        if (exito)
        {
            TempData["Mensaje"] = "El chofer ha sido dado de baja exitosamente del padrón de tu empresa.";
        }
        else
        {
            TempData["Error"] = "No se pudo dar de baja al chofer seleccionado.";
        }
        return RedirectToAction(nameof(MisChoferes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoChoferPropio(PostulacionChoferDto model, IFormFile? archivoLicencia, IFormFile? archivoSeguro)
    {
        return await RegistrarChofer(model, archivoLicencia, archivoSeguro);
    }
}
