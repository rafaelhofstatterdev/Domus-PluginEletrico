using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Wiring
{
    /// <summary>
    /// Analisa a topologia física dos conduítes (árvore enraizada no QD) e calcula,
    /// para CADA conduíte, quais circuitos passam por ele (carga a jusante) e os
    /// condutores correspondentes. Exemplo: no trecho QD→lâmpada passam o circuito
    /// da lâmpada e o da tomada que está depois dela; no trecho lâmpada→tomada passa
    /// só o circuito da tomada.
    /// </summary>
    public static class WiringTopology
    {
        /// <summary>Resultado de fios por circuito num trecho.</summary>
        private sealed class CircuitoFios
        {
            public string Numero = "";
            public int Fases, Neutros, Terras;
            public string Resumo()
            {
                var p = new List<string>();
                if (Fases > 0) p.Add($"{Fases}F");
                if (Neutros > 0) p.Add($"{Neutros}N");
                if (Terras > 0) p.Add($"{Terras}T");
                return $"C{Numero}:{string.Join("+", p)}";
            }
        }

        /// <summary>Recalcula e grava os condutores de cada conduíte pela topologia. Própria transação.</summary>
        public static int Analisar(Document doc, DmWiringSettings cfg)
        {
            var conduites = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Conduit).WhereElementIsNotElementType()
                .Where(c => long.TryParse(c.LookupParameter(DmParameters.NoA)?.AsString(), out _)
                         && long.TryParse(c.LookupParameter(DmParameters.NoB)?.AsString(), out _))
                .ToList();
            if (conduites.Count == 0) return 0;

            // Grafo: adjacência + conduítes por aresta (par não-ordenado).
            var adj = new Dictionary<long, HashSet<long>>();
            var arestaConduites = new Dictionary<(long, long), List<Element>>();
            foreach (var c in conduites)
            {
                var a = long.Parse(c.LookupParameter(DmParameters.NoA)!.AsString());
                var b = long.Parse(c.LookupParameter(DmParameters.NoB)!.AsString());
                if (a == b) continue;
                if (!adj.ContainsKey(a)) adj[a] = new HashSet<long>();
                if (!adj.ContainsKey(b)) adj[b] = new HashSet<long>();
                adj[a].Add(b); adj[b].Add(a);
                var key = a < b ? (a, b) : (b, a);
                if (!arestaConduites.TryGetValue(key, out var lst)) arestaConduites[key] = lst = new List<Element>();
                lst.Add(c);
            }

            // Raízes = QDs.
            var qds = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment).WhereElementIsNotElementType()
                .Select(e => e.Id.Value).ToHashSet();

            // BFS a partir dos QDs → pai e profundidade.
            var pai = new Dictionary<long, long>();
            var prof = new Dictionary<long, int>();
            var fila = new Queue<long>();
            foreach (var qd in qds.Where(adj.ContainsKey))
            {
                if (prof.ContainsKey(qd)) continue;
                prof[qd] = 0; fila.Enqueue(qd);
                while (fila.Count > 0)
                {
                    var n = fila.Dequeue();
                    foreach (var nb in adj[n])
                        if (!prof.ContainsKey(nb)) { prof[nb] = prof[n] + 1; pai[nb] = n; fila.Enqueue(nb); }
                }
            }

            // Filhos por nó (para subárvore).
            var filhos = new Dictionary<long, List<long>>();
            foreach (var kv in pai)
            {
                if (!filhos.TryGetValue(kv.Value, out var lst)) filhos[kv.Value] = lst = new List<long>();
                lst.Add(kv.Key);
            }

            int atualizados = 0;
            using var tx = new Transaction(doc, "DmEletrico — Analisar Fiação");
            tx.Start();

            foreach (var (key, conduitsAresta) in arestaConduites)
            {
                // Nó a jusante = o de maior profundidade (mais longe do QD).
                if (!prof.ContainsKey(key.Item1) || !prof.ContainsKey(key.Item2)) continue;
                var jusante = prof[key.Item1] >= prof[key.Item2] ? key.Item1 : key.Item2;

                var devices = Subarvore(jusante, filhos)
                    .Select(id => doc.GetElement(new ElementId(id)))
                    .Where(EhDispositivo)
                    .ToList();

                var circuitos = AgruparCircuitos(devices, cfg);
                if (circuitos.Count == 0) continue;

                int fases = circuitos.Sum(x => x.Fases);
                int neutros = circuitos.Sum(x => x.Neutros);
                int terras = circuitos.Sum(x => x.Terras);
                var resumo = string.Join(" | ", circuitos.Select(x => x.Resumo()));

                foreach (var c in conduitsAresta)
                {
                    c.LookupParameter(DmParameters.NumFases)?.Set(fases);
                    c.LookupParameter(DmParameters.NumNeutros)?.Set(neutros);
                    c.LookupParameter(DmParameters.NumTerras)?.Set(terras);
                    c.LookupParameter(DmParameters.NumRetornos)?.Set(0);
                    c.LookupParameter(DmParameters.NumCondutores)?.Set(fases + neutros + terras);
                    c.LookupParameter(DmParameters.CircuitosNoTrecho)?.Set(resumo);
                    atualizados++;
                }
            }

            tx.Commit();
            return atualizados;
        }

        private static IEnumerable<long> Subarvore(long raiz, Dictionary<long, List<long>> filhos)
        {
            var pilha = new Stack<long>();
            pilha.Push(raiz);
            while (pilha.Count > 0)
            {
                var n = pilha.Pop();
                yield return n;
                if (filhos.TryGetValue(n, out var fs))
                    foreach (var f in fs) pilha.Push(f);
            }
        }

        private static bool EhDispositivo(Element? e)
        {
            var bic = (BuiltInCategory)(e?.Category?.Id.Value ?? 0);
            return bic == BuiltInCategory.OST_ElectricalFixtures || bic == BuiltInCategory.OST_LightingFixtures;
        }

        private static List<CircuitoFios> AgruparCircuitos(IEnumerable<Element> devices, DmWiringSettings cfg)
        {
            return devices
                .GroupBy(d => (d.LookupParameter(DmParameters.Quadro)?.AsString() ?? "")
                            + "|" + (d.LookupParameter(DmParameters.NumeroCircuito)?.AsString() ?? ""))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key.Split('|').Last()))
                .Select(g =>
                {
                    var numero = g.Key.Split('|').Last();
                    var poles = g.Max(d => (int)(d.LookupParameter(DmParameters.NumeroPolos)?.AsInteger() ?? 1));
                    var tipo = g.Select(d => d.LookupParameter(DmParameters.TipoCircuito)?.AsString())
                                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
                    var ct = WiringService.ContagemPorTipo(poles, tipo, cfg);
                    return new CircuitoFios { Numero = numero, Fases = ct.Fases, Neutros = ct.Neutros, Terras = ct.Terras };
                })
                .OrderBy(x => int.TryParse(x.Numero, out var n) ? n : 0)
                .ToList();
        }
    }
}
