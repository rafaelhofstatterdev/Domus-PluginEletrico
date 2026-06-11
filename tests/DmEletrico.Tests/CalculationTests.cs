using DmEletrico.Core.Calculation;
using Xunit;

namespace DmEletrico.Tests
{
    public class Nbr5410TablesTests
    {
        [Fact]
        public void Fct_30C_eh_um()
            => Assert.Equal(1.0, Nbr5410Tables.Fct(30), 3);

        [Fact]
        public void Fct_40C_reduz()
            => Assert.True(Nbr5410Tables.Fct(40) < 1.0);

        [Theory]
        [InlineData(1, 1.0)]
        [InlineData(3, 0.70)]
        public void Fca_por_agrupamento(int n, double esperado)
            => Assert.Equal(esperado, Nbr5410Tables.Fca(n), 3);

        [Theory]
        [InlineData(15, 16)]
        [InlineData(16, 16)]
        [InlineData(17, 20)]
        public void Disjuntor_comercial_proximo_acima(double corrente, double esperado)
            => Assert.Equal(esperado, Nbr5410Tables.DisjuntorComercial(corrente));

        [Fact]
        public void Secao_cresce_com_corrente()
        {
            var pequena = Nbr5410Tables.SecaoPorCapacidade(10);
            var grande = Nbr5410Tables.SecaoPorCapacidade(100);
            Assert.True(grande >= pequena);
        }
    }

    public class ConduitSizingTests
    {
        [Fact]
        public void Diametro_aumenta_com_numero_de_condutores()
        {
            var poucos = ConduitSizing.DiametroNominal(2.5, 3);
            var muitos = ConduitSizing.DiametroNominal(2.5, 9);
            Assert.True(muitos >= poucos);
        }

        [Fact]
        public void Taxa_ocupacao_40_para_tres_ou_mais()
            => Assert.Equal(0.40, ConduitSizing.TaxaOcupacao(3), 3);
    }

    public class ElectricalCalculatorTests
    {
        [Fact]
        public void Corrente_de_projeto_eh_va_sobre_tensao()
        {
            var calc = new ElectricalCalculator();
            var r = calc.Calcular(new TrechoInput
            {
                PotenciaAparenteVa = 2200,
                TensaoNominalV = 220,
                TemperaturaAmbienteC = 30,
                ComprimentoM = 10,
                CircuitosAgrupados = 1
            });
            Assert.Equal(10.0, r.CorrenteProjetoA, 3);
            Assert.True(r.SecaoAdotadaMm2 > 0);
            Assert.True(r.QuedaTensaoPercent > 0);
        }

        [Fact]
        public void Queda_de_tensao_cresce_com_comprimento()
        {
            var calc = new ElectricalCalculator();
            TrechoResultado R(double l) => calc.Calcular(new TrechoInput
            {
                PotenciaAparenteVa = 2200, TensaoNominalV = 220, ComprimentoM = l
            });
            Assert.True(R(50).QuedaTensaoPercent > R(10).QuedaTensaoPercent);
        }
    }
}
