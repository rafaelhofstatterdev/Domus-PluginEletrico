using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Documentation;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 10 — Diagrama Unifilar Dinâmico. Gera um ViewDrafting por QDC
    /// com barramento, disjuntor geral e ramais (número, bitola, disjuntor,
    /// destino). O dimensionamento dos ramais usa o motor de cálculo, de modo que
    /// o diagrama reflete as cargas atribuídas.
    /// </summary>
    public sealed class DmUnifilarCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var settings = DmProjectSettings.Read(doc);
            var report = new UnifilarService().Generate(doc, settings);

            if (report.Vistas.Count > 0)
                uiDoc.ActiveView = report.Vistas.First();

            TaskDialog.Show("DmEletrico — Diagrama Unifilar", report.ToString());
            return Result.Succeeded;
        }
    }
}
