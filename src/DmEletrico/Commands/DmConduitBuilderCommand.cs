using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.Core;
using DmEletrico.Core.Routing;
using DmEletrico.UI.Routing;
using DmEletrico.UI.Setup;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 3 — Conduit Builder (atalho CB). Abre a janela de configuração
    /// (tipo de conduíte, diâmetro, ângulos, modo de seleção). No modo
    /// "Dispositivos selecionados", o usuário escolhe os dispositivos (ex.: dois) e
    /// só eles são conectados; no modo "Circuitos completos", roteia todos os
    /// circuitos até o QDC.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmConduitBuilderCommand : DmCommandBase
    {
        private static readonly BuiltInCategory[] Terminais =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalEquipment
        };

        /// <summary>Configuração escolhida na 1ª execução, reutilizada na sessão.</summary>
        private static ConduitBuildOptions? _configSessao;

        /// <summary>Redefine a configuração da sessão (usado pelo comando de reconfiguração).</summary>
        internal static void DefinirConfig(ConduitBuildOptions? options) => _configSessao = options;

        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var settings = DmProjectSettings.Read(doc);
            if (!settings.SetupConcluido)
            {
                TaskDialog.Show("DmEletrico", "Execute o Setup antes de construir os conduítes.");
                return Result.Cancelled;
            }

            // 1ª vez na sessão → abre a configuração; depois reaproveita.
            if (_configSessao == null)
            {
                var tipos = new FilteredElementCollector(doc)
                    .OfClass(typeof(ConduitType))
                    .Cast<ConduitType>()
                    .Select(t => new ConduitTypeOption(t.Name, t.Id.Value.ToString()))
                    .ToList();

                if (tipos.Count == 0)
                {
                    TaskDialog.Show("DmEletrico", "Nenhum tipo de conduíte carregado no projeto. Carregue uma família de conduíte e tente de novo.");
                    return Result.Cancelled;
                }

                var vm = new DmConduitBuilderViewModel(tipos).WithDefaults();
                var window = new DmConduitBuilderWindow { DataContext = vm };
                if (window.ShowDialog() != true)
                    return Result.Cancelled;

                _configSessao = vm.ToOptions();
            }

            var options = _configSessao.CloneConfig();

            // Modo "dispositivos selecionados": coleta os dispositivos a conectar.
            if (options.Modo == ModoSelecao.DispositivosSelecionados)
            {
                var ids = DispositivosSelecionados(uiDoc, doc);
                if (ids.Count < 2)
                {
                    var refs = uiDoc.Selection.PickObjects(ObjectType.Element, new TerminalFilter(),
                        "Selecione os dispositivos a conectar (ao menos dois) e pressione Concluir.");
                    ids = refs.Select(r => r.ElementId).ToList();
                }
                if (ids.Count < 2)
                {
                    TaskDialog.Show("DmEletrico", "Selecione ao menos dois dispositivos.");
                    return Result.Cancelled;
                }
                options.Dispositivos = ids;

                // Retorno visual: realça os dispositivos que serão conectados.
                uiDoc.Selection.SetElementIds(ids);
                uiDoc.ShowElements(ids);
            }

            var service = new ConduitBuilderService();
            ConduitBuildReport report;
            using (var tx = new Transaction(doc, "DmEletrico — Construir Conduítes"))
            {
                tx.Start();
                Core.WarningSwallower.Apply(tx);
                report = service.Build(doc, settings, options);
                tx.Commit();
            }

            TaskDialog.Show("DmEletrico — Construir Conduítes", report.ToString());
            return Result.Succeeded;
        }

        private static List<ElementId> DispositivosSelecionados(UIDocument uiDoc, Document doc)
            => uiDoc.Selection.GetElementIds().Where(id => EhTerminal(doc.GetElement(id))).ToList();

        private static bool EhTerminal(Element e)
        {
            var bic = (BuiltInCategory)(e.Category?.Id.Value ?? 0);
            return Terminais.Contains(bic);
        }

        private sealed class TerminalFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => EhTerminal(elem);
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
