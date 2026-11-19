using Backend.Datos;
using Backend.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Servicios;

public class Mensajeria(AppDbContext db, WhatsApp whatsApp)
{
    public static readonly TimeSpan Ventana = TimeSpan.FromHours(24);

    public async Task<bool> VentanaAbierta(int conversacionId)
    {
        var mensajesDelCliente = db.Mensajes.Where(m => m.ConversacionId == conversacionId && m.Emisor == "Cliente");
        DateTime? ultimo = await mensajesDelCliente.MaxAsync(m => (DateTime?)m.Fecha);
        if (ultimo == null)
        {
            return false;
        }
        return DateTime.UtcNow - ultimo.Value < Ventana;
    }

    public async Task<Conversacion?> UltimaConversacion(int clienteId)
    {
        var conversaciones = db.Conversaciones.Include(c => c.Cliente).Where(c => c.ClienteId == clienteId);
        var ultima = await conversaciones.OrderByDescending(c => c.ConversacionId).FirstOrDefaultAsync();
        return ultima;
    }

    public async Task DerivarAVendedor(int conversacionId)
    {
        var conversacion = await db.Conversaciones.FirstAsync(c => c.ConversacionId == conversacionId);
        conversacion.Estado = "Vendedor";
        await db.SaveChangesAsync();
    }

    public async Task<string?> Enviar(int conversacionId, string texto, string emisor, int? usuarioId = null, Adjunto? imagen = null)
    {
        var conversacion = await db.Conversaciones.Include(c => c.Cliente).FirstAsync(c => c.ConversacionId == conversacionId);

        bool ventanaAbierta = await VentanaAbierta(conversacionId);
        if (!ventanaAbierta)
        {
            return "Pasaron más de 24 horas desde el último mensaje del cliente: WhatsApp no permite escribirle hasta que el cliente envíe un mensaje nuevo.";
        }

        if (!conversacion.Cliente!.EsPrueba)
        {
            bool enviado;
            if (imagen == null)
            {
                enviado = await whatsApp.Enviar(conversacion.Cliente.Telefono, texto);
            }
            else
            {
                enviado = await whatsApp.EnviarImagen(conversacion.Cliente.Telefono, imagen.Datos, imagen.Tipo, texto);
            }
            if (!enviado)
            {
                return "WhatsApp no aceptó el mensaje. Inténtalo de nuevo en unos minutos.";
            }
        }

        var mensaje = new Mensaje { ConversacionId = conversacionId, Emisor = emisor, UsuarioId = usuarioId, Texto = texto };
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();
        if (imagen != null)
        {
            imagen.MensajeId = mensaje.MensajeId;
            db.Adjuntos.Add(imagen);
            await db.SaveChangesAsync();
        }
        return null;
    }

    public async Task<string?> EnviarCotizacion(int conversacionId, Cotizacion cotizacion, string emisor, int? usuarioId = null)
    {
        var conversacion = await db.Conversaciones.Include(c => c.Cliente).FirstAsync(c => c.ConversacionId == conversacionId);

        bool ventanaAbierta = await VentanaAbierta(conversacionId);
        if (!ventanaAbierta)
        {
            return "Pasaron más de 24 horas desde el último mensaje del cliente: WhatsApp no permite escribirle hasta que el cliente envíe un mensaje nuevo.";
        }

        if (cotizacion.Cliente == null)
        {
            cotizacion.Cliente = conversacion.Cliente;
        }

        if (!conversacion.Cliente!.EsPrueba)
        {
            byte[] pdf = CotizacionPdf.Generar(cotizacion);
            string nombreArchivo = CotizacionPdf.NombreArchivo(cotizacion);
            string leyenda = $"Cotización N.º {cotizacion.CotizacionId} de FerreLupe. Total: {Cotizador.Pesos(cotizacion.Total)} (IVA incluido).";

            bool enviado = await whatsApp.EnviarDocumento(conversacion.Cliente.Telefono, pdf, nombreArchivo, leyenda);
            if (!enviado)
            {
                return "WhatsApp no aceptó la cotización. Inténtalo de nuevo en unos minutos.";
            }
        }

        string texto = "Se envió en PDF: " + Cotizador.Texto(cotizacion);
        var mensaje = new Mensaje { ConversacionId = conversacionId, Emisor = emisor, UsuarioId = usuarioId, Texto = texto, CotizacionId = cotizacion.CotizacionId };
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();
        return null;
    }
}
