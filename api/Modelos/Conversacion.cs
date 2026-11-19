namespace Backend.Modelos;

public class Conversacion
{
    public int ConversacionId { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Estado { get; set; } = "Asistente";
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public List<Mensaje> Mensajes { get; set; } = [];
}
