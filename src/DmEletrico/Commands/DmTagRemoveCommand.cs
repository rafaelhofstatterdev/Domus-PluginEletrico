using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.Core.Annotation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 5 — remoção de TAG. O usuário escolhe uma TAG e ela é removida,
    /// mantendo o vínculo dos demais elementos.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmTagRemoveCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var refTag = uiDoc.Selection.PickObject(
                ObjectType.Element, new TagFilter(), "Selecione a TAG a remover.");
            if (refTag == null) return Result.Cancelled;

            TagService.Remove(doc, refTag.ElementId);
            return Result.Succeeded;
        }

        private sealed class TagFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is IndependentTag;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
