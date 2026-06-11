using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Annotation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 7 — Auto TAG. Insere TAGs de fiação em todos os conduítes da
    /// vista ativa que ainda não estejam etiquetados.
    /// </summary>
    public sealed class DmAutoTagCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var report = TagService.AutoTag(doc, uiDoc.ActiveView);
            TaskDialog.Show("DmEletrico — Auto TAG", report.ToString());
            return Result.Succeeded;
        }
    }
}
