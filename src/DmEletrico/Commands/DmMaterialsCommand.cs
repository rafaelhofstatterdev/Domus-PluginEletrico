using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Documentation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 13 — Extração de Quantitativos. Gera ViewSchedules de materiais:
    /// conduítes (por diâmetro, com seção e comprimento) e dispositivos (por tipo).
    /// </summary>
    public sealed class DmMaterialsCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var result = DocumentationService.GerarQuantitativos(doc);
            uiDoc.ActiveView = result.Conduites;

            var msg = "Quantitativos gerados: Conduítes e Dispositivos.";
            if (result.CamposNaoEncontrados.Count > 0)
                msg += "\n\nColunas não disponíveis (rode o Setup):\n"
                       + string.Join(", ", result.CamposNaoEncontrados);

            TaskDialog.Show("DmEletrico — Quantitativos", msg);
            return Result.Succeeded;
        }
    }
}
