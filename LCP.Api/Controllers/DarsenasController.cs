using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Hubs;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DarsenasController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly IHubContext<PatioHub> _patioHub;

    public DarsenasController(LcpDbContext db, IHubContext<PatioHub> patioHub)
    {
        _db = db;
        _patioHub = patioHub;
    }

    [HttpGet]
    [AllowAnonymous] // Public and authenticated can view the patio overview
    public async Task<ActionResult<List<DarsenaDto>>> GetDarsenas()
    {
        var darsenas = await _db.Darsenas
            .Include(d => d.ClienteAlquilerActual)
            .OrderBy(d => d.NumeroDarsena)
            .ToListAsync();

        var ocupaciones = await _db.LotesInventario
            .GroupBy(l => l.DarsenaId)
            .Select(g => new { DarsenaId = g.Key, VolumenOcupado = g.Sum(l => l.VolumenM3) })
            .ToDictionaryAsync(x => x.DarsenaId, x => x.VolumenOcupado);

        var dtos = darsenas.Select(d => new DarsenaDto
        {
            Id = d.Id,
            NumeroDarsena = d.NumeroDarsena,
            Codigo = d.Codigo,
            CapacidadMaximaPallets = d.CapacidadMaximaPallets,
            CapacidadMaximaM3 = d.CapacidadMaximaM3,
            EstadoActual = d.EstadoActual,
            ClienteAlquilerActualId = d.ClienteAlquilerActualId,
            ClienteRazonSocial = d.ClienteAlquilerActual?.RazonSocial,
            DetalleOperacionActual = d.DetalleOperacionActual,
            ProximoTurnoHora = d.ProximoTurnoHora,
            ProximoTurnoDetalle = d.ProximoTurnoDetalle,
            VolumenOcupadoM3 = ocupaciones.GetValueOrDefault(d.Id, 0m)
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DarsenaDto>> GetDarsena(int id)
    {
        var d = await _db.Darsenas
            .Include(d => d.ClienteAlquilerActual)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (d == null) return NotFound();

        decimal ocupado = await _db.LotesInventario
            .Where(l => l.DarsenaId == id)
            .SumAsync(l => (decimal?)l.VolumenM3) ?? 0m;

        return Ok(new DarsenaDto
        {
            Id = d.Id,
            NumeroDarsena = d.NumeroDarsena,
            Codigo = d.Codigo,
            CapacidadMaximaPallets = d.CapacidadMaximaPallets,
            CapacidadMaximaM3 = d.CapacidadMaximaM3,
            EstadoActual = d.EstadoActual,
            ClienteAlquilerActualId = d.ClienteAlquilerActualId,
            ClienteRazonSocial = d.ClienteAlquilerActual?.RazonSocial,
            DetalleOperacionActual = d.DetalleOperacionActual,
            ProximoTurnoHora = d.ProximoTurnoHora,
            ProximoTurnoDetalle = d.ProximoTurnoDetalle,
            VolumenOcupadoM3 = ocupado
        });
    }

    [HttpPut("{id}/estado")]
    [Authorize(Roles = "Operador,Administrador")]
    public async Task<ActionResult<DarsenaDto>> ActualizarEstado(int id, [FromBody] ActualizarEstadoDarsenaDto dto)
    {
        var darsena = await _db.Darsenas
            .Include(d => d.ClienteAlquilerActual)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (darsena == null) return NotFound();

        darsena.EstadoActual = dto.NuevoEstado;
        if (dto.DetalleOperacion != null)
        {
            darsena.DetalleOperacionActual = dto.DetalleOperacion;
        }

        if (dto.NuevoEstado == EstadoDarsena.Libre)
        {
            darsena.DetalleOperacionActual = "Disponible ahora";
        }

        await _db.SaveChangesAsync();

        var darsenaDto = new DarsenaDto
        {
            Id = darsena.Id,
            NumeroDarsena = darsena.NumeroDarsena,
            Codigo = darsena.Codigo,
            CapacidadMaximaPallets = darsena.CapacidadMaximaPallets,
            CapacidadMaximaM3 = darsena.CapacidadMaximaM3,
            EstadoActual = darsena.EstadoActual,
            ClienteAlquilerActualId = darsena.ClienteAlquilerActualId,
            ClienteRazonSocial = darsena.ClienteAlquilerActual?.RazonSocial,
            DetalleOperacionActual = darsena.DetalleOperacionActual,
            ProximoTurnoHora = darsena.ProximoTurnoHora,
            ProximoTurnoDetalle = darsena.ProximoTurnoDetalle
        };

        // Real-time broadcast to SignalR clients!
        await _patioHub.Clients.All.SendAsync("DarsenaActualizada", darsenaDto);

        return Ok(darsenaDto);
    }
}
