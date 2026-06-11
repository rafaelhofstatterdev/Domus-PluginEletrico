using System.Collections.Generic;
using System.Linq;

namespace DmEletrico.Core.Calculation
{
    /// <summary>
    /// Dimensionamento do diâmetro do conduíte pela taxa de ocupação máxima
    /// (NBR 5410 / NBR 5444): a soma das áreas dos condutores (com isolação) não
    /// deve ultrapassar 40% da área interna do eletroduto para 3 ou mais cabos.
    ///
    /// As áreas externas de cabo e os diâmetros internos de eletroduto abaixo são
    /// valores representativos e devem ser conferidos com os catálogos/normas
    /// antes do uso em produção.
    /// </summary>
    public static class ConduitSizing
    {
        // Área externa aproximada do condutor isolado (mm²) por seção do cobre (mm²).
        private static readonly Dictionary<double, double> AreaExternaCabo = new()
        {
            [1.5] = 8.30, [2.5] = 11.0, [4] = 15.2, [6] = 18.1, [10] = 36.3,
            [16] = 50.3, [25] = 73.9, [35] = 95.0, [50] = 127.7, [70] = 176.7,
            [95] = 227.0, [120] = 285.0, [150] = 357.0, [185] = 437.0, [240] = 558.0
        };

        // Diâmetro nominal (mm) → área interna útil aproximada do eletroduto (mm²).
        private static readonly (double nominalMm, double areaInternaMm2)[] Eletrodutos =
        {
            (16, 122), (20, 196), (25, 333), (32, 556), (40, 872),
            (50, 1385), (60, 1963), (75, 4185)
        };

        /// <summary>Taxa máxima de ocupação conforme número de condutores.</summary>
        public static double TaxaOcupacao(int nCondutores)
            => nCondutores >= 3 ? 0.40 : (nCondutores == 2 ? 0.31 : 0.53);

        /// <summary>
        /// Retorna o menor diâmetro nominal de eletroduto (mm) que acomoda
        /// <paramref name="nCondutores"/> condutores de seção <paramref name="secaoMm2"/>.
        /// </summary>
        public static double DiametroNominal(double secaoMm2, int nCondutores)
        {
            var areaCabo = AreaExternaCabo.TryGetValue(secaoMm2, out var a) ? a : EstimarArea(secaoMm2);
            var areaOcupada = areaCabo * nCondutores;
            var areaNecessaria = areaOcupada / TaxaOcupacao(nCondutores);

            foreach (var (nominal, areaInterna) in Eletrodutos)
            {
                if (areaInterna >= areaNecessaria)
                    return nominal;
            }
            return Eletrodutos.Last().nominalMm;
        }

        private static double EstimarArea(double secaoMm2)
        {
            // Fallback: usa a maior seção tabelada se exceder a tabela.
            var maior = AreaExternaCabo.Keys.Max();
            return AreaExternaCabo[maior] * (secaoMm2 / maior);
        }
    }
}
