using Backend.Datos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CatalogosController(AppDbContext db) : ControllerBase
{
    [HttpGet("categorias")]
    public async Task<IActionResult> Categorias()
    {
        var ordenadas = db.Categorias.OrderBy(c => c.Nombre);
        var lista = await ordenadas.Select(c => new { id = c.CategoriaId, c.Nombre }).ToListAsync();
        return Ok(lista);
    }

    [HttpGet("clientes")]
    public async Task<IActionResult> Clientes()
    {
        var ordenados = db.Clientes.OrderBy(c => c.Nombre);
        var lista = await ordenados.Select(c => new { id = c.ClienteId, c.Nombre, c.Telefono }).ToListAsync();
        return Ok(lista);
    }
}
