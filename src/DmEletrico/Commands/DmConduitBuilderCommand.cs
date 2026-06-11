using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 3 — Conduit Builder (atalho CB). Lê os ElectricalConnector,
    /// traça o roteamento ortogonal 3D entre dispositivos e quadro, insere
    /// ConduitFitting nas mudanças de direção e dimensiona pela NBR 5410.
    /// </summary>
    public sealed class DmConduitBuilderCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            // TODO: roteamento 3D + ConduitFitting + motor de cálculo (ElectricalCalculator).
            return NotImplementedYet("Construir Conduítes",
                "Traça automaticamente o roteamento 3D de conduítes entre dispositivos e o quadro de origem, dimensionando pela NBR 5410.");
        }
    }
}
