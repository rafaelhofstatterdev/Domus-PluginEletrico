using System;
using Autodesk.Revit.UI;

namespace DmEletrico.Application
{
    /// <summary>
    /// Ponto de entrada do suplemento. Registrado no DmEletrico.addin como
    /// Type="Application". Constrói a aba "DmEletrico" na Ribbon ao iniciar o Revit.
    /// </summary>
    public sealed class DmApplication : IExternalApplication
    {
        public const string TabName = "DmEletrico";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                RibbonBuilder.Build(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("DmEletrico", "Falha ao inicializar o suplemento:\n" + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
