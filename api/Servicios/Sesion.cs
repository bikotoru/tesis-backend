using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend.Datos;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Servicios;

public class Sesion(IConfiguration configuracion)
{
    public static readonly TimeSpan Duracion = TimeSpan.FromHours(8);

    public string CrearToken(int usuarioId, string nombre, string rol)
    {
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuracion["Jwt:Clave"]!));
        var datos = new List<Claim>();
        datos.Add(new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()));
        datos.Add(new Claim(ClaimTypes.Name, nombre));
        datos.Add(new Claim(ClaimTypes.Role, rol));

        var token = new JwtSecurityToken(
            claims: datos,
            expires: DateTime.UtcNow.Add(Duracion),
            signingCredentials: new SigningCredentials(clave, SecurityAlgorithms.HmacSha256));
        string texto = new JwtSecurityTokenHandler().WriteToken(token);
        return texto;
    }

    public static int UsuarioId(ClaimsPrincipal usuario)
    {
        string id = usuario.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return int.Parse(id);
    }

    public static async Task<bool> SigueValida(AppDbContext db, ClaimsPrincipal sesion)
    {
        string? id = sesion.FindFirstValue(ClaimTypes.NameIdentifier);
        string? rolDelToken = sesion.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(id, out int usuarioId) || rolDelToken == null)
        {
            return false;
        }

        var usuario = await db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.UsuarioId == usuarioId);
        if (usuario == null || !usuario.Activo || usuario.Rol == null)
        {
            return false;
        }
        return usuario.Rol.Nombre == rolDelToken;
    }
}
