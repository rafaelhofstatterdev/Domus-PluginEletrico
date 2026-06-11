using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 7 — Manual TAG (atalho MT). Seleção pontual de trechos para
    /// inserir ou editar TAGs, mantendo o vínculo paramétrico com o circuito.
    /// </summary>
    public sealed class DmManualTagCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Manual TAG",
                "Seleção pontual de trechos de conduíte para inserir ou editar TAGs vinculadas ao circuito.");
        }
    }
}
