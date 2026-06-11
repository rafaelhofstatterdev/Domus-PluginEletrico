using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Base para todos os comandos do DmEletrico. Centraliza o tratamento de
    /// exceções e o acesso ao documento ativo. Marcada como Manual: cada comando
    /// gerencia suas próprias transações.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public abstract class DmCommandBase : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                message = "Nenhum documento ativo.";
                return Result.Failed;
            }

            try
            {
                return Run(commandData, uiDoc, uiDoc.Document);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("DmEletrico", $"Erro em {GetType().Name}:\n{ex}");
                return Result.Failed;
            }
        }

        protected abstract Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc);

        /// <summary>Placeholder padrão para módulos ainda não implementados.</summary>
        protected Result NotImplementedYet(string titulo, string descricao)
        {
            TaskDialog.Show("DmEletrico — " + titulo,
                descricao + "\n\n(Módulo em desenvolvimento — esqueleto registrado.)");
            return Result.Succeeded;
        }
    }
}
