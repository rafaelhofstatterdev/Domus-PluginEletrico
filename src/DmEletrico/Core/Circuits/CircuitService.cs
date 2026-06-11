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

            // Só dispositivos com conector elétrico de força LIVRE podem iniciar o circuito.
            var validos = deviceIds.Where(id => TemConectorPowerLivre(doc.GetElement(id))).ToList();
            if (validos.Count == 0)
                throw new System.InvalidOperationException(
                    "Nenhum dos dispositivos pode iniciar um circuito de força.\n\n" + Diagnostico(doc, deviceIds));

            using var tx = new Transaction(doc, "DmEletrico — Criar Circuito");
            tx.Start();

            ElectricalSystem system;
            try
            {
                system = ElectricalSystem.Create(doc, validos, ElectricalSystemType.PowerCircuit);
            }
            catch (System.Exception ex)
            {
                tx.RollBack();
                throw new System.InvalidOperationException(
                    "O Revit recusou a criação do circuito.\n\n" + Diagnostico(doc, deviceIds) +
                    "\nDetalhe: " + ex.Message);
            }

            system.SelectPanel(panel);
            doc.Regenerate();

            EscreverNumero(doc, system, system.CircuitNumber);
            EscreverQuadro(system, panel.Name);
            tx.Commit();
            return system;
        }

        /// <summary>True se o elemento tem um conector elétrico de força ainda não ligado.</summary>
        private static bool TemConectorPowerLivre(Element? e)
        {
            var manager = (e as FamilyInstance)?.MEPModel?.ConnectorManager;
            if (manager == null) return false;
            foreach (Connector c in manager.Connectors)
            {
                if (c.Domain != Domain.DomainElectrical) continue;
                if (c.IsConnected) continue;
                if (c.ElectricalSystemType == ElectricalSystemType.PowerCircuit ||
                    c.ElectricalSystemType == ElectricalSystemType.UndefinedSystemType)
                    return true;
            }
            return false;
        }

        /// <summary>Descreve os conectores de cada dispositivo para diagnóstico.</summary>
        private static string Diagnostico(Document doc, ICollection<ElementId> ids)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Conectores encontrados:");
            foreach (var id in ids.Take(8))
            {
                var e = doc.GetElement(id) as FamilyInstance;
                var manager = e?.MEPModel?.ConnectorManager;
                sb.Append($"• [{id}] {e?.Name}: ");
                if (manager == null) { sb.AppendLine("sem conectores MEP."); continue; }

                var descr = manager.Connectors.Cast<Connector>()
                    .Select(c => c.Domain == Domain.DomainElectrical
                        ? $"{c.ElectricalSystemType}{(c.IsConnected ? " (ligado)" : "")}"
                        : c.Domain.ToString());
                var lista = string.Join(", ", descr);
                sb.AppendLine(string.IsNullOrEmpty(lista) ? "nenhum conector." : lista);
            }
            sb.AppendLine("\nDica: o dispositivo precisa de um conector elétrico 'Power' livre. " +
                          "Se já mostra Painel/Circuito, ele já está circuitado.");
            return sb.ToString();
        }

        /// <summary>Reatribui um circuito a outro QDC.</summary>
        public static void Reassign(Document doc, ElectricalSystem system, FamilyInstance novoPainel)
        {
            using var tx = new Transaction(doc, "DmEletrico — Reatribuir Circuito");
            tx.Start();
            system.SelectPanel(novoPainel);
            doc.Regenerate();
            EscreverNumero(doc, system, system.CircuitNumber);
            EscreverQuadro(system, novoPainel.Name);
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

        private static void EscreverQuadro(ElectricalSystem system, string quadro)
        {
            foreach (Element e in system.Elements)
                e.LookupParameter(DmParameters.Quadro)?.Set(quadro);
        }
    }
}
