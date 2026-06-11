using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 13 — Extração de Quantitativos. Gera um ViewSchedule de materiais:
    /// conduítes (m), condutores (m), dispositivos por categoria/família e quadros
    /// com especificação de disjuntores.
    /// </summary>
    public sealed class DmMaterialsCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Quantitativos",
                "Gera o quadro de materiais: conduítes por diâmetro, condutores por seção, dispositivos e quadros.");
        }
    }
}
