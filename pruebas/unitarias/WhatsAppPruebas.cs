using System.Security.Cryptography;
using System.Text;
using Backend.Servicios;

namespace Pruebas;

public class WhatsAppPruebas
{
    const string Secreto = "secreto-de-prueba";
    static readonly byte[] Cuerpo = Encoding.UTF8.GetBytes("{\"object\":\"whatsapp_business_account\"}");

    public static string Firmar(byte[] cuerpo, string secreto)
    {
        byte[] firma = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), cuerpo);
        return "sha256=" + Convert.ToHexStringLower(firma);
    }

    [Fact]
    public void FirmaCorrecta_SeAcepta()
    {
        string firma = Firmar(Cuerpo, Secreto);

        bool valida = WhatsApp.FirmaValida(Cuerpo, firma, Secreto);

        Assert.True(valida);
    }

    [Fact]
    public void FirmaConOtroSecreto_SeRechaza()
    {
        string firma = Firmar(Cuerpo, "otro-secreto");

        bool valida = WhatsApp.FirmaValida(Cuerpo, firma, Secreto);

        Assert.False(valida);
    }

    [Fact]
    public void CuerpoModificado_SeRechaza()
    {
        string firma = Firmar(Cuerpo, Secreto);
        byte[] otroCuerpo = Encoding.UTF8.GetBytes("{\"object\":\"otro\"}");

        bool valida = WhatsApp.FirmaValida(otroCuerpo, firma, Secreto);

        Assert.False(valida);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("md5=abc")]
    [InlineData("sha256=esto-no-es-hexadecimal")]
    public void EncabezadoFaltanteOMalFormado_SeRechaza(string? encabezado)
    {
        bool valida = WhatsApp.FirmaValida(Cuerpo, encabezado, Secreto);

        Assert.False(valida);
    }

    [Fact]
    public void SinSecretoConfigurado_SeRechazaTodo()
    {
        string firma = Firmar(Cuerpo, "");

        bool valida = WhatsApp.FirmaValida(Cuerpo, firma, null);

        Assert.False(valida);
    }
}
