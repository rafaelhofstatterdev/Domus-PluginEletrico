using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Documentation
{
    /// <summary>
    /// Helper para criar ViewSchedules de forma robusta. As colunas são casadas
    /// pelo nome exibido do campo agendável; como os parâmetros do DmEletrico usam
    /// nomes fixos com prefixo Dm_ (independentes do idioma do Revit), o casamento
    /// é estável em qualquer instalação. Campos não encontrados são ignorados.
    ///
    /// Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public static class ScheduleBuilder
    {
        public sealed class Result
        {
            public ViewSchedule Schedule { get; }
            public List<string> CamposNaoEncontrados { get; } = new();
            public Result(ViewSchedule schedule) { Schedule = schedule; }
        }

        public static Result Create(
            Document doc,
            BuiltInCategory category,
            string name,
            IEnumerable<string> fieldNames,
            string? groupByFieldName = null,
            string? filterFieldName = null,
            string? filterValue = null)
        {
            var categoryId = Category.GetCategory(doc, category).Id;
            var schedule = ViewSchedule.CreateSchedule(doc, categoryId);
            schedule.Name = UniqueName(doc, name);

            var def = schedule.Definition;
            var schedulable = def.GetSchedulableFields();
            var added = new Dictionary<string, ScheduleField>();
            var result = new Result(schedule);

            foreach (var wanted in fieldNames)
            {
                var sf = schedulable.FirstOrDefault(s => s.GetName(doc) == wanted);
                if (sf == null)
                {
                    result.CamposNaoEncontrados.Add(wanted);
                    continue;
                }
                added[wanted] = def.AddField(sf);
            }

            if (groupByFieldName != null && added.TryGetValue(groupByFieldName, out var groupField))
            {
                var sgf = new ScheduleSortGroupField(groupField.FieldId) { ShowHeader = true };
                def.AddSortGroupField(sgf);
            }

            if (filterFieldName != null && filterValue != null &&
                added.TryGetValue(filterFieldName, out var filterField))
            {
                try { def.AddFilter(new ScheduleFilter(filterField.FieldId, ScheduleFilterType.Equal, filterValue)); }
                catch { /* tipo de campo não filtrável por igualdade textual */ }
            }

            return result;
        }

        private static string UniqueName(Document doc, string baseName)
        {
            var existentes = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Select(v => v.Name)
                .ToHashSet();

            if (!existentes.Contains(baseName)) return baseName;

            for (int i = 2; ; i++)
            {
                var candidato = $"{baseName} ({i})";
                if (!existentes.Contains(candidato)) return candidato;
            }
        }
    }
}
