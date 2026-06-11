using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.Core.Annotation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 7 — Manual TAG (atalho MT). O usuário escolhe um conduíte e a
    /// TAG é inserida no ponto clicado, mantendo o vínculo paramétrico.
    /// </summary>
    public sealed class DmManualTagCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var refConduit = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new ConduitSelectionFilter(),
                "Selecione o trecho de conduíte para etiquetar.");

            var ponto = refConduit.GlobalPoint ?? ((doc.GetElement(refConduit).Location as LocationCurve)?.Curve.Evaluate(0.5, true));
            if (ponto == null)
                return Result.Cancelled;

            var ok = TagService.ManualTag(doc, uiDoc.ActiveView, refConduit, ponto);
            if (!ok)
                TaskDialog.Show("DmEletrico — Manual TAG",
                    "Não foi possível inserir a TAG. Verifique se há família de TAG de conduíte carregada e se a vista suporta anotações.");

            return Result.Succeeded;
        }

        private sealed class ConduitSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Conduit;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
