using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Wiring;
using DmEletrico.UI.Wiring;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Passo 2 — Tabela de Fiação. Calcula e exibe o quantitativo de condutores por
    /// bitola, com descrição comercial e margem de segurança configuradas.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmWireListCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var cfg = DmWiringSettings.Read(doc);
            var rows = WiringService.GerarTabela(doc, cfg);

            if (rows.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhuma fiação encontrada. Construa conduítes e aplique a fiação primeiro.");
                return Result.Succeeded;
            }

            new DmWireListWindow(rows).ShowDialog();
            return Result.Succeeded;
        }
    }
}
