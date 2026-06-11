using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core.Documentation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 9 — Quadros de Cargas. Gera um ViewSchedule dos dispositivos
    /// agrupados por circuito (Dm_NumeroCircuito), com potência, tipo, tensão,
    /// fase e número de polos. Colunas baseadas em parâmetros Dm_ (nomes estáveis,
    /// independentes do idioma do Revit).
    /// </summary>
    public sealed class DmLoadScheduleCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var result = DocumentationService.GerarQuadroDeCargas(doc);
            uiDoc.ActiveView = result.Schedule;

            var msg = $"Quadro de cargas '{result.Schedule.Name}' criado.";
            if (result.CamposNaoEncontrados.Count > 0)
                msg += "\n\nColunas não disponíveis (rode o Setup para injetar os parâmetros):\n"
                       + string.Join(", ", result.CamposNaoEncontrados);

            TaskDialog.Show("DmEletrico — Quadro de Cargas", msg);
            return Result.Succeeded;
        }
    }
}
