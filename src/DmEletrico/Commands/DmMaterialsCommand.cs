using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Documentation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 13 — Extração de Quantitativos. Gera ViewSchedules de materiais:
    ///   - Conduítes: agrupados por diâmetro nominal, com seção e comprimento;
    ///   - Dispositivos: por circuito, com potência e tipo.
    /// Colunas baseadas em parâmetros Dm_ (nomes estáveis em qualquer idioma).
    /// </summary>
    public sealed class DmMaterialsCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var naoEncontrados = new List<string>();
            ViewSchedule? primeiro = null;

            using (var tx = new Transaction(doc, "DmEletrico — Quantitativos"))
            {
                tx.Start();

                var conduites = ScheduleBuilder.Create(
                    doc,
                    BuiltInCategory.OST_Conduit,
                    "DmEletrico - Quantitativo de Conduítes",
                    new[]
                    {
                        DmParameters.DiametroNominal,
                        DmParameters.SecaoAdotada,
                        DmParameters.Comprimento
                    },
                    groupByFieldName: DmParameters.DiametroNominal);
                naoEncontrados.AddRange(conduites.CamposNaoEncontrados);
                primeiro = conduites.Schedule;

                var dispositivos = ScheduleBuilder.Create(
                    doc,
                    BuiltInCategory.OST_ElectricalFixtures,
                    "DmEletrico - Quantitativo de Dispositivos",
                    new[]
                    {
                        DmParameters.NumeroCircuito,
                        DmParameters.TipoCircuito,
                        DmParameters.Potencia
                    },
                    groupByFieldName: DmParameters.TipoCircuito);
                naoEncontrados.AddRange(dispositivos.CamposNaoEncontrados);

                tx.Commit();
            }

            if (primeiro != null)
                uiDoc.ActiveView = primeiro;

            var msg = "Quantitativos gerados: Conduítes e Dispositivos.";
            if (naoEncontrados.Count > 0)
                msg += "\n\nColunas não disponíveis (rode o Setup):\n" + string.Join(", ", naoEncontrados);

            TaskDialog.Show("DmEletrico — Quantitativos", msg);
            return Result.Succeeded;
        }
    }
}
