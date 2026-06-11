using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 10 — Diagramas Unifilares Dinâmicos. Gera um ViewDrafting por QDC
    /// (disjuntor geral, barramentos, disjuntores de circuito, bitolas, destinos)
    /// vinculado parametricamente ao modelo.
    /// </summary>
    public sealed class DmUnifilarCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Diagrama Unifilar",
                "Gera o diagrama unifilar dinâmico (ViewDrafting) de cada QDC, atualizado automaticamente com o modelo.");
        }
    }
}
