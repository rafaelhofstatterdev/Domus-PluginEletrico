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
