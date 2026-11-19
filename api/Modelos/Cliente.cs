namespace Backend.Modelos;

public class Cliente
{
    public int ClienteId { get; set; }
    public string Telefono { get; set; } = "";
    public string? Nombre { get; set; }
    public bool EsPrueba { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
