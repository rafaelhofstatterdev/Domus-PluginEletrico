using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.UI.Circuits;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 3 — Gerenciador de circuitos. Lista os circuitos e permite
    /// reatribuir a outro QDC, renumerar e balancear fases.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmCircuitManagerCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            new DmCircuitManagerWindow(doc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
