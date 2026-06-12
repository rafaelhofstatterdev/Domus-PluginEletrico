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

            // Conduítes selecionados; se nada selecionado, todos da vista.
            var brutos = uiDoc.Selection.GetElementIds().Where(id => doc.GetElement(id) is Conduit).ToList();
            if (brutos.Count == 0)
                brutos = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Conduit)
                    .WhereElementIsNotElementType()
                    .ToElementIds().ToList();

            // Só os conduítes que possuem circuito (criados pelo Conduit Builder).
            var ids = brutos.Where(id =>
                !string.IsNullOrWhiteSpace(doc.GetElement(id)?.LookupParameter(DmParameters.CircuitoOrigemId)?.AsString()))
                .ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum conduíte com circuito encontrado. Atribua cargas, crie os circuitos e construa os conduítes antes da fiação.");
                return Result.Cancelled;
            }

            // Analisa a topologia (árvore no QD) e grava, em cada conduíte, os
            // condutores de TODOS os circuitos que passam por ele (carga a jusante).
            var topo = WiringTopology.Analisar(doc, cfg);

            // Anota, ocultando as bitolas configuradas como ocultas.
            bool Incluir(Element c)
            {
                var secao = c.LookupParameter(DmParameters.SecaoAdotada)?.AsDouble() ?? 0;
                return secao <= 0 || !cfg.Oculta(secao);
            }

            var report = TagService.AnotarFiacao(doc, view, ids, Incluir, cfg.FamiliaCondutores());
            TaskDialog.Show("DmEletrico — Fiação Automática", report + "\n\n" + topo);
            return Result.Succeeded;
        }
    }
}
