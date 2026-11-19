using System.Text.Json;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosProducto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int CategoriaId { get; set; }
    public string Unidad { get; set; } = "";
    public int Precio { get; set; }
    public int Stock { get; set; }
    public decimal? Litros { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

public class DatosSugerencia
{
    public string Nombre { get; set; } = "";
    public string? Categoria { get; set; }
    public string? Unidad { get; set; }
}

[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(string? busqueda)
    {
        var consulta = db.Productos.AsQueryable();
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            string texto = busqueda.Trim();
            consulta = consulta.Where(p => p.Codigo.Contains(texto) || p.Nombre.Contains(texto) || (p.Descripcion != null && p.Descripcion.Contains(texto)));
        }

        var ordenados = consulta.OrderBy(p => p.Nombre);
        var lista = await ordenados.Select(p => new
        {
            id = p.ProductoId,
            p.Codigo,
            p.Nombre,
            p.CategoriaId,
            categoria = p.Categoria!.Nombre,
            p.Unidad,
            p.Precio,
            p.Stock,
            p.Litros,
            p.Activo,
            p.Descripcion,
        }).ToListAsync();
        return Ok(lista);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var p = await db.Productos.FirstOrDefaultAsync(p => p.ProductoId == id);
        if (p == null)
        {
            return NotFound(new { mensaje = "No se encontró el producto." });
        }
        return Ok(new { id = p.ProductoId, p.Codigo, p.Nombre, p.CategoriaId, p.Unidad, p.Precio, p.Stock, p.Litros, p.Activo, p.Descripcion });
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Crear(DatosProducto datos)
    {
        string? error = await Validar(datos, 0);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        var producto = new Producto();
        Copiar(datos, producto);
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        return Ok(new { id = producto.ProductoId });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Editar(int id, DatosProducto datos)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.ProductoId == id);
        if (producto == null)
        {
            return NotFound(new { mensaje = "No se encontró el producto." });
        }
        string? error = await Validar(datos, id);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        Copiar(datos, producto);
        await db.SaveChangesAsync();
        return Ok(new { id });
    }

    [HttpPost("sugerir-descripcion")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SugerirDescripcion(DatosSugerencia datos, [FromServices] Asistente asistente)
    {
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return BadRequest(new { mensaje = "Escribe el nombre del producto." });
        }

        string categoria = datos.Categoria ?? "";
        string unidad = datos.Unidad ?? "";
        string texto = await asistente.SugerirDescripcion(datos.Nombre, categoria, unidad);
        if (texto == "")
        {
            return BadRequest(new { mensaje = "La IA no pudo proponer una descripción para este producto. Escríbela a mano." });
        }
        return Ok(new { descripcion = texto });
    }

    [HttpPost("sincronizar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Sincronizar([FromServices] Erp erp, [FromServices] ILogger<ProductosController> log)
    {
        try
        {
            var resultado = await erp.Sincronizar();
            return Ok(new { mensaje = $"Catálogo sincronizado con el ERP: {resultado.Actualizados} productos actualizados y {resultado.Nuevos} nuevos." });
        }
        catch (DbUpdateException error)
        {
            log.LogError(error, "El ERP envió datos que la base de datos rechazó");
            return StatusCode(502, new { mensaje = "El ERP envió datos inválidos." });
        }
        catch (Exception error) when (error is HttpRequestException || error is InvalidOperationException || error is TaskCanceledException || error is JsonException)
        {
            log.LogError(error, "Falló la sincronización con el ERP");
            return StatusCode(502, new { mensaje = "No se pudo conectar con el ERP. Se mantienen los datos anteriores." });
        }
    }

    async Task<string?> Validar(DatosProducto datos, int id)
    {
        if (string.IsNullOrWhiteSpace(datos.Codigo))
        {
            return "Escribe el código.";
        }
        if (datos.Codigo.Trim().Length > 30)
        {
            return "El código admite hasta 30 caracteres.";
        }
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return "Escribe el nombre.";
        }
        if (datos.Nombre.Trim().Length > 150)
        {
            return "El nombre admite hasta 150 caracteres.";
        }
        if (datos.CategoriaId < 1)
        {
            return "Elige una categoría.";
        }
        if (string.IsNullOrWhiteSpace(datos.Unidad))
        {
            return "Elige la unidad de venta.";
        }
        if (datos.Unidad.Length > 20)
        {
            return "La unidad de venta admite hasta 20 caracteres.";
        }
        if (datos.Precio < 0)
        {
            return "El precio no puede ser negativo.";
        }
        if (datos.Precio > 100_000_000)
        {
            return "El precio no puede superar $100.000.000.";
        }
        if (datos.Stock < 0)
        {
            return "El stock no puede ser negativo.";
        }
        if (datos.Stock > 1_000_000)
        {
            return "El stock no puede superar 1.000.000.";
        }
        if (datos.Litros != null && (datos.Litros <= 0 || datos.Litros > 999))
        {
            return "Los litros deben ser mayores que 0 y menores que 1.000. Déjalo vacío si el producto no es una pintura.";
        }
        if (datos.Descripcion != null && datos.Descripcion.Length > 1000)
        {
            return "La descripción admite hasta 1.000 caracteres.";
        }

        string codigo = datos.Codigo.Trim();
        bool codigoOcupado = await db.Productos.AnyAsync(p => p.Codigo == codigo && p.ProductoId != id);
        if (codigoOcupado)
        {
            return "Ya existe otro producto con ese código.";
        }
        bool categoriaExiste = await db.Categorias.AnyAsync(c => c.CategoriaId == datos.CategoriaId);
        if (!categoriaExiste)
        {
            return "La categoría no existe.";
        }
        return null;
    }

    static void Copiar(DatosProducto datos, Producto producto)
    {
        producto.Codigo = datos.Codigo.Trim();
        producto.Nombre = datos.Nombre.Trim();
        producto.CategoriaId = datos.CategoriaId;
        producto.Unidad = datos.Unidad;
        producto.Precio = datos.Precio;
        producto.Stock = datos.Stock;
        producto.Litros = datos.Litros;
        producto.Activo = datos.Activo;
        if (string.IsNullOrWhiteSpace(datos.Descripcion))
        {
            producto.Descripcion = null;
        }
        else
        {
            producto.Descripcion = datos.Descripcion.Trim();
        }
    }
}
