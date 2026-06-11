using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.UI.Setup;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 2 — Setup. Abre o diálogo WPF de configuração, injeta os
    /// parâmetros compartilhados e grava as variáveis globais no ProjectInformation.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmSetupCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var settings = DmProjectSettings.Read(doc);

            var tiposConduite = new FilteredElementCollector(doc)
                .OfClass(typeof(ConduitType))
                .Cast<ConduitType>()
                .Select(t => new ConduitTypeOption(t.Name, t.Id.Value.ToString()))
                .ToList();

            var vm = new DmSetupViewModel(settings, tiposConduite);
            var window = new DmSetupWindow { DataContext = vm };

            var ok = window.ShowDialog();
            if (ok != true)
                return Result.Cancelled;

            using (var tx = new Transaction(doc, "DmEletrico — Setup"))
            {
                tx.Start();

                // 1) Injeção dos parâmetros compartilhados.
                SharedParameterInjector.EnsureParameters(doc);

                // 1.1) Carrega famílias necessárias (TAG de conduíte etc.), se houver.
                FamilyLoaderService.LoadMissingFamilies(doc);

                // 2) Persistência das variáveis globais.
                vm.ToSettings().Write(doc);

                // 3) Marca o setup como concluído (libera os demais comandos).
                doc.ProjectInformation.LookupParameter(DmParameters.SetupConcluido)?.Set(1);

                tx.Commit();
            }

            TaskDialog.Show("DmEletrico", "Setup concluído. Parâmetros injetados e comandos liberados.");
            return Result.Succeeded;
        }
    }
}
