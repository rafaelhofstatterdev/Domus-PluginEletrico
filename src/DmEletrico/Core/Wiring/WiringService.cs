using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DmEletrico.Core.Circuits;

namespace DmEletrico.Core.Wiring
{
    /// <summary>Contagem de condutores de um circuito.</summary>
    public struct ContagemFios
    {
        public int Fases, Neutros, Terras, Retornos;
        public int Total => Fases + Neutros + Terras + Retornos;
    }

    /// <summary>Linha da tabela de fiação.</summary>
    public sealed class WireRow
    {
        public double SecaoMm2 { get; set; }
        public string Descricao { get; set; } = "";
        public double MetrosBase { get; set; }
        public double MetrosComFolga { get; set; }
    }

    /// <summary>
    /// Lógica de fiação: aplica as regras de neutro/terra (passo 5) aos condutores
    /// de cada conduíte e gera a tabela de fiação (passos 2/3/4).
    /// </summary>
    public static class WiringService
    {
        /// <summary>Contagem de condutores de um circuito conforme a configuração.</summary>
        public static ContagemFios Contagem(int poles, bool temIluminacao, DmWiringSettings cfg)
        {
            poles = Math.Max(1, Math.Min(3, poles));
            var neutros = poles >= 3 ? (cfg.ForcarNeutroTrifasico ? 1 : 0) : 1;
            var terras = 1; // sempre há terra; ForcarTerraIluminacao garante p/ iluminação
            if (cfg.ForcarTerraIluminacao && temIluminacao) terras = Math.Max(terras, 1);
            return new ContagemFios { Fases = poles, Neutros = neutros, Terras = terras, Retornos = 0 };
        }

        /// <summary>
        /// Recalcula e grava os parâmetros de fiação nos conduítes indicados, segundo
        /// a configuração. Gerencia a própria transação. Retorna os conduítes afetados.
        /// </summary>
        public static List<ElementId> AplicarFiacao(Document doc, ICollection<ElementId> conduitIds, DmWiringSettings cfg)
        {
            var mapaCircuito = LogicalCircuits.All(doc).ToDictionary(c => c.Quadro + "|" + c.Numero, c => c);
            var afetados = new List<ElementId>();

            using var tx = new Transaction(doc, "DmEletrico — Aplicar Fiação");
            tx.Start();

            foreach (var id in conduitIds)
            {
                var conduit = doc.GetElement(id);
                if (conduit == null) continue;

                var chave = conduit.LookupParameter(DmParameters.CircuitoOrigemId)?.AsString() ?? "";
                int poles;
                bool temIlum;
                if (mapaCircuito.TryGetValue(chave, out var circ))
                {
                    poles = circ.Dispositivos
                        .Select(d => (int)(d.LookupParameter(DmParameters.NumeroPolos)?.AsInteger() ?? 0))
                        .Where(p => p > 0).DefaultIfEmpty(1).Max();
                    temIlum = circ.Dispositivos.Any(d =>
                        (BuiltInCategory)(d.Category?.Id.Value ?? 0) == BuiltInCategory.OST_LightingFixtures);
                }
                else
                {
                    poles = Math.Max(1, conduit.LookupParameter(DmParameters.NumFases)?.AsInteger() ?? 1);
                    temIlum = false;
                }

                var ct = Contagem(poles, temIlum, cfg);
                conduit.LookupParameter(DmParameters.NumFases)?.Set(ct.Fases);
                conduit.LookupParameter(DmParameters.NumNeutros)?.Set(ct.Neutros);
                conduit.LookupParameter(DmParameters.NumTerras)?.Set(ct.Terras);
                conduit.LookupParameter(DmParameters.NumRetornos)?.Set(ct.Retornos);
                conduit.LookupParameter(DmParameters.NumCondutores)?.Set(ct.Total);
                afetados.Add(id);
            }

            tx.Commit();
            return afetados;
        }

        /// <summary>Tabela de fiação: metragem por bitola, com descrição e margem de segurança.</summary>
        public static List<WireRow> GerarTabela(Document doc, DmWiringSettings cfg)
        {
            var porSecao = new Dictionary<double, double>(); // secao → metros de cabo

            foreach (var c in new FilteredElementCollector(doc)
                         .OfCategory(BuiltInCategory.OST_Conduit)
                         .WhereElementIsNotElementType())
            {
                var secao = c.LookupParameter(DmParameters.SecaoAdotada)?.AsDouble() ?? 0;
                if (secao <= 0) continue;
                var nCond = c.LookupParameter(DmParameters.NumCondutores)?.AsInteger() ?? 0;
                if (nCond <= 0) nCond = 3;
                var lenFeet = (c.Location as LocationCurve)?.Curve?.Length ?? 0;
                var metros = UnitUtils.ConvertFromInternalUnits(lenFeet, UnitTypeId.Meters) * nCond;
                porSecao[secao] = porSecao.TryGetValue(secao, out var v) ? v + metros : metros;
            }

            return porSecao
                .OrderBy(kv => kv.Key)
                .Select(kv => new WireRow
                {
                    SecaoMm2 = kv.Key,
                    Descricao = cfg.EspecPara(kv.Key).Descricao,
                    MetrosBase = kv.Value,
                    MetrosComFolga = kv.Value * (1.0 + cfg.MargemSeguranca)
                })
                .ToList();
        }
    }
}
