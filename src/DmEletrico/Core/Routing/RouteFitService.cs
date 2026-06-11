using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace DmEletrico.Core.Routing
{
    public sealed class RouteFitReport
    {
        public int TrechosInvalidos { get; set; }
        public int TrechosRemovidos { get; set; }
        public int FittingsOrfaosRemovidos { get; set; }
        public List<string> Avisos { get; } = new();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Trechos com geometria inválida: {TrechosInvalidos}");
            sb.AppendLine($"Trechos removidos: {TrechosRemovidos}");
            sb.AppendLine($"Curvas órfãs removidas: {FittingsOrfaosRemovidos}");
            if (Avisos.Count > 0)
            {
                sb.AppendLine("\nAvisos:");
                foreach (var a in Avisos.Take(20)) sb.AppendLine("• " + a);
                if (Avisos.Count > 20) sb.AppendLine($"… e mais {Avisos.Count - 20}.");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Route Fit (requisito 4). Detecta e corrige geometria inválida na rede
    /// física após movimentação de dispositivos:
    ///   - trechos de comprimento (quase) zero → removidos;
    ///   - curvas/conexões órfãs (sem nenhum conector ligado) → removidas.
    ///
    /// Cobre conduítes (Conduit) e eletrocalhas (CableTray). O re-traçado completo
    /// de um circuito alterado é feito re-executando o Conduit Builder; este
    /// serviço cuida da limpeza/continuidade da malha existente.
    ///
    /// Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public sealed class RouteFitService
    {
        private const double LengthTolFeet = 1e-3; // ~0,3 mm

        public RouteFitReport Fit(Document doc)
        {
            var report = new RouteFitReport();

            RemoverTrechosDegenerados<Conduit>(doc, report);
            RemoverTrechosDegenerados<CableTray>(doc, report);
            RemoverFittingsOrfaos(doc, report);

            return report;
        }

        private void RemoverTrechosDegenerados<T>(Document doc, RouteFitReport report) where T : MEPCurve
        {
            var elementos = new FilteredElementCollector(doc)
                .OfClass(typeof(T))
                .Cast<T>()
                .ToList();

            var remover = new List<ElementId>();
            foreach (var e in elementos)
            {
                var curva = (e.Location as LocationCurve)?.Curve;
                var len = curva?.Length ?? 0.0;
                if (len <= LengthTolFeet)
                {
                    report.TrechosInvalidos++;
                    remover.Add(e.Id);
                }
            }

            foreach (var id in remover)
            {
                doc.Delete(id);
                report.TrechosRemovidos++;
            }
        }

        private void RemoverFittingsOrfaos(Document doc, RouteFitReport report)
        {
            var fittings = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            var remover = new List<ElementId>();
            foreach (var f in fittings)
            {
                var manager = f.MEPModel?.ConnectorManager;
                if (manager == null) continue;

                var algumLigado = manager.Connectors
                    .Cast<Connector>()
                    .Any(c => c.IsConnected);

                if (!algumLigado)
                    remover.Add(f.Id);
            }

            foreach (var id in remover)
            {
                doc.Delete(id);
                report.FittingsOrfaosRemovidos++;
            }
        }
    }
}
