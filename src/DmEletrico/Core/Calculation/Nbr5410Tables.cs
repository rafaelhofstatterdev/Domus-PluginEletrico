using System.Collections.Generic;
using System.Linq;

namespace DmEletrico.Core.Calculation
{
    /// <summary>
    /// Tabelas de referência da ABNT NBR 5410 usadas pelo dimensionamento.
    ///
    /// IMPORTANTE: os valores abaixo são uma base de partida representativa para
    /// o esqueleto e DEVEM ser revisados/completados contra a edição vigente da
    /// norma antes do uso em produção (especialmente as tabelas de capacidade de
    /// condução por método de instalação — Tabelas 36 a 39 da NBR 5410:2004).
    /// </summary>
    public static class Nbr5410Tables
    {
        /// <summary>Seções comerciais de condutores de cobre (mm²).</summary>
        public static readonly double[] SecoesComerciais =
        {
            1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240, 300, 400
        };

        /// <summary>
        /// Fator de Correção por Temperatura (FCT) para isolação PVC (70 °C),
        /// temperatura ambiente de referência 30 °C. Tabela 40 (extrato).
        /// </summary>
        private static readonly Dictionary<int, double> FctPvcPorTemperatura = new()
        {
            [10] = 1.22, [15] = 1.17, [20] = 1.12, [25] = 1.06, [30] = 1.00,
            [35] = 0.94, [40] = 0.87, [45] = 0.79, [50] = 0.71, [55] = 0.61, [60] = 0.50
        };

        /// <summary>
        /// Fator de Correção por Agrupamento (FCA) — número de circuitos/cabos
        /// agrupados. Tabela 42 (extrato, método de referência embutido).
        /// </summary>
        private static readonly Dictionary<int, double> FcaPorAgrupamento = new()
        {
            [1] = 1.00, [2] = 0.80, [3] = 0.70, [4] = 0.65, [5] = 0.60,
            [6] = 0.57, [7] = 0.54, [8] = 0.52, [9] = 0.50
        };

        /// <summary>
        /// Capacidade de condução de corrente (A) por seção (mm²) — cobre, PVC.
        /// Extrato simplificado para o método B1 (condutores em eletroduto
        /// embutido em alvenaria), 2 condutores carregados. Substituir pela
        /// tabela completa correspondente ao método de instalação configurado.
        /// </summary>
        private static readonly Dictionary<double, double> CapacidadeB1 = new()
        {
            [1.5] = 17.5, [2.5] = 24, [4] = 32, [6] = 41, [10] = 57, [16] = 76,
            [25] = 101, [35] = 125, [50] = 151, [70] = 192, [95] = 232,
            [120] = 269, [150] = 309, [185] = 353, [240] = 415, [300] = 477
        };

        /// <summary>Resistividade do cobre (Ω·mm²/m) a ~70 °C para queda de tensão.</summary>
        public const double ResistividadeCobre = 0.0224;

        public static double Fct(double temperaturaAmbienteC)
        {
            int chave = MaisProximo(FctPvcPorTemperatura.Keys, (int)temperaturaAmbienteC);
            return FctPvcPorTemperatura[chave];
        }

        public static double Fca(int circuitosAgrupados)
        {
            if (circuitosAgrupados <= 1) return 1.0;
            if (FcaPorAgrupamento.TryGetValue(circuitosAgrupados, out var f)) return f;
            return FcaPorAgrupamento.Values.Min(); // saturação para muitos circuitos
        }

        public static double CapacidadeCorrente(double secaoMm2)
            => CapacidadeB1.TryGetValue(secaoMm2, out var i) ? i : 0;

        /// <summary>Menor seção comercial cuja capacidade corrigida atende a corrente exigida.</summary>
        public static double SecaoPorCapacidade(double correnteCorrigidaA)
        {
            foreach (var s in SecoesComerciais)
            {
                if (CapacidadeCorrente(s) >= correnteCorrigidaA)
                    return s;
            }
            return SecoesComerciais.Last();
        }

        private static int MaisProximo(IEnumerable<int> chaves, int alvo)
            => chaves.OrderBy(k => System.Math.Abs(k - alvo)).First();
    }
}
