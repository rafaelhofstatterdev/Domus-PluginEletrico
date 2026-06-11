using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Shortcuts;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 12 — registra/mescla os atalhos CB/DC/MT/RF no
    /// KeyboardShortcuts.xml da versão do Revit, reportando conflitos.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmShortcutsCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var versao = data.Application.Application.VersionNumber;
            var result = ShortcutsService.Apply(versao);
            TaskDialog.Show("DmEletrico — Atalhos de Teclado", result.ToString());
            return Result.Succeeded;
        }
    }
}
