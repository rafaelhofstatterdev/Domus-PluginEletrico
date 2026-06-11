using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 7 — Auto TAG. Insere famílias de anotação de fiação nos trechos
    /// de conduíte (número do circuito, seção em mm², traços fase/neutro/terra) e
    /// atualiza quando o dimensionamento muda.
    /// </summary>
    public sealed class DmAutoTagCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Auto TAG",
                "Insere automaticamente TAGs de fiação nos trechos de conduíte com número do circuito, seção e representação de traços.");
        }
    }
}
