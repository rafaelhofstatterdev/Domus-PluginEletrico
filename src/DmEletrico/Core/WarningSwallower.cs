using Autodesk.Revit.DB;

namespace DmEletrico.Core
{
    /// <summary>
    /// Pré-processador de falhas que descarta avisos (warnings) durante a
    /// transação — evita os diálogos "Erro pode ser ignorado" ao criar conduítes
    /// e conexões. Erros de verdade continuam sendo tratados pelo Revit.
    /// </summary>
    public sealed class WarningSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            failuresAccessor.DeleteAllWarnings();
            return FailureProcessingResult.Continue;
        }

        /// <summary>Aplica o supressor a uma transação aberta.</summary>
        public static void Apply(Transaction tx)
        {
            var options = tx.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new WarningSwallower());
            options.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(options);
        }
    }
}
