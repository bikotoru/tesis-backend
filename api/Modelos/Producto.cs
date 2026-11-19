namespace Backend.Modelos;

public class Producto
{
    public int ProductoId { get; set; }
    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string Unidad { get; set; } = "";
    public int Precio { get; set; }
    public int Stock { get; set; }
    public decimal? Litros { get; set; }
    public bool Activo { get; set; } = true;
}
