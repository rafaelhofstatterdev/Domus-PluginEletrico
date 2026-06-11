using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 4 — Route Fit (atalho RF). Detecta conduítes com geometria
    /// inválida após movimentação de dispositivos e recalcula os trechos afetados,
    /// restaurando a continuidade da rede (conduítes, eletrocalhas, perfilados).
    /// </summary>
    public sealed class DmRouteFitCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Ajuste de Rotas",
                "Detecta trechos com geometria inválida (comprimento zero, ângulos não suportados, conexões rompidas) e recalcula a rede física.");
        }
    }
}
