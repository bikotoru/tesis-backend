namespace Backend.Servicios;

public class ResultadoPintura
{
    public double Area { get; set; }
    public int AreaConMargen { get; set; }
    public int Litros { get; set; }
}

public class ResultadoElectricidad
{
    public double Recorrido { get; set; }
    public int RecorridoConMargen { get; set; }
    public int CablePorColor { get; set; }
    public int TirasTuberia { get; set; }
    public int Enchufes { get; set; }
    public int CajasEnchufe { get; set; }
    public int CajasDerivacion { get; set; }
    public int Automaticos { get; set; }
    public int Diferenciales { get; set; }
    public int DiametroTubo { get; set; }
}

public class Envase
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = "";
    public double Litros { get; set; }
    public int Precio { get; set; }
    public int Stock { get; set; }
}

public class EnvaseElegido
{
    public Envase Envase { get; set; } = new Envase();
    public int Cantidad { get; set; }
}

public class CombinacionEnvases
{
    public List<EnvaseElegido> Elegidos { get; set; } = [];
    public double Litros { get; set; }
    public int Precio { get; set; }
}

public class ResultadoTubo
{
    public bool Caben { get; set; }
    public int DiametroMinimo { get; set; }
    public int MaximoEnElTubo { get; set; }
    public int TubosNecesarios { get; set; }
    public int DiametroDeLosTubos { get; set; }
}

public static class Calculos
{
    const double AlturaEnchufe = 0.30;
    const double LargoTiraTuberia = 3;
    public const int MaximoConductoresTabla = 5;

    static readonly Dictionary<double, int[]> DuctoSegunConductores = new Dictionary<double, int[]>
    {
        [1.5] = [16, 16, 16, 20, 20],
        [2.5] = [16, 16, 20, 20, 20],
        [4] = [16, 16, 20, 20, 25],
    };

    public static string? ValidarHabitacion(double largo, double ancho, double alto)
    {
        if (largo < 0.5 || largo > 30 || ancho < 0.5 || ancho > 30)
        {
            return "El largo y el ancho deben estar entre 0,5 y 30 metros.";
        }
        if (alto < 1.5 || alto > 6)
        {
            return "El alto debe estar entre 1,5 y 6 metros.";
        }
        return null;
    }

    public static ResultadoPintura Pintura(double largo, double ancho, double alto, int manos, bool cielo, double rendimiento, int margen)
    {
        double area = 2 * (largo + ancho) * alto;
        if (cielo)
        {
            area = area + largo * ancho;
        }

        int areaConMargen = ArribaConMargen(area, margen);
        int litros = Arriba(areaConMargen * manos / rendimiento);

        var resultado = new ResultadoPintura();
        resultado.Area = Math.Round(area, 2);
        resultado.AreaConMargen = areaConMargen;
        resultado.Litros = litros;
        return resultado;
    }

    public static ResultadoElectricidad Electricidad(double largo, double ancho, double alto, int enchufes, double distanciaTablero, int margen)
    {
        double perimetro = 2 * (largo + ancho);
        double bajadas = enchufes * (alto - AlturaEnchufe);
        double recorrido = perimetro + bajadas + distanciaTablero;
        int recorridoConMargen = ArribaConMargen(recorrido, margen);
        int tiras = Arriba(recorridoConMargen / LargoTiraTuberia);

        var resultado = new ResultadoElectricidad();
        resultado.Recorrido = Math.Round(recorrido, 2);
        resultado.RecorridoConMargen = recorridoConMargen;
        resultado.CablePorColor = recorridoConMargen;
        resultado.TirasTuberia = tiras;
        resultado.Enchufes = enchufes;
        resultado.CajasEnchufe = enchufes;
        resultado.CajasDerivacion = enchufes;
        resultado.Automaticos = 1;
        resultado.Diferenciales = 1;
        resultado.DiametroTubo = DuctoSegunConductores[2.5][2];
        return resultado;
    }

    public static CombinacionEnvases? EnvasesMasBaratos(double litros, List<Envase> envases)
    {
        var ordenados = envases.OrderByDescending(e => e.Litros).ToList();
        var combinacion = Combinar(litros, ordenados, 0);
        return combinacion;
    }

    static CombinacionEnvases? Combinar(double faltan, List<Envase> envases, int indice)
    {
        if (Math.Round(faltan, 6) <= 0)
        {
            return new CombinacionEnvases();
        }
        if (indice >= envases.Count)
        {
            return null;
        }

        var envase = envases[indice];
        int maximo = Arriba(faltan / envase.Litros);
        if (maximo > envase.Stock)
        {
            maximo = envase.Stock;
        }

        CombinacionEnvases? mejor = null;
        for (int cantidad = maximo; cantidad >= 0; cantidad--)
        {
            var resto = Combinar(faltan - cantidad * envase.Litros, envases, indice + 1);
            if (resto == null)
            {
                continue;
            }

            int precio = resto.Precio + cantidad * envase.Precio;
            if (mejor == null || precio < mejor.Precio)
            {
                mejor = new CombinacionEnvases();
                if (cantidad > 0)
                {
                    mejor.Elegidos.Add(new EnvaseElegido { Envase = envase, Cantidad = cantidad });
                }
                mejor.Elegidos.AddRange(resto.Elegidos);
                mejor.Litros = Math.Round(resto.Litros + cantidad * envase.Litros, 2);
                mejor.Precio = precio;
            }
        }
        return mejor;
    }

    public static bool SeccionConocida(double seccion)
    {
        return DuctoSegunConductores.ContainsKey(seccion);
    }

    public static ResultadoTubo CablesEnTubo(double seccion, int cables, int diametroTubo)
    {
        int[] ductos = DuctoSegunConductores[seccion];
        var resultado = new ResultadoTubo();

        if (cables <= MaximoConductoresTabla)
        {
            resultado.DiametroMinimo = ductos[cables - 1];
        }

        if (diametroTubo > 0)
        {
            for (int conductores = 1; conductores <= MaximoConductoresTabla; conductores++)
            {
                if (ductos[conductores - 1] <= diametroTubo)
                {
                    resultado.MaximoEnElTubo = conductores;
                }
            }
        }

        if (cables <= MaximoConductoresTabla)
        {
            resultado.Caben = diametroTubo == 0 || resultado.DiametroMinimo <= diametroTubo;
        }

        if (resultado.Caben)
        {
            resultado.TubosNecesarios = 1;
            resultado.DiametroDeLosTubos = resultado.DiametroMinimo;
            if (diametroTubo > 0)
            {
                resultado.DiametroDeLosTubos = diametroTubo;
            }
        }
        else if (resultado.MaximoEnElTubo > 0)
        {
            resultado.TubosNecesarios = Arriba((double)cables / resultado.MaximoEnElTubo);
            resultado.DiametroDeLosTubos = diametroTubo;
        }
        else if (cables <= MaximoConductoresTabla)
        {
            resultado.TubosNecesarios = 1;
            resultado.DiametroDeLosTubos = resultado.DiametroMinimo;
        }
        else
        {
            resultado.TubosNecesarios = Arriba((double)cables / MaximoConductoresTabla);
            resultado.DiametroDeLosTubos = ductos[MaximoConductoresTabla - 1];
        }
        return resultado;
    }

    static int ArribaConMargen(double medida, int margen)
    {
        double conMargen = medida * (100 + margen) / 100;
        return Arriba(conMargen);
    }

    static int Arriba(double valor)
    {
        double sinErrorDeRedondeo = Math.Round(valor, 6);
        return (int)Math.Ceiling(sinErrorDeRedondeo);
    }
}
