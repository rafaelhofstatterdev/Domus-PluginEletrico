using System.Collections.Generic;
using System.Linq;
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
            public List<ViewSchedule> Schedules { get; } = new();
            public List<string> CamposNaoEncontrados { get; } = new();
        }

        public sealed class QuantitativosResult
        {
            public ViewSchedule Conduites { get; set; } = null!;
            public ViewSchedule Dispositivos { get; set; } = null!;
            public List<string> CamposNaoEncontrados { get; } = new();
        }

        /// <summary>
        /// Gera um quadro de cargas (ViewSchedule) por QDC, filtrado por Dm_Quadro,
        /// incluindo a coluna de disjuntor. Caso nenhum QDC esteja nomeado nos
        /// dispositivos, gera um quadro único consolidado.
        /// </summary>
        public static QuadroCargasResult GerarQuadroDeCargas(Document doc)
        {
            var campos = new[]
            {
                DmParameters.NumeroCircuito,
                DmParameters.TipoCircuito,
                DmParameters.Potencia,
                DmParameters.TensaoOperacao,
                DmParameters.Disjuntor,
                DmParameters.Fase,
                DmParameters.NumeroPolos
            };

            var quadros = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Select(p => p.Name)
                .Distinct()
                .ToList();

            var result = new QuadroCargasResult();

            using var tx = new Transaction(doc, "DmEletrico — Quadros de Cargas");
            tx.Start();

            if (quadros.Count == 0)
            {
                var r = ScheduleBuilder.Create(doc, BuiltInCategory.OST_ElectricalFixtures,
                    "DmEletrico - Quadro de Cargas", campos, groupByFieldName: DmParameters.NumeroCircuito);
                result.Schedules.Add(r.Schedule);
                result.CamposNaoEncontrados.AddRange(r.CamposNaoEncontrados);
            }
            else
            {
                foreach (var nome in quadros)
                {
                    var r = ScheduleBuilder.Create(doc, BuiltInCategory.OST_ElectricalFixtures,
                        $"DmEletrico - Quadro de Cargas {nome}", campos,
                        groupByFieldName: DmParameters.NumeroCircuito,
                        filterFieldName: DmParameters.Quadro, filterValue: nome);
                    result.Schedules.Add(r.Schedule);
                    foreach (var c in r.CamposNaoEncontrados)
                        if (!result.CamposNaoEncontrados.Contains(c)) result.CamposNaoEncontrados.Add(c);
                }
            }

            tx.Commit();
            return result;
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
