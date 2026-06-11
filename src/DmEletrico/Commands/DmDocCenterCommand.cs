using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 8 — Central de Documentação (atalho DC). Painel WPF central que
    /// lista QDCs e circuitos, gerencia vistas/tabelas/pranchas e aciona a geração
    /// de quadros de cargas e diagramas unifilares.
    /// </summary>
    public sealed class DmDocCenterCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            return NotImplementedYet("Central de Documentação",
                "Painel central de QDCs, circuitos, vistas, quadros de cargas e diagramas unifilares.");
        }
    }
}
