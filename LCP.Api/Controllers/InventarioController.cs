using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly LcpDbContext _db;

    public InventarioController(LcpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<LoteInventarioDto>>> GetInventario([FromQuery] int? darsenaId)
    {
        var query = _db.LotesInventario
            .Include(l => l.ClienteEmpresa)
            .Include(l => l.Darsena)
            .Include(l => l.OrdenadoPorOperador)
            .AsQueryable();

        // RBAC: Cliente ve sólo su propio inventario
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId))
            {
                query = query.Where(l => l.ClienteEmpresaId == clienteId);
            }
            else
            {
                return Ok(new List<LoteInventarioDto>());
            }
        }

        if (darsenaId.HasValue)
        {
            query = query.Where(l => l.DarsenaId == darsenaId.Value);
        }

        var list = await query.OrderByDescending(l => l.FechaIngreso).ToListAsync();

        var dtos = list.Select(l => new LoteInventarioDto
        {
            Id = l.Id,
            ClienteEmpresaId = l.ClienteEmpresaId,
            ClienteRazonSocial = l.ClienteEmpresa?.RazonSocial ?? "N/A",
            DarsenaId = l.DarsenaId,
            DarsenaCodigo = l.Darsena?.Codigo ?? $"Dársena #{l.DarsenaId}",
            SolicitudIngresoId = l.SolicitudIngresoId,
            CodigoLote = l.CodigoLote,
            ProductoDescripcion = l.ProductoDescripcion,
            CantidadUnidades = l.CantidadUnidades,
            CantidadBultosCajas = l.CantidadBultosCajas,
            PesoKg = l.PesoKg,
            VolumenM3 = l.VolumenM3,
            UbicacionInternaDarsena = l.UbicacionInternaDarsena,
            FechaIngreso = l.FechaIngreso,
            OrdenadoPorOperadorId = l.OrdenadoPorOperadorId,
            OrdenadoPorOperadorNombre = l.OrdenadoPorOperador?.NombreCompleto ?? "Operador de turno"
        }).ToList();

        return Ok(dtos);
    }
}
