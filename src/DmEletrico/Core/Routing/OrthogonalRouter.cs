using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Routing
{
    /// <summary>
    /// Gera um caminho ortogonal 3D entre dois pontos passando por uma "espinha"
    /// horizontal numa elevação fixa (requisito 3 — percurso horizontal + vertical).
    ///
    /// Sequência: dispositivo → sobe à espinha → corre em X → corre em Y → desce ao
    /// painel. Pontos coincidentes são removidos para não gerar trechos de
    /// comprimento zero.
    ///
    /// Puro (apenas geometria), sem dependência de Document — testável isoladamente.
    /// </summary>
    public static class OrthogonalRouter
    {
        private const double Tol = 1e-6;

        /// <param name="from">Ponto do conector do dispositivo (pés).</param>
        /// <param name="to">Ponto do conector do painel (pés).</param>
        /// <param name="spineElevation">Elevação da espinha horizontal (pés).</param>
        public static IList<XYZ> Route(XYZ from, XYZ to, double spineElevation)
        {
            var pontos = new List<XYZ>
            {
                from,
                new XYZ(from.X, from.Y, spineElevation),  // sobe até a espinha
                new XYZ(to.X,   from.Y, spineElevation),  // corre em X
                new XYZ(to.X,   to.Y,   spineElevation),  // corre em Y
                to                                        // desce ao painel
            };

            return Dedupe(pontos);
        }

        /// <summary>Roteamento direto: segmento único entre origem e destino.</summary>
        public static IList<XYZ> RouteDireto(XYZ from, XYZ to) => Dedupe(new List<XYZ> { from, to });

        /// <summary>
        /// Roteamento "pela parede/piso": percurso ortogonal mantido na altura dos
        /// dispositivos — horizontal no plano e vertical para vencer o desnível,
        /// sem subir até o teto.
        /// </summary>
        public static IList<XYZ> RouteParede(XYZ from, XYZ to)
        {
            // Estritamente ortogonal (Manhattan): X, depois Y, depois Z — nunca
            // diagonal, para hugar parede/piso em vez de cruzar o vazio.
            var pontos = new List<XYZ>
            {
                from,
                new XYZ(to.X, from.Y, from.Z), // corre em X na altura de origem
                new XYZ(to.X, to.Y, from.Z),   // corre em Y na altura de origem
                to                             // sobe/desce até o destino
            };
            return Dedupe(pontos);
        }

        /// <summary>Roteamento "pelo teto": sobe à espinha, corre e desce.</summary>
        public static IList<XYZ> RouteTeto(XYZ from, XYZ to, double spineElevation)
            => Route(from, to, spineElevation);

        /// <summary>Comprimento total (em pés) de um caminho.</summary>
        public static double Comprimento(IList<XYZ> pts)
        {
            double total = 0;
            for (int i = 0; i < pts.Count - 1; i++) total += pts[i].DistanceTo(pts[i + 1]);
            return total;
        }

        private static IList<XYZ> Dedupe(IList<XYZ> pts)
        {
            var result = new List<XYZ>();
            foreach (var p in pts)
            {
                if (result.Count == 0 || result[result.Count - 1].DistanceTo(p) > Tol)
                    result.Add(p);
            }
            return result;
        }
    }
}
