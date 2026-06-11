using System.Collections.Generic;
using Autodesk.Revit.DB;
using DmEletrico.Core;

namespace DmEletrico.Core.Documentation
{
    /// <summary>
    /// Serviço de geração de documentação (quadros de cargas e quantitativos),
    /// reutilizado pelos comandos diretos e pela Central de Documentação.
    /// Cada método gerencia sua própria transação.
    /// </summary>
    public static class DocumentationService
    {
        public sealed class QuadroCargasResult
        {
            public ViewSchedule Schedule { get; set; } = null!;
            public List<string> CamposNaoEncontrados { get; set; } = new();
        }

        public sealed class QuantitativosResult
        {
            public ViewSchedule Conduites { get; set; } = null!;
            public ViewSchedule Dispositivos { get; set; } = null!;
            public List<string> CamposNaoEncontrados { get; } = new();
        }

        public static QuadroCargasResult GerarQuadroDeCargas(Document doc)
        {
            using var tx = new Transaction(doc, "DmEletrico — Quadro de Cargas");
            tx.Start();

            var r = ScheduleBuilder.Create(
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

            return new QuadroCargasResult
            {
                Schedule = r.Schedule,
                CamposNaoEncontrados = r.CamposNaoEncontrados
            };
        }

        public static QuantitativosResult GerarQuantitativos(Document doc)
        {
            using var tx = new Transaction(doc, "DmEletrico — Quantitativos");
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

            tx.Commit();

            var result = new QuantitativosResult
            {
                Conduites = conduites.Schedule,
                Dispositivos = dispositivos.Schedule
            };
            result.CamposNaoEncontrados.AddRange(conduites.CamposNaoEncontrados);
            result.CamposNaoEncontrados.AddRange(dispositivos.CamposNaoEncontrados);
            return result;
        }
    }
}
