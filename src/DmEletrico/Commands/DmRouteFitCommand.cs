using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Routing;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 4 — Route Fit (atalho RF). Detecta conduítes/eletrocalhas com
    /// geometria inválida (comprimento zero, curvas órfãs) após movimentação de
    /// dispositivos e restaura a continuidade da rede física.
    /// </summary>
    public sealed class DmRouteFitCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var settings = DmProjectSettings.Read(doc);
            var service = new RouteFitService();
            RouteFitReport report;

            using (var tx = new Transaction(doc, "DmEletrico — Ajuste de Rotas"))
            {
                tx.Start();
                report = service.Fit(doc, settings);
                tx.Commit();
            }

            TaskDialog.Show("DmEletrico — Ajuste de Rotas", report.ToString());
            return Result.Succeeded;
        }
    }
}
