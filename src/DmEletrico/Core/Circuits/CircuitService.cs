using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Circuits
{
    /// <summary>
    /// Gestão de circuitos do DmEletrico. As famílias usadas (padrão OFElétrico)
    /// têm conectores de CONDUÍTE (DomainCableTrayConduit), não conectores
    /// elétricos de força — portanto o circuito é LÓGICO: agrupa dispositivos por
    /// Dm_Quadro (QDC) + Dm_NumeroCircuito, sem usar o ElectricalSystem nativo.
    /// Cada método gerencia a própria transação.
    /// </summary>
    public static class CircuitService
    {
        /// <summary>
        /// Cria/atribui um circuito lógico aos dispositivos: grava Dm_Quadro (nome do
        /// QDC) e Dm_NumeroCircuito (sequencial por QDC). Retorna o número atribuído.
        /// </summary>
        public static string CreateAndAssign(Document doc, ICollection<ElementId> deviceIds, FamilyInstance panel)
        {
            if (deviceIds.Count == 0) return "";

            var numero = NextNumero(doc, panel.Name).ToString();

            using var tx = new Transaction(doc, "DmEletrico — Criar Circuito");
            tx.Start();
            foreach (var id in deviceIds)
            {
                var e = doc.GetElement(id);
                if (e == null) continue;
                e.LookupParameter(DmParameters.NumeroCircuito)?.Set(numero);
                e.LookupParameter(DmParameters.Quadro)?.Set(panel.Name);

                // Propaga para os parâmetros nativos da família (aparece no Revit).
                Core.ParamPropagation.SetTexto(e, numero, new[] { "circuito" }, new[] { "tipo", "dm_" });
                Core.ParamPropagation.SetTexto(e, panel.Name, new[] { "quadro" }, new string[0]);
                Core.ParamPropagation.SetTexto(e, panel.Name, new[] { "painel" }, new string[0]);
            }
            tx.Commit();
            return numero;
        }

        /// <summary>Reatribui os dispositivos de um circuito a outro QDC.</summary>
        public static void Reassign(Document doc, IEnumerable<ElementId> deviceIds, FamilyInstance novoPainel)
        {
            using var tx = new Transaction(doc, "DmEletrico — Reatribuir Circuito");
            tx.Start();
            foreach (var id in deviceIds)
                doc.GetElement(id)?.LookupParameter(DmParameters.Quadro)?.Set(novoPainel.Name);
            tx.Commit();
        }

        /// <summary>Renumera os dispositivos de um circuito.</summary>
        public static void SetNumero(Document doc, IEnumerable<ElementId> deviceIds, string numero)
        {
            using var tx = new Transaction(doc, "DmEletrico — Renumerar Circuito");
            tx.Start();
            foreach (var id in deviceIds)
                doc.GetElement(id)?.LookupParameter(DmParameters.NumeroCircuito)?.Set(numero);
            tx.Commit();
        }

        /// <summary>Próximo número de circuito livre no QDC.</summary>
        public static int NextNumero(Document doc, string quadro)
        {
            var usados = LogicalCircuits.DispositivosEletricos(doc)
                .Where(e => (e.LookupParameter(DmParameters.Quadro)?.AsString() ?? "") == quadro)
                .Select(e => int.TryParse(e.LookupParameter(DmParameters.NumeroCircuito)?.AsString(), out var n) ? n : 0);
            return usados.DefaultIfEmpty(0).Max() + 1;
        }
    }
}
