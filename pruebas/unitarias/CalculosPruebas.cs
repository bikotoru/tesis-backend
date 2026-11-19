using Backend.Servicios;

namespace Pruebas;

public class CalculosPruebas
{
    [Fact]
    public void Pintura_EjemploDelInforme()
    {
        var r = Calculos.Pintura(5, 2, 2.4, manos: 2, cielo: false, rendimiento: 10, margen: 10);

        Assert.Equal(33.6, r.Area);
        Assert.Equal(37, r.AreaConMargen);
        Assert.Equal(8, r.Litros);
    }

    [Fact]
    public void Pintura_ConCielo()
    {
        var r = Calculos.Pintura(3, 4, 2.4, manos: 2, cielo: true, rendimiento: 10, margen: 10);

        Assert.Equal(45.6, r.Area);
        Assert.Equal(51, r.AreaConMargen);
        Assert.Equal(11, r.Litros);
    }

    [Fact]
    public void Pintura_SinMargenYMedidaExacta_NoSumaDeMas()
    {
        var r = Calculos.Pintura(4, 4, 2.5, manos: 2, cielo: false, rendimiento: 10, margen: 0);

        Assert.Equal(40, r.AreaConMargen);
        Assert.Equal(8, r.Litros);
    }

    [Fact]
    public void Electricidad_EjemploDelInforme()
    {
        var r = Calculos.Electricidad(5, 2, 2.4, enchufes: 10, distanciaTablero: 5, margen: 10);

        Assert.Equal(40, r.Recorrido);
        Assert.Equal(44, r.RecorridoConMargen);
        Assert.Equal(44, r.CablePorColor);
        Assert.Equal(15, r.TirasTuberia);
        Assert.Equal(10, r.Enchufes);
        Assert.Equal(10, r.CajasEnchufe);
        Assert.Equal(10, r.CajasDerivacion);
        Assert.Equal(1, r.Automaticos);
        Assert.Equal(1, r.Diferenciales);
    }

    [Fact]
    public void Electricidad_LasCantidadesQueSeCuentanNoLlevanMargen()
    {
        var r = Calculos.Electricidad(3, 3, 2.5, enchufes: 4, distanciaTablero: 2, margen: 10);

        Assert.Equal(22.8, r.Recorrido);
        Assert.Equal(26, r.RecorridoConMargen);
        Assert.Equal(9, r.TirasTuberia);
        Assert.Equal(4, r.Enchufes);
    }

    [Theory]
    [InlineData(5, 2, 2.4, true)]
    [InlineData(0.4, 2, 2.4, false)]
    [InlineData(5, 31, 2.4, false)]
    [InlineData(5, 2, 1.4, false)]
    [InlineData(5, 2, 6.5, false)]
    public void ValidarHabitacion_RechazaMedidasFueraDeRango(double largo, double ancho, double alto, bool valida)
    {
        string? error = Calculos.ValidarHabitacion(largo, ancho, alto);

        Assert.Equal(valida, error == null);
    }
}
