using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Documentation
{
    /// <summary>
    /// Geração de pranchas (ViewSheet) da Central de Documentação (requisito 8).
    /// Cria uma prancha por QDC e posiciona os quadros de cargas e o diagrama
    /// unifilar correspondentes (casados pelo nome do QDC). Gerencia a própria
    /// transação.
    /// </summary>
    public static class SheetService
    {
        public sealed class SheetsResult
        {
            public List<ViewSheet> Pranchas { get; } = new();
            public string? Aviso { get; set; }
        }

        public static SheetsResult GerarPranchas(Document doc)
        {
            var result = new SheetsResult();

            var paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Select(p => p.Name)
                .Distinct()
                .ToList();

            if (paineis.Count == 0)
            {
                result.Aviso = "Nenhum QDC encontrado para gerar pranchas.";
                return result;
            }

            var titleBlockId = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .FirstElementId();
            if (titleBlockId == ElementId.InvalidElementId)
                titleBlockId = ElementId.InvalidElementId; // prancha sem carimbo

            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                .Where(v => v.Name.StartsWith("DmEletrico")).ToList();

            var draftings = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>()
                .Where(v => v.Name.StartsWith("DmEletrico")).ToList();

            using var tx = new Transaction(doc, "DmEletrico — Gerar Pranchas");
            tx.Start();

            foreach (var nome in paineis)
            {
                var sheet = ViewSheet.Create(doc, titleBlockId);
                sheet.Name = $"DmEletrico - {nome}";

                double y = 1.2;
                foreach (var sch in schedules.Where(s => s.Name.Contains(nome)))
                {
                    ScheduleSheetInstance.Create(doc, sheet.Id, sch.Id, new XYZ(0.2, y, 0));
                    y -= 0.5;
                }

                foreach (var dv in draftings.Where(d => d.Name.Contains(nome)))
                {
                    if (Viewport.CanAddViewToSheet(doc, sheet.Id, dv.Id))
                        Viewport.Create(doc, sheet.Id, dv.Id, new XYZ(1.2, 0.8, 0));
                }

                result.Pranchas.Add(sheet);
            }

            tx.Commit();
            return result;
        }
    }
}
