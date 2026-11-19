using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosIngreso
{
    public string Correo { get; set; } = "";
    public string Clave { get; set; } = "";
}

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<Usuario> hasher, Sesion sesion) : ControllerBase
{
    const string HashDeRelleno = "AQAAAAIAA1tgAAAAELMONJn5LD/rbldi7ak+S+bmYtwVfnL/TiiJM2yqUwygppcb/HqoMwbAgVSkP1SWpw==";

    [HttpPost("ingresar")]
    [EnableRateLimiting("ingreso")]
    public async Task<IActionResult> Ingresar(DatosIngreso datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Correo))
        {
            return BadRequest(new { mensaje = "Escribe tu correo." });
        }
        if (string.IsNullOrEmpty(datos.Clave))
        {
            return BadRequest(new { mensaje = "Escribe tu contraseña." });
        }

        string correo = datos.Correo.Trim();
        var usuario = await db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Correo == correo);
        if (usuario == null)
        {
            hasher.VerifyHashedPassword(new Usuario(), HashDeRelleno, datos.Clave);
            return Unauthorized(new { mensaje = "El correo o la contraseña no son correctos." });
        }

        var resultado = hasher.VerifyHashedPassword(usuario, usuario.ClaveHash, datos.Clave);
        if (resultado == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { mensaje = "El correo o la contraseña no son correctos." });
        }
        if (!usuario.Activo)
        {
            return Unauthorized(new { mensaje = "Tu cuenta está desactivada. Habla con el administrador." });
        }

        string rol = usuario.Rol!.Nombre;
        string token = sesion.CrearToken(usuario.UsuarioId, usuario.Nombre, rol);
        return Ok(new { token, nombre = usuario.Nombre, correo = usuario.Correo, rol });
    }
}
