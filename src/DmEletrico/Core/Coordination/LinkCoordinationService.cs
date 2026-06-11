using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DmEletrico.Core;

namespace DmEletrico.Core.Coordination
{
    public sealed class LinkCircuitInfo
    {
        public string LinkName { get; set; } = "";
        public string Quadro { get; set; } = "(sem QDC)";
        public string Numero { get; set; } = "?";
        public double CargaVa { get; set; }
    }

    public sealed class LinkElementInfo
    {
        public string LinkName { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Nome { get; set; } = "";
        public ElementId IdNoLink { get; set; } = ElementId.InvalidElementId;
        public XYZ? PontoGlobal { get; set; }
    }

    public sealed class LinkCoordinationReport
    {
        public List<LinkElementInfo> Elementos { get; } = new();
        public List<LinkCircuitInfo> Circuitos { get; } = new();
        public int LinksCarregados { get; set; }
        public int LinksNaoCarregados { get; set; }

        public double CargaTotalVa => Circuitos.Sum(c => c.CargaVa);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Links carregados: {LinksCarregados} | não carregados: {LinksNaoCarregados}");
            sb.AppendLine($"Elementos elétricos nos links: {Elementos.Count}");

            foreach (var porLink in Elementos.GroupBy(e => e.LinkName))
            {
                sb.AppendLine($"• {porLink.Key}:");
                foreach (var porCat in porLink.GroupBy(e => e.Categoria))
                    sb.AppendLine($"    {porCat.Key}: {porCat.Count()}");
            }

            sb.AppendLine($"\nCircuitos consolidados dos links: {Circuitos.Count}");
            foreach (var porQuadro in Circuitos.GroupBy(c => c.Quadro))
            {
                var total = porQuadro.Sum(c => c.CargaVa);
                sb.AppendLine($"• {porQuadro.Key}: {porQuadro.Count()} circuito(s), {total:F0} VA");
            }
            sb.AppendLine($"\nCarga total importada: {CargaTotalVa:F0} VA");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Requisito 11 — Coordenação entre modelos. Lê os elementos elétricos
    /// presentes em RevitLinkInstance (modelos vinculados), aplicando a transformada
    /// do link para obter as coordenadas no modelo hospedeiro.
    ///
    /// A importação consolidada de circuitos/cargas e o roteamento de conduítes
    /// atravessando fronteiras entre modelos federados partem deste levantamento
    /// (roadmap v3.0+).
    /// </summary>
    public sealed class LinkCoordinationService
    {
        private static readonly BuiltInCategory[] Categorias =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalEquipment
        };

        public LinkCoordinationReport ReadElectrical(Document host)
        {
            var report = new LinkCoordinationReport();

            var links = new FilteredElementCollector(host)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            foreach (var link in links)
            {
                var linkedDoc = link.GetLinkDocument();
                if (linkedDoc == null)
                {
                    report.LinksNaoCarregados++;
                    continue;
                }
                report.LinksCarregados++;

                var transform = link.GetTotalTransform();
                var linkName = link.Name;

                var filter = new ElementMulticategoryFilter(Categorias);
                var elementos = new FilteredElementCollector(linkedDoc)
                    .WherePasses(filter)
                    .WhereElementIsNotElementType();

                foreach (var e in elementos)
                {
                    var local = (e.Location as LocationPoint)?.Point;
                    report.Elementos.Add(new LinkElementInfo
                    {
                        LinkName = linkName,
                        Categoria = e.Category?.Name ?? "—",
                        Nome = e.Name,
                        IdNoLink = e.Id,
                        PontoGlobal = local != null ? transform.OfPoint(local) : null
                    });
                }

                // Importação consolidada de circuitos/cargas do link.
                var sistemas = new FilteredElementCollector(linkedDoc)
                    .OfClass(typeof(ElectricalSystem))
                    .Cast<ElectricalSystem>();

                foreach (var s in sistemas)
                {
                    var carga = s.Elements.Cast<Element>()
                        .Sum(el => el.LookupParameter(DmParameters.Potencia)?.AsDouble() ?? 0);
                    report.Circuitos.Add(new LinkCircuitInfo
                    {
                        LinkName = linkName,
                        Quadro = s.BaseEquipment?.Name ?? "(sem QDC)",
                        Numero = string.IsNullOrWhiteSpace(s.CircuitNumber) ? "?" : s.CircuitNumber,
                        CargaVa = carga
                    });
                }
            }

            return report;
        }
    }
}
