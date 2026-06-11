using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Coordination;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 11 — Coordenação entre modelos. Varre os RevitLinkInstance e
    /// reporta os elementos elétricos encontrados nos modelos vinculados, com as
    /// coordenadas transformadas para o hospedeiro. Base para importação de
    /// circuitos/cargas e roteamento entre modelos federados (roadmap).
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmLinkCoordinationCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var report = new LinkCoordinationService().ReadElectrical(doc);
            TaskDialog.Show("DmEletrico — Coordenação entre Modelos", report.ToString());
            return Result.Succeeded;
        }
    }
}
