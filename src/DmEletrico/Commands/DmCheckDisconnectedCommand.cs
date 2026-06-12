using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DmEletrico.Core;
using DmEletrico.Core.Circuits;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 6 — Rastreador de Dispositivos Desconectados. Identifica
    /// dispositivos sem número de circuito (Dm_NumeroCircuito) e circuitos lógicos
    /// sem QDC atribuído (Dm_Quadro vazio).
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmCheckDisconnectedCommand : DmCommandBase
    {
        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var dispositivos = LogicalCircuits.DispositivosEletricos(doc);

            var semCircuito = dispositivos
                .Where(e => string.IsNullOrWhiteSpace(e.LookupParameter(DmParameters.NumeroCircuito)?.AsString()))
                .ToList();

            var circuitosSemQdc = LogicalCircuits.All(doc)
                .Where(c => string.IsNullOrEmpty(c.Quadro) || c.Painel == null)
                .ToList();

            if (semCircuito.Count == 0 && circuitosSemQdc.Count == 0)
            {
                TaskDialog.Show("DmEletrico", "Nenhum dispositivo desconectado nem circuito sem QDC.");
                return Result.Succeeded;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Dispositivos SEM circuito: {semCircuito.Count}");
            foreach (var fi in semCircuito.Take(40))
            {
                var p = (fi.Location as LocationPoint)?.Point;
                var loc = p != null ? $"({p.X:F1}, {p.Y:F1}, {p.Z:F1})" : "—";
                sb.AppendLine($"• [{fi.Id}] {fi.Category?.Name} — {fi.Name} {loc}");
            }
            if (semCircuito.Count > 40) sb.AppendLine($"… e mais {semCircuito.Count - 40}.");

            sb.AppendLine($"\nCircuitos SEM QDC atribuído: {circuitosSemQdc.Count}");
            foreach (var c in circuitosSemQdc.Take(40))
                sb.AppendLine($"• Circuito {c.Numero} — {c.Dispositivos.Count} dispositivo(s)");

            TaskDialog.Show("DmEletrico — Desconectados", sb.ToString());
            return Result.Succeeded;
        }
    }
}
