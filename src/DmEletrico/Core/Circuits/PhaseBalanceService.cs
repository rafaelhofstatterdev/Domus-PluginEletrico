using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace DmEletrico.Core.Circuits
{
    public sealed class PhaseBalanceReport
    {
        public int CircuitosBalanceados { get; set; }
        public List<string> Quadros { get; } = new();

        public override string ToString()
        {
            if (CircuitosBalanceados == 0 && Quadros.Count == 0)
                return "Nenhum circuito encontrado para balancear.";

            var sb = new StringBuilder();
            sb.AppendLine($"Circuitos balanceados: {CircuitosBalanceados}\n");
            foreach (var q in Quadros) sb.AppendLine(q);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Requisito 6 — Balanceamento de fases. Para cada QDC, distribui os circuitos
    /// entre as fases A, B e C buscando equilíbrio de carga. Estratégia gulosa:
    /// circuitos ordenados por carga decrescente são alocados às fases menos
    /// carregadas; circuitos bi/tripolares ocupam 2/3 fases dividindo a carga.
    /// A fase resultante é gravada em Dm_Fase nos dispositivos do circuito.
    ///
    /// Gerencia a própria transação.
    /// </summary>
    public sealed class PhaseBalanceService
    {
        private static readonly char[] Fases = { 'A', 'B', 'C' };

        public PhaseBalanceReport Balance(Document doc)
        {
            var report = new PhaseBalanceReport();

            var sistemas = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .Where(s => s.BaseEquipment != null)
                .ToList();

            var porQuadro = sistemas.GroupBy(s => s.BaseEquipment!.Id);

            using var tx = new Transaction(doc, "DmEletrico — Balanceamento de Fases");
            tx.Start();

            foreach (var grupo in porQuadro)
            {
                var painel = doc.GetElement(grupo.Key);
                var totais = new double[3];

                var circuitos = grupo
                    .Select(s => new { System = s, Carga = CargaDoCircuito(s) })
                    .OrderByDescending(x => x.Carga)
                    .ToList();

                foreach (var c in circuitos)
                {
                    var polos = Clamp(LerPolos(c.System), 1, 3);
                    var escolhidas = FasesMenosCarregadas(totais, polos);
                    var parcela = polos > 0 ? c.Carga / polos : c.Carga;

                    foreach (var idx in escolhidas) totais[idx] += parcela;

                    var label = new string(escolhidas.Select(i => Fases[i]).ToArray());
                    GravarFase(c.System, label);
                    report.CircuitosBalanceados++;
                }

                report.Quadros.Add(
                    $"{painel?.Name ?? grupo.Key.ToString()}: " +
                    $"A={totais[0]:F0} VA, B={totais[1]:F0} VA, C={totais[2]:F0} VA " +
                    $"(desbalanço {Desbalanco(totais):F0} VA)");
            }

            tx.Commit();
            return report;
        }

        private static IList<int> FasesMenosCarregadas(double[] totais, int quantidade)
            => Enumerable.Range(0, 3)
                .OrderBy(i => totais[i])
                .Take(quantidade)
                .OrderBy(i => i)
                .ToList();

        private static double Desbalanco(double[] totais) => totais.Max() - totais.Min();

        private static double CargaDoCircuito(ElectricalSystem s)
            => s.Elements.Cast<Element>().Sum(e => ReadDouble(e, DmParameters.Potencia));

        private static int LerPolos(ElectricalSystem s)
        {
            foreach (Element e in s.Elements)
            {
                var v = (int)ReadDouble(e, DmParameters.NumeroPolos, fallback: 0);
                if (v > 0) return v;
            }
            return 1;
        }

        private static void GravarFase(ElectricalSystem s, string label)
        {
            foreach (Element e in s.Elements)
                e.LookupParameter(DmParameters.Fase)?.Set(label);
        }

        private static double ReadDouble(Element e, string name, double fallback = 0)
        {
            var p = e.LookupParameter(name);
            if (p == null) return fallback;
            return p.StorageType switch
            {
                StorageType.Double => p.AsDouble(),
                StorageType.Integer => p.AsInteger(),
                _ => fallback
            };
        }

        private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
    }
}
