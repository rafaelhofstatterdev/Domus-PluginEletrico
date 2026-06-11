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

            var orfaos = elementos.Where(SemCircuito).ToList();

            if (orfaos.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum dispositivo elétrico desconectado encontrado.");
                return Result.Succeeded;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{orfaos.Count} dispositivo(s) sem circuito atribuído:\n");
            foreach (var fi in orfaos.Take(50))
            {
                var p = (fi.Location as LocationPoint)?.Point;
                var loc = p != null ? $"({p.X:F1}, {p.Y:F1}, {p.Z:F1})" : "—";
                sb.AppendLine($"• [{fi.Id}] {fi.Category?.Name} — {fi.Name} {loc}");
            }
            if (orfaos.Count > 50) sb.AppendLine($"\n… e mais {orfaos.Count - 50}.");

            // TODO: substituir por janela WPF com seleção/zoom nos elementos.
            TaskDialog.Show("DmEletrico — Dispositivos Desconectados", sb.ToString());
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
