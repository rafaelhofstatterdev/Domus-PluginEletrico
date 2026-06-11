using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 6 — Rastreador de Dispositivos Desconectados. Varre o modelo,
    /// identifica famílias elétricas sem ElectricalSystem atribuído e lista os
    /// elementos órfãos com categoria e localização.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmCheckDisconnectedCommand : DmCommandBase
    {
        private static readonly BuiltInCategory[] Categorias =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalEquipment
        };

        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var filter = new ElementMulticategoryFilter(Categorias);
            var elementos = new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();

            var semCircuito = elementos.Where(SemCircuito).ToList();

            // Circuitos sem QDC atribuído.
            var circuitosSemQdc = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .Where(s => s.BaseEquipment == null)
                .ToList();

            if (semCircuito.Count == 0 && circuitosSemQdc.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum dispositivo desconectado nem circuito sem QDC.");
                return Result.Succeeded;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Dispositivos SEM circuito: {semCircuito.Count}");
            foreach (var fi in semCircuito.Take(40))
            {
                var p = (fi.Location as LocationPoint)?.Point;
                var loc = p != null ? $"({p.X:F1}, {p.Y:F1}, {p.Z:F1})" : "—";
                sb.AppendLine($"• [{fi.Id}] {fi.Category?.Name} — {fi.Name} {loc}");
            }
            if (semCircuito.Count > 40) sb.AppendLine($"… e mais {semCircuito.Count - 40}.");

            sb.AppendLine($"\nCircuitos SEM QDC atribuído: {circuitosSemQdc.Count}");
            foreach (var s in circuitosSemQdc.Take(40))
                sb.AppendLine($"• [{s.Id}] Circuito {(string.IsNullOrWhiteSpace(s.CircuitNumber) ? "?" : s.CircuitNumber)} — {s.Name}");
            if (circuitosSemQdc.Count > 40) sb.AppendLine($"… e mais {circuitosSemQdc.Count - 40}.");

            TaskDialog.Show("DmEletrico — Desconectados", sb.ToString());
            return Result.Succeeded;
        }

        private static bool SemCircuito(FamilyInstance fi)
        {
            var mep = fi.MEPModel;
            if (mep == null) return true;

            var systems = mep.GetElectricalSystems();
            return systems == null || systems.Count == 0;
        }
    }
}
