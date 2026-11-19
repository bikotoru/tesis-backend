namespace Backend.Modelos;

public class Configuracion
{
    public int ConfiguracionId { get; set; } = 1;
    public string MensajeBienvenida { get; set; } = "";
    public string Horario { get; set; } = "";
    public decimal RendimientoPintura { get; set; }
    public int MargenMedidas { get; set; }
    public DateTime? UltimaSincronizacion { get; set; }
}
