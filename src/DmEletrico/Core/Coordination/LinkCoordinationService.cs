using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Coordination
{
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
        public int LinksCarregados { get; set; }
        public int LinksNaoCarregados { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Links carregados: {LinksCarregados} | não carregados: {LinksNaoCarregados}");
            sb.AppendLine($"Elementos elétricos encontrados nos links: {Elementos.Count}\n");

            foreach (var porLink in Elementos.GroupBy(e => e.LinkName))
            {
                sb.AppendLine($"• {porLink.Key}:");
                foreach (var porCat in porLink.GroupBy(e => e.Categoria))
                    sb.AppendLine($"    {porCat.Key}: {porCat.Count()}");
            }
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
            }

            return report;
        }
    }
}
