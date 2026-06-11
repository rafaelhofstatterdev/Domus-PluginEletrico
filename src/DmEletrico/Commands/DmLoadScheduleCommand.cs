using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Documentation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 9 — Quadros de Cargas. Gera um ViewSchedule dos dispositivos
    /// agrupados por circuito (Dm_NumeroCircuito), com potência, tipo, tensão,
    /// fase e número de polos. As colunas usam parâmetros Dm_ (nomes estáveis),
    /// portanto independem do idioma do Revit.
    ///
    /// Múltiplos quadros por pavimento/zona são obtidos pelo agrupamento por
    /// circuito; o dimensionamento de disjuntor será adicionado em iteração futura.
    /// </summary>
    public sealed class DmLoadScheduleCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            ScheduleBuilder.Result result;

            using (var tx = new Transaction(doc, "DmEletrico — Quadro de Cargas"))
            {
                tx.Start();
                result = ScheduleBuilder.Create(
                    doc,
                    BuiltInCategory.OST_ElectricalFixtures,
                    "DmEletrico - Quadro de Cargas",
                    new[]
                    {
                        DmParameters.NumeroCircuito,
                        DmParameters.TipoCircuito,
                        DmParameters.Potencia,
                        DmParameters.TensaoOperacao,
                        DmParameters.Fase,
                        DmParameters.NumeroPolos
                    },
                    groupByFieldName: DmParameters.NumeroCircuito);
                tx.Commit();
            }

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
