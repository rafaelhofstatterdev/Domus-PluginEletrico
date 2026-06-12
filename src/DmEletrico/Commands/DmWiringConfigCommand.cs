using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Wiring;
using DmEletrico.UI.Wiring;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Passos 3/4/5 — Configuração de Fiação. Descrições/ocultação por bitola,
    /// margem de segurança e regras de neutro/aterramento. Persiste no projeto.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmWiringConfigCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var cfg = DmWiringSettings.Read(doc);
            var vm = new DmWiringConfigViewModel(cfg);
            var window = new DmWiringConfigWindow { DataContext = vm };
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            using (var tx = new Transaction(doc, "DmEletrico — Configuração de Fiação"))
            {
                tx.Start();
                vm.ToSettings().Write(doc);
                tx.Commit();
            }

            TaskDialog.Show("DmEletrico", "Configuração de fiação salva.");
            return Result.Succeeded;
        }
    }
}
