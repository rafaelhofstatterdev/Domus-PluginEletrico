using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Routing;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 3 — Conduit Builder (atalho CB). Lê os ElectricalSystem do modelo,
    /// traça o roteamento ortogonal 3D entre dispositivos e painel, insere
    /// ConduitFitting nas mudanças de direção e dimensiona pela NBR 5410.
    /// </summary>
    public sealed class DmConduitBuilderCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var settings = DmProjectSettings.Read(doc);
            if (!settings.SetupConcluido)
            {
                TaskDialog.Show("DmEletrico", "Execute o Setup antes de construir os conduítes.");
                return Result.Cancelled;
            }

            var service = new ConduitBuilderService();
            ConduitBuildReport report;

            using (var tx = new Transaction(doc, "DmEletrico — Construir Conduítes"))
            {
                tx.Start();
                report = service.Build(doc, settings);
                tx.Commit();
            }

            TaskDialog.Show("DmEletrico — Construir Conduítes", report.ToString());
            return Result.Succeeded;
        }
    }
}
