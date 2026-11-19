using System.Text.Json;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class MensajeGuardado
{
    public int ConversacionId { get; set; }
    public int MensajeId { get; set; }
}

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController(AppDbContext db, IConfiguration configuracion, IServiceScopeFactory fabrica, ILogger<WhatsAppController> log) : ControllerBase
{
    [HttpGet]
    public IActionResult Verificar(
        [FromQuery(Name = "hub.mode")] string? modo,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? desafio)
    {
        string? esperado = configuracion["WhatsApp:TokenVerificacion"];
        if (modo == "subscribe" && !string.IsNullOrEmpty(esperado) && token == esperado)
        {
            return Content(desafio ?? "", "text/plain");
        }
        return StatusCode(403);
    }

    [HttpPost]
    public async Task<IActionResult> Recibir()
    {
        using var memoria = new MemoryStream();
        await Request.Body.CopyToAsync(memoria);
        byte[] cuerpo = memoria.ToArray();

        string? firma = Request.Headers["X-Hub-Signature-256"];
        if (!WhatsApp.FirmaValida(cuerpo, firma, configuracion["WhatsApp:SecretoApp"]))
        {
            return Unauthorized();
        }

        using var json = JsonDocument.Parse(cuerpo);
        foreach (var entrada in Lista(json.RootElement, "entry"))
        {
            foreach (var cambio in Lista(entrada, "changes"))
            {
                var valor = cambio.GetProperty("value");
                foreach (var mensaje in Lista(valor, "messages"))
                {
                    string telefono = mensaje.GetProperty("from").GetString()!;
                    string? nombre = NombreDelPerfil(valor, telefono);
                    var guardado = await Guardar(mensaje, nombre);
                    if (guardado != null)
                    {
                        Asistente.ResponderAparte(fabrica, log, guardado.ConversacionId, guardado.MensajeId);
                    }
                }
            }
        }
        return Ok();
    }

    async Task<MensajeGuardado?> Guardar(JsonElement mensaje, string? nombre)
    {
        string idWhatsApp = mensaje.GetProperty("id").GetString()!;
        bool repetido = await db.Mensajes.AnyAsync(m => m.IdWhatsApp == idWhatsApp);
        if (repetido)
        {
            return null;
        }

        string telefono = mensaje.GetProperty("from").GetString()!;
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono);
        if (cliente == null)
        {
            cliente = new Cliente { Telefono = telefono, Nombre = nombre };
            db.Clientes.Add(cliente);
        }

        var conversacion = await ConversacionAbierta(cliente.ClienteId);
        if (conversacion == null)
        {
            conversacion = new Conversacion { Cliente = cliente, Estado = "Asistente" };
            db.Conversaciones.Add(conversacion);
        }

        var nuevo = new Mensaje
        {
            IdWhatsApp = idWhatsApp,
            Emisor = "Cliente",
            Texto = TextoDelMensaje(mensaje),
            IdImagen = IdImagenDelMensaje(mensaje),
            Fecha = FechaDelMensaje(mensaje),
        };
        conversacion.Mensajes.Add(nuevo);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            bool yaEstaba = await db.Mensajes.AnyAsync(m => m.IdWhatsApp == idWhatsApp);
            if (!yaEstaba)
            {
                throw;
            }
            return null;
        }

        var guardado = new MensajeGuardado { ConversacionId = conversacion.ConversacionId, MensajeId = nuevo.MensajeId };
        return guardado;
    }

    async Task<Conversacion?> ConversacionAbierta(int clienteId)
    {
        if (clienteId == 0)
        {
            return null;
        }

        var delCliente = db.Conversaciones.Where(c => c.ClienteId == clienteId);
        var ultima = await delCliente.OrderByDescending(c => c.ConversacionId).FirstOrDefaultAsync();
        if (ultima == null)
        {
            return null;
        }

        var mensajesDelCliente = db.Mensajes.Where(m => m.ConversacionId == ultima.ConversacionId && m.Emisor == "Cliente");
        DateTime? ultimoMensaje = await mensajesDelCliente.MaxAsync(m => (DateTime?)m.Fecha);
        if (ultimoMensaje == null)
        {
            return null;
        }
        if (DateTime.UtcNow - ultimoMensaje.Value >= Mensajeria.Ventana)
        {
            return null;
        }
        return ultima;
    }

    static string TextoDelMensaje(JsonElement mensaje)
    {
        string tipo = mensaje.GetProperty("type").GetString() ?? "";
        if (tipo == "text")
        {
            string texto = mensaje.GetProperty("text").GetProperty("body").GetString() ?? "";
            return texto;
        }
        if (tipo == "image")
        {
            string texto = "[Imagen]";
            if (mensaje.GetProperty("image").TryGetProperty("caption", out JsonElement leyenda))
            {
                texto = texto + " " + leyenda.GetString();
            }
            return texto;
        }
        return $"[El cliente envió un mensaje de tipo «{tipo}», que el sistema no puede leer]";
    }

    static string? IdImagenDelMensaje(JsonElement mensaje)
    {
        string tipo = mensaje.GetProperty("type").GetString() ?? "";
        if (tipo != "image")
        {
            return null;
        }
        return mensaje.GetProperty("image").GetProperty("id").GetString();
    }

    static DateTime FechaDelMensaje(JsonElement mensaje)
    {
        if (mensaje.TryGetProperty("timestamp", out JsonElement marca))
        {
            if (long.TryParse(marca.ToString(), out long segundos))
            {
                return DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime;
            }
        }
        return DateTime.UtcNow;
    }

    static string? NombreDelPerfil(JsonElement valor, string telefono)
    {
        foreach (var contacto in Lista(valor, "contacts"))
        {
            if (contacto.TryGetProperty("wa_id", out JsonElement id) && id.GetString() == telefono)
            {
                return contacto.GetProperty("profile").GetProperty("name").GetString();
            }
        }
        return null;
    }

    static List<JsonElement> Lista(JsonElement elemento, string campo)
    {
        var lista = new List<JsonElement>();
        if (elemento.TryGetProperty(campo, out JsonElement arreglo) && arreglo.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arreglo.EnumerateArray())
            {
                lista.Add(item);
            }
        }
        return lista;
    }
}
