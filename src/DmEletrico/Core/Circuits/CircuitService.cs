using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace DmEletrico.Core.Circuits
{
    /// <summary>
    /// Requisito 3 (gestão lógica) — criação e gestão de circuitos
    /// (ElectricalSystem): atribuição a QDC, numeração sequencial e reorganização.
    /// Cada método gerencia a própria transação.
    /// </summary>
    public static class CircuitService
    {
        /// <summary>
        /// Cria um circuito de força a partir dos dispositivos e o atribui ao QDC.
        /// O Revit numera o circuito automaticamente (sequencial inteligente); o
        /// número é replicado em Dm_NumeroCircuito para as tabelas do DmEletrico.
        /// </summary>
        public static ElectricalSystem? CreateAndAssign(Document doc, ICollection<ElementId> deviceIds, FamilyInstance panel)
        {
            if (deviceIds.Count == 0) return null;

            using var tx = new Transaction(doc, "DmEletrico — Criar Circuito");
            tx.Start();

            var system = ElectricalSystem.Create(doc, deviceIds.ToList(), ElectricalSystemType.PowerCircuit);
            system.SelectPanel(panel);
            doc.Regenerate();

            EscreverNumero(doc, system, system.CircuitNumber);
            tx.Commit();
            return system;
        }

        /// <summary>Reatribui um circuito a outro QDC.</summary>
        public static void Reassign(Document doc, ElectricalSystem system, FamilyInstance novoPainel)
        {
            using var tx = new Transaction(doc, "DmEletrico — Reatribuir Circuito");
            tx.Start();
            system.SelectPanel(novoPainel);
            doc.Regenerate();
            EscreverNumero(doc, system, system.CircuitNumber);
            tx.Commit();
        }

        /// <summary>Renumera o circuito (Dm_NumeroCircuito nos dispositivos).</summary>
        public static void SetNumero(Document doc, ElectricalSystem system, string numero)
        {
            using var tx = new Transaction(doc, "DmEletrico — Renumerar Circuito");
            tx.Start();
            EscreverNumero(doc, system, numero);
            tx.Commit();
        }

        /// <summary>Próximo número de circuito livre no QDC (para uso manual).</summary>
        public static int NextNumero(Document doc, ElementId panelId)
        {
            var usados = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .Where(s => s.BaseEquipment != null && s.BaseEquipment.Id == panelId)
                .Select(s => int.TryParse(s.CircuitNumber, out var n) ? n : 0);

            var max = usados.DefaultIfEmpty(0).Max();
            return max + 1;
        }

        private static void EscreverNumero(Document doc, ElectricalSystem system, string numero)
        {
            foreach (Element e in system.Elements)
                e.LookupParameter(DmParameters.NumeroCircuito)?.Set(numero);
        }
    }
}
