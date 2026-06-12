using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using DmEletrico.UI.Routing;
using DmEletrico.UI.Setup;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Reabre a janela de configuração do Conduit Builder e salva a escolha como a
    /// configuração da sessão (usada pelo "Construir Conduítes" sem reabrir a janela).
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmConduitConfigCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var tipos = new FilteredElementCollector(doc)
                .OfClass(typeof(ConduitType))
                .Cast<ConduitType>()
                .Select(t => new ConduitTypeOption(t.Name, t.Id.Value.ToString()))
                .ToList();

            if (tipos.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum tipo de conduíte carregado no projeto.");
                return Result.Cancelled;
            }

            var vm = new DmConduitBuilderViewModel(tipos).WithDefaults();
            var window = new DmConduitBuilderWindow { DataContext = vm };
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            DmConduitBuilderCommand.DefinirConfig(vm.ToOptions());
            TaskDialog.Show("DmEletrico", "Configuração de conduítes salva para esta sessão.");
            return Result.Succeeded;
        }
    }
}
