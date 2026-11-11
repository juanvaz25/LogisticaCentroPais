using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Shared.DTOs;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TarifasController : ControllerBase
{
    private readonly LcpDbContext _db;

    public TarifasController(LcpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<TarifaConfiguracionDto>>> GetTarifas()
    {
        var tarifas = await _db.TarifasConfiguracion
            .Include(t => t.ActualizadoPorUsuario)
            .OrderBy(t => t.Id)
            .ToListAsync();

        var dtos = tarifas.Select(t => new TarifaConfiguracionDto
        {
            Id = t.Id,
            CodigoConcepto = t.CodigoConcepto,
            Descripcion = t.Descripcion,
            ValorUnitario = t.ValorUnitario,
            UltimaActualizacion = t.UltimaActualizacion,
            ActualizadoPorUsuarioId = t.ActualizadoPorUsuarioId,
            ActualizadoPorNombre = t.ActualizadoPorUsuario?.NombreCompleto ?? "Administrador"
        }).ToList();

        return Ok(dtos);
    }

    [HttpPut]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> ActualizarTarifa([FromBody] ActualizarTarifaDto dto)
    {
        var tarifa = await _db.TarifasConfiguracion
            .FirstOrDefaultAsync(t => t.CodigoConcepto == dto.CodigoConcepto);

        if (tarifa == null) return NotFound(new { Mensaje = "Tarifa no encontrada." });

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int adminId);

        tarifa.ValorUnitario = dto.NuevoValor;
        tarifa.UltimaActualizacion = DateTime.UtcNow;
        if (adminId > 0) tarifa.ActualizadoPorUsuarioId = adminId;

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Tarifa '{tarifa.Descripcion}' actualizada a ${dto.NuevoValor:N2}" });
    }
}
