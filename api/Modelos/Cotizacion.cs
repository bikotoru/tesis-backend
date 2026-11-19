namespace Backend.Modelos;

public class Cotizacion
{
    public int CotizacionId { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? ConversacionId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "Enviada";
    public List<DetalleCotizacion> Detalles { get; set; } = [];

    public long Total
    {
        get
        {
            long total = 0;
            foreach (var detalle in Detalles)
            {
                total = total + (long)detalle.Cantidad * detalle.PrecioUnitario;
            }
            return total;
        }
    }
}
