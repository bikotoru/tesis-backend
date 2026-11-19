namespace Backend.Modelos;

public class DetalleCotizacion
{
    public int CotizacionId { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int Cantidad { get; set; }
    public int PrecioUnitario { get; set; }
}
