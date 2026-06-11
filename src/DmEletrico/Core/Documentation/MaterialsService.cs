using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DmEletrico.Core;

namespace DmEletrico.Core.Documentation
{
    /// <summary>
    /// Quantitativos calculados para itens que não são elementos modelados
    /// individualmente (requisito 9/BIM 5D): condutores por bitola, eletrocalhas
    /// por tipo e disjuntores por corrente nominal.
    /// </summary>
    public static class MaterialsService
    {
        public sealed class MaterialsReport
        {
            public List<(double secao, double metros)> Condutores { get; } = new();
            public List<(string tipo, int qtd, double metros)> Eletrocalhas { get; } = new();
            public List<(double disjuntor, int qtd)> Disjuntores { get; } = new();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine("Condutores por bitola (m de cabo):");
                foreach (var (secao, metros) in Condutores.OrderBy(x => x.secao))
                    sb.AppendLine($"  {secao:F1} mm²: {metros:F1} m");
                if (Condutores.Count == 0) sb.AppendLine("  —");

                sb.AppendLine("\nEletrocalhas/perfilados por tipo:");
                foreach (var (tipo, qtd, metros) in Eletrocalhas)
                    sb.AppendLine($"  {tipo}: {qtd} un, {metros:F1} m");
                if (Eletrocalhas.Count == 0) sb.AppendLine("  —");

                sb.AppendLine("\nDisjuntores por corrente nominal:");
                foreach (var (disj, qtd) in Disjuntores.OrderBy(x => x.disjuntor))
                    sb.AppendLine($"  {disj:F0} A: {qtd} un");
                if (Disjuntores.Count == 0) sb.AppendLine("  —");

                return sb.ToString();
            }
        }

        public static MaterialsReport Compute(Document doc)
        {
            var report = new MaterialsReport();
            CalcularCondutores(doc, report);
            CalcularEletrocalhas(doc, report);
            CalcularDisjuntores(doc, report);
            return report;
        }

        private static void CalcularCondutores(Document doc, MaterialsReport report)
        {
            var conduites = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Conduit)
                .WhereElementIsNotElementType();

            var porSecao = new Dictionary<double, double>();
            foreach (var c in conduites)
            {
                var secao = c.LookupParameter(DmParameters.SecaoAdotada)?.AsDouble() ?? 0;
                if (secao <= 0) continue;

                var nCond = c.LookupParameter(DmParameters.NumCondutores)?.AsInteger() ?? 0;
                if (nCond <= 0) nCond = 3;

                var lenFeet = (c.Location as LocationCurve)?.Curve?.Length ?? 0;
                var metros = UnitUtils.ConvertFromInternalUnits(lenFeet, UnitTypeId.Meters) * nCond;

                porSecao[secao] = porSecao.TryGetValue(secao, out var v) ? v + metros : metros;
            }
            report.Condutores.AddRange(porSecao.Select(kv => (kv.Key, kv.Value)));
        }

        private static void CalcularEletrocalhas(Document doc, MaterialsReport report)
        {
            var trays = new FilteredElementCollector(doc)
                .OfClass(typeof(CableTray))
                .Cast<CableTray>();

            foreach (var grupo in trays.GroupBy(t => doc.GetElement(t.GetTypeId())?.Name ?? "—"))
            {
                var metros = grupo.Sum(t =>
                    UnitUtils.ConvertFromInternalUnits((t.Location as LocationCurve)?.Curve?.Length ?? 0, UnitTypeId.Meters));
                report.Eletrocalhas.Add((grupo.Key, grupo.Count(), metros));
            }
        }

        private static void CalcularDisjuntores(Document doc, MaterialsReport report)
        {
            var sistemas = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>();

            var porDisj = new Dictionary<double, int>();
            foreach (var s in sistemas)
            {
                var disj = s.Elements.Cast<Element>()
                    .Select(e => e.LookupParameter(DmParameters.Disjuntor)?.AsDouble() ?? 0)
                    .FirstOrDefault(v => v > 0);
                if (disj <= 0) continue;
                porDisj[disj] = porDisj.TryGetValue(disj, out var v) ? v + 1 : 1;
            }
            report.Disjuntores.AddRange(porDisj.Select(kv => (kv.Key, kv.Value)));
        }
    }
}
