namespace Backend.Modelos;

public class Adjunto
{
    public int AdjuntoId { get; set; }
    public int MensajeId { get; set; }
    public string Tipo { get; set; } = "";
    public byte[] Datos { get; set; } = [];
}
