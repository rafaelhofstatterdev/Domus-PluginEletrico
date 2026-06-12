using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.Core.Circuits;
using DmEletrico.UI.Circuits;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 3 — cria um circuito (ElectricalSystem) a partir dos dispositivos
    /// selecionados e o atribui a um QDC, com numeração sequencial.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmCreateCircuitCommand : DmCommandBase
    {
        private static readonly BuiltInCategory[] Terminais =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures
        };

        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var dispositivos = DispositivosSelecionados(uiDoc, doc);
            if (dispositivos.Count == 0)
            {
                var refs = uiDoc.Selection.PickObjects(ObjectType.Element, new TerminalFilter(),
                    "Selecione os dispositivos terminais do circuito e pressione Concluir.");
                dispositivos = refs.Select(r => r.ElementId).ToList();
            }
            if (dispositivos.Count == 0)
                return Result.Cancelled;

            var paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Select(p => new PanelOption(p.Name, p.Id))
                .ToList();

            if (paineis.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum QDC (OST_ElectricalEquipment) encontrado no modelo.");
                return Result.Cancelled;
            }

            var picker = new DmPanelPickerWindow(paineis);
            if (picker.ShowDialog() != true || picker.Selecionado == null)
                return Result.Cancelled;

            var painel = (FamilyInstance)doc.GetElement(picker.Selecionado.Id);
            var (numero, nativoInfo) = CircuitService.CreateAndAssign(doc, dispositivos, painel);

            TaskDialog.Show("DmEletrico — Criar Circuito",
                (string.IsNullOrEmpty(numero)
                    ? "Não foi possível criar o circuito."
                    : $"Circuito {numero} criado e atribuído a {painel.Name} ({dispositivos.Count} dispositivo(s)).")
                + "\n\n" + nativoInfo);
            return Result.Succeeded;
        }

        private static List<ElementId> DispositivosSelecionados(UIDocument uiDoc, Document doc)
            => uiDoc.Selection.GetElementIds()
                .Where(id => EhTerminal(doc.GetElement(id)))
                .ToList();

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
