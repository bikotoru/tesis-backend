using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosCotizacion
{
    public int ClienteId { get; set; }
    public List<Linea>? Lineas { get; set; }
}

public class DatosEstado
{
    public string Estado { get; set; } = "";
}

[ApiController]
[Route("api/cotizaciones")]
[Authorize]
public class CotizacionesController(AppDbContext db, Mensajeria mensajeria) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var ultimas = db.Cotizaciones.OrderByDescending(c => c.CotizacionId).Take(200);
        var lista = await ultimas.Select(c => new
        {
            id = c.CotizacionId,
            c.Fecha,
            cliente = c.Cliente!.Nombre,
            telefono = c.Cliente.Telefono,
            total = c.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
            c.Estado,
        }).ToListAsync();
        return Ok(lista);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var c = await Buscar(id);
        if (c == null)
        {
            return NotFound(new { mensaje = "No se encontró la cotización." });
        }

        var lineas = new List<object>();
        foreach (var d in c.Detalles)
        {
            lineas.Add(new { d.ProductoId, nombre = d.Producto!.Nombre, unidad = d.Producto.Unidad, precio = d.PrecioUnitario, d.Cantidad });
        }

        return Ok(new
        {
            id = c.CotizacionId,
            c.Fecha,
            c.ClienteId,
            cliente = c.Cliente!.Nombre,
            telefono = c.Cliente.Telefono,
            c.Estado,
            lineas,
            total = c.Total,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Crear(DatosCotizacion datos)
    {
        if (datos.Lineas == null || datos.Lineas.Count == 0)
        {
            return BadRequest(new { mensaje = "Agrega al menos un producto." });
        }
        bool clienteExiste = await db.Clientes.AnyAsync(c => c.ClienteId == datos.ClienteId);
        if (!clienteExiste)
        {
            return BadRequest(new { mensaje = "El cliente no existe." });
        }

        var armados = await Cotizador.ArmarDetalles(db, datos.Lineas);
        if (armados.Error != null)
        {
            return BadRequest(new { mensaje = armados.Error });
        }

        var conversacion = await mensajeria.UltimaConversacion(datos.ClienteId);
        var cotizacion = new Cotizacion { ClienteId = datos.ClienteId, Detalles = armados.Detalles };
        if (conversacion != null)
        {
            cotizacion.ConversacionId = conversacion.ConversacionId;
        }
        db.Cotizaciones.Add(cotizacion);
        await db.SaveChangesAsync();

        string? aviso = await EnviarAlCliente(cotizacion);
        return Ok(new { id = cotizacion.CotizacionId, aviso });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Ajustar(int id, DatosCotizacion datos)
    {
        if (datos.Lineas == null || datos.Lineas.Count == 0)
        {
            return BadRequest(new { mensaje = "Agrega al menos un producto." });
        }
        var cotizacion = await Buscar(id);
        if (cotizacion == null)
        {
            return NotFound(new { mensaje = "No se encontró la cotización." });
        }

        var armados = await Cotizador.ArmarDetalles(db, datos.Lineas);
        if (armados.Error != null)
        {
            return BadRequest(new { mensaje = armados.Error });
        }

        db.DetallesCotizacion.RemoveRange(cotizacion.Detalles);
        cotizacion.Detalles = armados.Detalles;
        cotizacion.Estado = "Enviada";
        await db.SaveChangesAsync();

        string? aviso = await EnviarAlCliente(cotizacion);
        return Ok(new { id, aviso });
    }

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, DatosEstado datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Estado))
        {
            return BadRequest(new { mensaje = "Elige un estado." });
        }
        if (datos.Estado != "Enviada" && datos.Estado != "Aceptada" && datos.Estado != "Rechazada")
        {
            return BadRequest(new { mensaje = "El estado no es válido." });
        }

        var cotizacion = await db.Cotizaciones.FirstOrDefaultAsync(c => c.CotizacionId == id);
        if (cotizacion == null)
        {
            return NotFound(new { mensaje = "No se encontró la cotización." });
        }
        cotizacion.Estado = datos.Estado;
        await db.SaveChangesAsync();
        return Ok(new { mensaje = "El estado se guardó." });
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var cotizacion = await Buscar(id);
        if (cotizacion == null)
        {
            return NotFound(new { mensaje = "No se encontró la cotización." });
        }
        byte[] pdf = CotizacionPdf.Generar(cotizacion);
        return File(pdf, "application/pdf", CotizacionPdf.NombreArchivo(cotizacion));
    }

    [HttpPost("{id}/reenviar")]
    public async Task<IActionResult> Reenviar(int id)
    {
        var cotizacion = await Buscar(id);
        if (cotizacion == null)
        {
            return NotFound(new { mensaje = "No se encontró la cotización." });
        }

        string? aviso = await EnviarAlCliente(cotizacion);
        if (aviso != null)
        {
            return BadRequest(new { mensaje = aviso });
        }
        return Ok(new { mensaje = "Cotización reenviada por WhatsApp." });
    }

    async Task<Cotizacion?> Buscar(int id)
    {
        var consulta = db.Cotizaciones.Include(c => c.Cliente).Include(c => c.Detalles).ThenInclude(d => d.Producto);
        var cotizacion = await consulta.FirstOrDefaultAsync(c => c.CotizacionId == id);
        return cotizacion;
    }

    async Task<string?> EnviarAlCliente(Cotizacion cotizacion)
    {
        var conversacion = await mensajeria.UltimaConversacion(cotizacion.ClienteId);
        if (conversacion == null)
        {
            return "El cliente todavía no ha escrito por WhatsApp: la cotización quedó guardada, pero no se envió.";
        }

        string? aviso = await mensajeria.EnviarCotizacion(conversacion.ConversacionId, cotizacion, "Vendedor", Sesion.UsuarioId(User));
        if (aviso == null)
        {
            return null;
        }
        return aviso + " La cotización quedó guardada.";
    }
}
