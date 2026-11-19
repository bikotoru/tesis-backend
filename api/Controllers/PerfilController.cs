using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosPerfil
{
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
}

public class CambioClave
{
    public string Actual { get; set; } = "";
    public string Nueva { get; set; } = "";
}

[ApiController]
[Route("api/perfil")]
[Authorize]
public class PerfilController(AppDbContext db, IPasswordHasher<Usuario> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        int id = Sesion.UsuarioId(User);
        var usuario = await db.Usuarios.Include(u => u.Rol).FirstAsync(u => u.UsuarioId == id);
        return Ok(new { usuario.Nombre, usuario.Correo, rol = usuario.Rol!.Nombre });
    }

    [HttpPut]
    public async Task<IActionResult> Guardar(DatosPerfil datos)
    {
        string? error = Validar(datos);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        int id = Sesion.UsuarioId(User);
        string correo = datos.Correo.Trim();
        bool correoOcupado = await db.Usuarios.AnyAsync(u => u.Correo == correo && u.UsuarioId != id);
        if (correoOcupado)
        {
            return BadRequest(new { mensaje = "Ese correo ya lo usa otra cuenta." });
        }

        var usuario = await db.Usuarios.FirstAsync(u => u.UsuarioId == id);
        usuario.Nombre = datos.Nombre.Trim();
        usuario.Correo = correo;
        await db.SaveChangesAsync();
        return Ok(new { mensaje = "Tus datos se guardaron." });
    }

    [HttpPut("clave")]
    public async Task<IActionResult> CambiarClave(CambioClave datos)
    {
        if (string.IsNullOrEmpty(datos.Actual))
        {
            return BadRequest(new { mensaje = "Escribe tu contraseña actual." });
        }
        if (string.IsNullOrEmpty(datos.Nueva))
        {
            return BadRequest(new { mensaje = "Escribe la contraseña nueva." });
        }
        if (datos.Nueva.Length < 8)
        {
            return BadRequest(new { mensaje = "La contraseña nueva debe tener al menos 8 caracteres." });
        }

        int id = Sesion.UsuarioId(User);
        var usuario = await db.Usuarios.FirstAsync(u => u.UsuarioId == id);
        var resultado = hasher.VerifyHashedPassword(usuario, usuario.ClaveHash, datos.Actual);
        if (resultado == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { mensaje = "La contraseña actual no es correcta." });
        }

        usuario.ClaveHash = hasher.HashPassword(usuario, datos.Nueva);
        await db.SaveChangesAsync();
        return Ok(new { mensaje = "Tu contraseña se cambió." });
    }

    static string? Validar(DatosPerfil datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return "Escribe tu nombre.";
        }
        if (datos.Nombre.Trim().Length > 100)
        {
            return "El nombre admite hasta 100 caracteres.";
        }
        if (string.IsNullOrWhiteSpace(datos.Correo))
        {
            return "Escribe tu correo.";
        }
        string correo = datos.Correo.Trim();
        if (!UsuariosController.CorreoValido(correo))
        {
            return "El correo no es válido.";
        }
        if (correo.Length > 150)
        {
            return "El correo admite hasta 150 caracteres.";
        }
        return null;
    }
}
