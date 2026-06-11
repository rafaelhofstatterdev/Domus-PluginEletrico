using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 9 — Quadros de Cargas. Gera um ViewSchedule por QDC com circuito,
    /// descrição, disjuntor, corrente, potência, fase e tipo (Iluminação/TUG/TUE).
    /// </summary>
    public sealed class DmLoadScheduleCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Quadros de Cargas",
                "Gera o quadro de cargas (ViewSchedule) de cada QDC, com atualização paramétrica automática.");
        }
    }
}
