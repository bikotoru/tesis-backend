using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosImagen
{
    public string Tipo { get; set; } = "";
    public string Datos { get; set; } = "";
}

public class DatosRespuesta
{
    public string Texto { get; set; } = "";
    public DatosImagen? Imagen { get; set; }
}

public class DatosSimulacion
{
    public string? Nombre { get; set; }
    public string Texto { get; set; } = "";
    public DatosImagen? Imagen { get; set; }
}

[ApiController]
[Route("api/conversaciones")]
[Authorize]
public class ConversacionesController(AppDbContext db, Mensajeria mensajeria, IServiceScopeFactory fabrica, ILogger<ConversacionesController> log) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var idsDeUltimosMensajes = db.Mensajes.GroupBy(m => m.ConversacionId).Select(g => g.Max(m => m.MensajeId));
        var ids = await idsDeUltimosMensajes.OrderByDescending(id => id).Take(100).ToListAsync();
        var ultimosMensajes = await db.Mensajes.Where(m => ids.Contains(m.MensajeId)).OrderByDescending(m => m.MensajeId).ToListAsync();

        var idsDeConversaciones = new List<int>();
        foreach (var mensaje in ultimosMensajes)
        {
            idsDeConversaciones.Add(mensaje.ConversacionId);
        }

        var conversaciones = await db.Conversaciones
            .Include(c => c.Cliente)
            .Include(c => c.Usuario)
            .Where(c => idsDeConversaciones.Contains(c.ConversacionId))
            .ToDictionaryAsync(c => c.ConversacionId);

        var mensajesDelCliente = db.Mensajes.Where(m => idsDeConversaciones.Contains(m.ConversacionId) && m.Emisor == "Cliente");
        var fechasDelCliente = await mensajesDelCliente
            .GroupBy(m => m.ConversacionId)
            .Select(g => new { ConversacionId = g.Key, Fecha = g.Max(m => m.Fecha) })
            .ToDictionaryAsync(f => f.ConversacionId, f => f.Fecha);

        var lista = new List<object>();
        foreach (var mensaje in ultimosMensajes)
        {
            var c = conversaciones[mensaje.ConversacionId];
            bool ventanaAbierta = false;
            if (fechasDelCliente.ContainsKey(c.ConversacionId))
            {
                ventanaAbierta = DateTime.UtcNow - fechasDelCliente[c.ConversacionId] < Mensajeria.Ventana;
            }

            string? vendedor = null;
            if (c.Usuario != null)
            {
                vendedor = c.Usuario.Nombre;
            }

            lista.Add(new
            {
                id = c.ConversacionId,
                cliente = c.Cliente!.Nombre,
                telefono = c.Cliente.Telefono,
                prueba = c.Cliente.EsPrueba,
                estado = c.Estado,
                vendedor,
                ultimo = new { mensaje.Texto, mensaje.Fecha, mensaje.Emisor },
                ventanaAbierta,
            });
        }
        return Ok(lista);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var c = await db.Conversaciones.Include(c => c.Cliente).Include(c => c.Usuario).FirstOrDefaultAsync(c => c.ConversacionId == id);
        if (c == null)
        {
            return NotFound(new { mensaje = "No se encontró la conversación." });
        }

        var mensajesDeLaConversacion = db.Mensajes.Where(m => m.ConversacionId == id).OrderBy(m => m.MensajeId);
        var lista = await mensajesDeLaConversacion.ToListAsync();
        var idsConAdjunto = await db.Adjuntos.Where(a => lista.Select(m => m.MensajeId).Contains(a.MensajeId)).Select(a => a.MensajeId).ToListAsync();

        var mensajes = new List<object>();
        foreach (var m in lista)
        {
            bool tieneImagen = m.IdImagen != null || idsConAdjunto.Contains(m.MensajeId);
            mensajes.Add(new { id = m.MensajeId, m.Emisor, m.Texto, m.Fecha, cotizacionId = m.CotizacionId, tieneImagen });
        }
        bool ventanaAbierta = await mensajeria.VentanaAbierta(id);

        return Ok(new
        {
            id = c.ConversacionId,
            clienteId = c.ClienteId,
            cliente = c.Cliente!.Nombre,
            telefono = c.Cliente.Telefono,
            prueba = c.Cliente.EsPrueba,
            estado = c.Estado,
            vendedor = c.Usuario?.Nombre,
            ventanaAbierta,
            mensajes,
        });
    }

    [HttpPost("{id}/tomar")]
    public async Task<IActionResult> Tomar(int id)
    {
        var c = await db.Conversaciones.FirstOrDefaultAsync(c => c.ConversacionId == id);
        if (c == null)
        {
            return NotFound(new { mensaje = "No se encontró la conversación." });
        }
        c.Estado = "Vendedor";
        c.UsuarioId = Sesion.UsuarioId(User);
        await db.SaveChangesAsync();
        return Ok(new { mensaje = "Tomaste la conversación. El asistente deja de responder." });
    }

    [HttpPost("{id}/devolver")]
    public async Task<IActionResult> Devolver(int id)
    {
        var c = await db.Conversaciones.FirstOrDefaultAsync(c => c.ConversacionId == id);
        if (c == null)
        {
            return NotFound(new { mensaje = "No se encontró la conversación." });
        }
        c.Estado = "Asistente";
        c.UsuarioId = null;
        await db.SaveChangesAsync();
        return Ok(new { mensaje = "El asistente vuelve a atender esta conversación." });
    }

    [HttpPost("{id}/responder")]
    public async Task<IActionResult> Responder(int id, DatosRespuesta datos)
    {
        string? error = RevisarMensaje(datos.Texto, datos.Imagen);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }
        string texto = TextoDelMensaje(datos.Texto, datos.Imagen);

        var c = await db.Conversaciones.FirstOrDefaultAsync(c => c.ConversacionId == id);
        if (c == null)
        {
            return NotFound(new { mensaje = "No se encontró la conversación." });
        }
        if (c.Estado != "Vendedor")
        {
            return BadRequest(new { mensaje = "Toma la conversación antes de responder, para que el asistente no conteste al mismo tiempo." });
        }

        string? aviso = await mensajeria.Enviar(id, texto, "Vendedor", Sesion.UsuarioId(User), CrearAdjunto(datos.Imagen));
        if (aviso != null)
        {
            return BadRequest(new { mensaje = aviso });
        }
        return Ok(new { mensaje = "Mensaje enviado por WhatsApp." });
    }

    [HttpPost("simular")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Simular(DatosSimulacion datos)
    {
        string? error = RevisarMensaje(datos.Texto, datos.Imagen);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        string nombre = "Cliente de prueba";
        if (!string.IsNullOrWhiteSpace(datos.Nombre))
        {
            nombre = datos.Nombre.Trim();
        }
        if (nombre.Length > 100)
        {
            return BadRequest(new { mensaje = "El nombre admite hasta 100 caracteres." });
        }

        var cliente = new Cliente();
        cliente.Telefono = "PRUEBA-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        cliente.Nombre = nombre;
        cliente.EsPrueba = true;

        var conversacion = new Conversacion { Cliente = cliente, Estado = "Asistente" };
        db.Conversaciones.Add(conversacion);
        await db.SaveChangesAsync();

        await GuardarMensajeDePrueba(conversacion.ConversacionId, TextoDelMensaje(datos.Texto, datos.Imagen), CrearAdjunto(datos.Imagen));
        return Ok(new { id = conversacion.ConversacionId, mensaje = "Conversación de prueba creada. El asistente responderá en unos segundos." });
    }

    [HttpPost("{id}/simular")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SimularMensaje(int id, DatosRespuesta datos)
    {
        string? error = RevisarMensaje(datos.Texto, datos.Imagen);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        var c = await db.Conversaciones.Include(c => c.Cliente).FirstOrDefaultAsync(c => c.ConversacionId == id);
        if (c == null)
        {
            return NotFound(new { mensaje = "No se encontró la conversación." });
        }
        if (!c.Cliente!.EsPrueba)
        {
            return BadRequest(new { mensaje = "Solo se puede escribir como cliente en una conversación de prueba." });
        }

        await GuardarMensajeDePrueba(id, TextoDelMensaje(datos.Texto, datos.Imagen), CrearAdjunto(datos.Imagen));
        return Ok(new { mensaje = "Mensaje enviado como cliente." });
    }

    async Task GuardarMensajeDePrueba(int conversacionId, string texto, Adjunto? imagen)
    {
        var mensaje = new Mensaje { ConversacionId = conversacionId, Emisor = "Cliente", Texto = texto };
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();
        if (imagen != null)
        {
            imagen.MensajeId = mensaje.MensajeId;
            db.Adjuntos.Add(imagen);
            await db.SaveChangesAsync();
        }
        Asistente.ResponderAparte(fabrica, log, conversacionId, mensaje.MensajeId);
    }

    [HttpGet("{id}/mensajes/{mensajeId}/imagen")]
    public async Task<IActionResult> Imagen(int id, int mensajeId, [FromServices] WhatsApp whatsApp)
    {
        var mensaje = await db.Mensajes.FirstOrDefaultAsync(m => m.MensajeId == mensajeId && m.ConversacionId == id);
        if (mensaje == null)
        {
            return NotFound(new { mensaje = "No se encontró el mensaje." });
        }

        var adjunto = await db.Adjuntos.FirstOrDefaultAsync(a => a.MensajeId == mensajeId);
        if (adjunto != null)
        {
            return File(adjunto.Datos, adjunto.Tipo, NombreDeImagen(mensajeId, adjunto.Tipo));
        }

        if (mensaje.IdImagen != null)
        {
            var descargada = await whatsApp.DescargarImagen(mensaje.IdImagen);
            if (descargada != null)
            {
                return File(descargada.Datos, descargada.Tipo, NombreDeImagen(mensajeId, descargada.Tipo));
            }
        }
        return NotFound(new { mensaje = "La imagen ya no está disponible." });
    }

    static string NombreDeImagen(int mensajeId, string tipo)
    {
        string extension = ".jpg";
        if (tipo == "image/png")
        {
            extension = ".png";
        }
        return "imagen-" + mensajeId + extension;
    }

    static string? RevisarMensaje(string? texto, DatosImagen? imagen)
    {
        if (imagen == null && string.IsNullOrWhiteSpace(texto))
        {
            return "Escribe un mensaje o adjunta una imagen.";
        }
        int largo = 0;
        if (texto != null)
        {
            largo = texto.Trim().Length;
        }
        if (largo > 4096)
        {
            return "WhatsApp admite hasta 4.096 caracteres.";
        }
        if (imagen == null)
        {
            return null;
        }
        if (largo > 1024)
        {
            return "El texto que acompaña una imagen admite hasta 1.024 caracteres.";
        }

        if (imagen.Tipo != "image/jpeg" && imagen.Tipo != "image/png")
        {
            return "La imagen debe ser JPG o PNG.";
        }
        byte[] datos;
        try
        {
            datos = Convert.FromBase64String(imagen.Datos);
        }
        catch (FormatException)
        {
            return "La imagen no tiene un formato válido.";
        }
        if (datos.Length == 0)
        {
            return "La imagen está vacía.";
        }
        if (datos.Length > TamanoMaximoImagen)
        {
            return "La imagen puede pesar hasta 3,5 MB.";
        }
        return null;
    }

    const int TamanoMaximoImagen = 3_500_000;

    static string TextoDelMensaje(string? texto, DatosImagen? imagen)
    {
        string limpio = "";
        if (texto != null)
        {
            limpio = texto.Trim();
        }
        if (imagen == null)
        {
            return limpio;
        }
        if (limpio == "")
        {
            return "[Imagen]";
        }
        return "[Imagen] " + limpio;
    }

    static Adjunto? CrearAdjunto(DatosImagen? imagen)
    {
        if (imagen == null)
        {
            return null;
        }
        var adjunto = new Adjunto();
        adjunto.Tipo = imagen.Tipo;
        adjunto.Datos = Convert.FromBase64String(imagen.Datos);
        return adjunto;
    }
}
