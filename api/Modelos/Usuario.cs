namespace Backend.Modelos;

public class Usuario
{
    public int UsuarioId { get; set; }
    public int RolId { get; set; }
    public Rol? Rol { get; set; }
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string ClaveHash { get; set; } = "";
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
