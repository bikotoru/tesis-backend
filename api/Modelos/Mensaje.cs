namespace Backend.Modelos;

public class Mensaje
{
    public int MensajeId { get; set; }
    public int ConversacionId { get; set; }
    public string Emisor { get; set; } = "";
    public int? UsuarioId { get; set; }
    public string? IdWhatsApp { get; set; }
    public string? IdImagen { get; set; }
    public int? CotizacionId { get; set; }
    public string Texto { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
