using System.Text;
using System.Threading.RateLimiting;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(opciones =>
{
    opciones.UseSqlServer(config.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure());
});

var claveJwt = config["Jwt:Clave"];
if (string.IsNullOrEmpty(claveJwt) || claveJwt.Length < 32)
{
    throw new InvalidOperationException("Falta la variable Jwt__Clave (mínimo 32 caracteres).");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveJwt)),
        };
        opciones.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contexto =>
            {
                var db = contexto.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                bool valida = await Sesion.SigueValida(db, contexto.Principal!);
                if (!valida)
                {
                    contexto.Fail("La sesión ya no es válida.");
                }
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = 429;
    opciones.OnRejected = async (contexto, cancelacion) =>
    {
        await contexto.HttpContext.Response.WriteAsJsonAsync(new { mensaje = "Demasiados intentos. Espera un minuto." }, cancelacion);
    };
    opciones.AddPolicy("ingreso", contexto =>
    {
        string ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "";
        string reenviada = contexto.Request.Headers["X-Forwarded-For"].ToString();
        if (reenviada != "")
        {
            ip = reenviada.Split(',')[0].Trim();
        }
        var limite = new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 };
        return RateLimitPartition.GetFixedWindowLimiter(ip, clave => limite);
    });
});

builder.Services.Configure<PasswordHasherOptions>(opciones =>
{
    opciones.IterationCount = 220_000;
});
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddControllers(opciones =>
    {
        opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .ConfigureApiBehaviorOptions(opciones =>
    {
        opciones.InvalidModelStateResponseFactory = contexto =>
        {
            return new BadRequestObjectResult(new { mensaje = "Los datos enviados no tienen el formato correcto." });
        };
    });
builder.Services.AddHttpClient<WhatsApp>();
builder.Services.AddHttpClient<Erp>();
builder.Services.AddScoped<Mensajeria>();
builder.Services.AddScoped<Asistente>();
builder.Services.AddSingleton<Sesion>();

var app = builder.Build();

app.UseExceptionHandler(error =>
{
    error.Run(async contexto =>
    {
        contexto.Response.StatusCode = 500;
        await contexto.Response.WriteAsJsonAsync(new { mensaje = "Ocurrió un error en el servidor. Inténtalo de nuevo en unos minutos." });
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapFallback(async contexto =>
{
    contexto.Response.StatusCode = 404;
    await contexto.Response.WriteAsJsonAsync(new { mensaje = "No se encontró la ruta." });
});

app.Run();
