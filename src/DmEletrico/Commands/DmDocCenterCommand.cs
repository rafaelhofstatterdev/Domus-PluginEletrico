using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.UI.DocCenter;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 8 — Central de Documentação (atalho DC). Painel WPF que lista os
    /// QDCs e seus circuitos e dispara a geração de quadros de cargas,
    /// quantitativos e diagramas unifilares.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmDocCenterCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var vm = new DmDocCenterViewModel(doc);
            var window = new DmDocCenterWindow(doc) { DataContext = vm };
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
