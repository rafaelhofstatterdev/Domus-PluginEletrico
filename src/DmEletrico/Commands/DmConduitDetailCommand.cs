using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.UI.Detail;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 5 (UI) — Detalhamento do trecho. Abre a janela WPF com os
    /// parâmetros calculados de um conduíte. Usa o elemento já selecionado ou,
    /// se não houver, pede para o usuário escolher um conduíte.
    ///
    /// Observação: a Revit API pública não permite injetar itens no menu de
    /// contexto (clique direito) de elementos; este comando é o equivalente
    /// suportado (selecionar o conduíte e acionar o detalhamento).
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmConduitDetailCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var conduit = ConduiteSelecionado(uiDoc, doc) ?? ConduitePorEscolha(uiDoc, doc);
            if (conduit == null)
                return Result.Cancelled;

            var vm = new DmConduitDetailViewModel(conduit);
            new DmConduitDetailWindow { DataContext = vm }.ShowDialog();
            return Result.Succeeded;
        }

        private static Element? ConduiteSelecionado(UIDocument uiDoc, Document doc)
        {
            var ids = uiDoc.Selection.GetElementIds();
            return ids
                .Select(id => doc.GetElement(id))
                .FirstOrDefault(e => e is Conduit);
        }

        private static Element? ConduitePorEscolha(UIDocument uiDoc, Document doc)
        {
            var refElem = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new ConduitSelectionFilter(),
                "Selecione um trecho de conduíte para detalhar.");
            return refElem == null ? null : doc.GetElement(refElem);
        }

        private sealed class ConduitSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Conduit;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
