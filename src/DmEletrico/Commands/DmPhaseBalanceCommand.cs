using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Circuits;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 6 — Balanceamento de fases. Reorganiza os circuitos dos QDCs
    /// para equilibrar a carga entre as fases A, B e C.
    /// </summary>
    public sealed class DmPhaseBalanceCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var report = new PhaseBalanceService().Balance(doc);
            TaskDialog.Show("DmEletrico — Balanceamento de Fases", report.ToString());
            return Result.Succeeded;
        }
    }
}
