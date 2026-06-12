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

            if (brutos.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum conduíte selecionado nem visível nesta vista.");
                return Result.Cancelled;
            }

            // Analisa a topologia (árvore no QD) e grava, em cada conduíte, os
            // condutores de TODOS os circuitos que passam por ele (carga a jusante).
            // É a análise — não o parâmetro CircuitoOrigemId — que define os circuitos.
            var topo = WiringTopology.Analisar(doc, cfg);

            // Anota só os conduítes que, após a análise, têm circuito passando.
            var ids = brutos.Where(id =>
                !string.IsNullOrWhiteSpace(doc.GetElement(id)?.LookupParameter(DmParameters.CircuitosNoTrecho)?.AsString()))
                .ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum circuito identificado nos conduítes.\n\n" +
                    "Verifique: (1) o Setup foi rodado (parâmetros Dm_NoA/NoB), (2) os conduítes foram reconstruídos depois disso, " +
                    "(3) há um QD conectado e dispositivos com circuito (Dm_NumeroCircuito).\n\n" + topo);
                return Result.Cancelled;
            }

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
