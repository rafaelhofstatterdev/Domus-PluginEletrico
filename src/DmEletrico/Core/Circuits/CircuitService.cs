using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

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
        public static (string numero, string nativoInfo) CreateAndAssign(Document doc, ICollection<ElementId> deviceIds, FamilyInstance panel)
        {
            if (deviceIds.Count == 0) return ("", "");

            var numero = NextNumero(doc, panel.Name).ToString();

            using var tx = new Transaction(doc, "DmEletrico — Criar Circuito");
            tx.Start();
            Core.WarningSwallower.Apply(tx);

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

            // Cria também o circuito NATIVO do Revit (inicializa "força"), quando os
            // dispositivos têm conector de força livre. Best-effort.
            var nativoInfo = TentarCriarSistemaNativo(doc, deviceIds, panel);

            tx.Commit();
            return (numero, nativoInfo);
        }

        private static string TentarCriarSistemaNativo(Document doc, ICollection<ElementId> deviceIds, FamilyInstance panel)
        {
            var validos = deviceIds.Where(id => TemConectorPowerLivre(doc.GetElement(id))).ToList();
            if (validos.Count == 0)
                return "Circuito nativo NÃO criado: nenhum dispositivo tem conector de força (Power) livre.";
            try
            {
                var sys = ElectricalSystem.Create(doc, validos, ElectricalSystemType.PowerCircuit);
                try { sys.SelectPanel(panel); }
                catch (System.Exception ex)
                {
                    return $"Circuito nativo criado, mas não foi atribuído ao QD '{panel.Name}': {ex.Message}";
                }
                doc.Regenerate();
                return $"Circuito nativo do Revit criado e atribuído a '{panel.Name}' (nº {sys.CircuitNumber}).";
            }
            catch (System.Exception ex)
            {
                return "Circuito nativo NÃO criado pelo Revit: " + ex.Message;
            }
        }

        private static bool TemConectorPowerLivre(Element? e)
        {
            var manager = (e as FamilyInstance)?.MEPModel?.ConnectorManager;
            if (manager == null) return false;
            foreach (Connector c in manager.Connectors)
            {
                if (c.Domain != Domain.DomainElectrical || c.IsConnected) continue;
                if (c.ElectricalSystemType == ElectricalSystemType.PowerCircuit ||
                    c.ElectricalSystemType == ElectricalSystemType.UndefinedSystemType)
                    return true;
            }
            return false;
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
