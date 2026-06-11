using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.Core;
using DmEletrico.Core.Calculation;
using DmEletrico.UI.Load;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 6 — Atribuição de carga. Configura potência, polos, tensão e tipo
    /// nos dispositivos elétricos selecionados (ou escolhidos), gravando os
    /// parâmetros Dm_ correspondentes.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmCircuitLoadCommand : DmCommandBase
    {
        private static readonly BuiltInCategory[] Categorias =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalEquipment
        };

        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var dispositivos = DispositivosSelecionados(uiDoc, doc);
            if (dispositivos.Count == 0)
                dispositivos = DispositivosEscolhidos(uiDoc, doc);
            if (dispositivos.Count == 0)
                return Result.Cancelled;

            var vm = new DmCircuitLoadViewModel(dispositivos.Count);
            var window = new DmCircuitLoadWindow { DataContext = vm };
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            using (var tx = new Transaction(doc, "DmEletrico — Atribuir Carga"))
            {
                tx.Start();
                var corrente = vm.TensaoOperacao > 0 ? vm.Potencia / vm.TensaoOperacao : 0;
                var disjuntor = Nbr5410Tables.DisjuntorComercial(corrente);

                foreach (var e in dispositivos)
                {
                    e.LookupParameter(DmParameters.Potencia)?.Set(vm.Potencia);
                    e.LookupParameter(DmParameters.NumeroPolos)?.Set(vm.NumeroPolos);
                    e.LookupParameter(DmParameters.TensaoOperacao)?.Set(vm.TensaoOperacao);
                    e.LookupParameter(DmParameters.TipoCircuito)?.Set(vm.TipoCircuito);
                    e.LookupParameter(DmParameters.Disjuntor)?.Set(disjuntor);
                }
                tx.Commit();
            }

            TaskDialog.Show("DmEletrico — Atribuir Carga",
                $"Carga aplicada a {dispositivos.Count} dispositivo(s).");
            return Result.Succeeded;
        }

        private static List<Element> DispositivosSelecionados(UIDocument uiDoc, Document doc)
            => uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(EhEletrico)
                .ToList();

        private static List<Element> DispositivosEscolhidos(UIDocument uiDoc, Document doc)
        {
            var refs = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                new ElectricalFilter(),
                "Selecione os dispositivos elétricos e pressione Concluir.");
            return refs.Select(r => doc.GetElement(r)).ToList();
        }

        private static bool EhEletrico(Element e)
        {
            var bic = (BuiltInCategory)(e.Category?.Id.Value ?? 0);
            return Categorias.Contains(bic);
        }

        private sealed class ElectricalFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => EhEletrico(elem);
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
