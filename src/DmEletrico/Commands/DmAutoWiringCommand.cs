using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Annotation;
using DmEletrico.Core.Wiring;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Passo 1 — Fiação Automática. Aplica a simbologia de fiação aos conduítes
    /// SELECIONADOS (ou a todos os da vista, se nada selecionado). Funciona em
    /// planta e em 3D. Re-clicar realinha as anotações. Respeita a configuração
    /// (neutro/terra e ocultar bitolas).
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmAutoWiringCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var view = uiDoc.ActiveView;
            var cfg = DmWiringSettings.Read(doc);

            var ids = uiDoc.Selection.GetElementIds().Where(id => doc.GetElement(id) is Conduit).ToList();
            if (ids.Count == 0)
                ids = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Conduit)
                    .WhereElementIsNotElementType()
                    .ToElementIds().ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum conduíte selecionado nem visível nesta vista.");
                return Result.Cancelled;
            }

            // Recalcula os condutores conforme a configuração (regras de neutro/terra).
            WiringService.AplicarFiacao(doc, ids, cfg);

            // Anota, ocultando as bitolas configuradas como ocultas.
            bool Incluir(Element c)
            {
                var secao = c.LookupParameter(DmParameters.SecaoAdotada)?.AsDouble() ?? 0;
                return secao <= 0 || !cfg.Oculta(secao);
            }

            var report = TagService.AnotarFiacao(doc, view, ids, Incluir);
            TaskDialog.Show("DmEletrico — Fiação Automática", report.ToString());
            return Result.Succeeded;
        }
    }
}
