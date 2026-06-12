using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DmEletrico.Core.Calculation;
using DmEletrico.Core.Circuits;

namespace DmEletrico.Core.Routing
{
    /// <summary>Resumo da execução do Conduit Builder, exibido ao usuário.</summary>
    public sealed class ConduitBuildReport
    {
        public int CircuitosProcessados { get; set; }
        public int ConduitesCriados { get; set; }
        public int CurvasCriadas { get; set; }
        public int Conexoes { get; set; }
        public int TrechosCompartilhados { get; set; }
        public List<string> Avisos { get; } = new();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Circuitos processados: {CircuitosProcessados}");
            sb.AppendLine($"Conduítes criados: {ConduitesCriados}");
            sb.AppendLine($"Curvas (fittings) criadas: {CurvasCriadas}");
            sb.AppendLine($"Conexões a dispositivos/painel: {Conexoes}");
            sb.AppendLine($"Trechos compartilhados (multi-circuito): {TrechosCompartilhados}");
            if (Avisos.Count > 0)
            {
                sb.AppendLine("\nAvisos:");
                foreach (var a in Avisos.Take(20)) sb.AppendLine("• " + a);
                if (Avisos.Count > 20) sb.AppendLine($"… e mais {Avisos.Count - 20}.");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Conduit Builder (requisito 3). Roteia conduítes mirando e conectando os
    /// conectores de CONDUÍTE (DomainCableTrayConduit) dos dispositivos e do QDC —
    /// padrão das famílias OFElétrico. Trabalha sobre circuitos lógicos (Dm_Quadro
    /// + Dm_NumeroCircuito) ou, no modo "dispositivos selecionados", conecta apenas
    /// os dispositivos escolhidos. Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public sealed class ConduitBuilderService
    {
        private const double Tol = 1e-6;
        private readonly ElectricalCalculator _calc = new();

        private sealed class ConduitMeta
        {
            public Conduit Conduit = null!;
            public string SegKey = "";
            public string CircuitoId = "";
            public string CircuitoNumero = "";
            public int NumCondutores;
            public double Secao;
            public double Corrente;
            public double Fct;
        }

        // ===== Entradas =====

        public ConduitBuildReport Build(Document doc, DmProjectSettings settings, ConduitBuildOptions options)
        {
            if (options.Modo == ModoSelecao.DispositivosSelecionados)
                return BuildBetweenDevices(doc, settings, options);

            var circuitos = LogicalCircuits.All(doc).Where(c => c.Painel != null).ToList();
            return BuildForCircuits(doc, settings, circuitos, options);
        }

        public ConduitBuildReport BuildForCircuits(
            Document doc, DmProjectSettings settings, IEnumerable<LogicalCircuit> circuitos, ConduitBuildOptions? options = null)
        {
            var report = new ConduitBuildReport();
            options ??= ConduitBuildOptions.Default(doc);
            var tetoId = options.ResolveTetoPiso(doc);
            var paredeId = options.ResolveParede(doc);
            if (tetoId == ElementId.InvalidElementId)
            {
                report.Avisos.Add("Nenhum tipo de conduíte (ConduitType) carregado no projeto.");
                return report;
            }

            var metas = new List<ConduitMeta>();
            foreach (var c in circuitos)
            {
                try { ProcessCircuit(doc, settings, c, tetoId, paredeId, options, report, metas); }
                catch (Exception ex) { report.Avisos.Add($"Circuito {c.Quadro}/{c.Numero}: {ex.Message}"); }
            }
            AgregarTrechosCompartilhados(metas, report);
            return report;
        }

        private void ProcessCircuit(
            Document doc, DmProjectSettings settings, LogicalCircuit circuito,
            ElementId tetoId, ElementId paredeId, ConduitBuildOptions options,
            ConduitBuildReport report, List<ConduitMeta> metas)
        {
            var panel = circuito.Painel!;
            var dispositivos = circuito.Dispositivos;
            if (dispositivos.Count == 0) return;

            report.CircuitosProcessados++;

            var potenciaVa = dispositivos.Sum(d => ReadDouble(d, DmParameters.Potencia));
            var poles = Math.Max(1, (int)ReadDouble(dispositivos[0], DmParameters.NumeroPolos, 1));
            var nCondutores = poles + 2;
            var chave = circuito.Quadro + "|" + circuito.Numero;

            var levelId = ResolveLevelId(doc, panel);
            var levelElev = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;

            var correnteCirc = settings.TensaoNominal > 0 ? potenciaVa / settings.TensaoNominal : 0;
            var disjuntor = Nbr5410Tables.DisjuntorComercial(correnteCirc);
            foreach (var d in dispositivos) d.LookupParameter(DmParameters.Disjuntor)?.Set(disjuntor);

            var painelOrigem = Origem(panel);

            foreach (var dispositivo in dispositivos)
            {
                var devCon = ConduitConnectorMaisProximo(dispositivo, painelOrigem);
                var panCon = ConduitConnectorMaisProximo(panel, Origem(dispositivo));
                var devPt = devCon?.Origin ?? Origem(dispositivo);
                var panPt = panCon?.Origin ?? painelOrigem;

                var ambiente = LerAmbiente(dispositivo);
                var offsetFeet = UnitUtils.ConvertToInternalUnits(settings.OffsetPorAmbiente(ambiente), UnitTypeId.Meters);
                var spineZ = levelElev + offsetFeet;

                var caminho = CaminhoComStubs(devCon, devPt, panCon, panPt, spineZ, options, settings);
                var trechoLenM = UnitUtils.ConvertFromInternalUnits(OrthogonalRouter.Comprimento(caminho), UnitTypeId.Meters);

                var r = _calc.Calcular(new TrechoInput
                {
                    ComprimentoM = trechoLenM,
                    PotenciaAparenteVa = potenciaVa,
                    TensaoNominalV = settings.TensaoNominal,
                    TemperaturaAmbienteC = settings.TemperaturaAmbiente,
                    CircuitosAgrupados = 1
                });

                var diametroMm = options.DiametroForcadoMm > 0
                    ? options.DiametroForcadoMm
                    : ConduitSizing.DiametroNominal(r.SecaoAdotadaMm2, nCondutores);

                var conduits = CriarConduites(doc, tetoId, paredeId, levelId, caminho, report);
                CriarCurvas(doc, conduits, report);
                ConectarPontas(conduits, devCon, panCon, report);
                AplicarParametros(conduits, r, potenciaVa, diametroMm, nCondutores, poles,
                    chave, dispositivo.Id.ToString(), circuito.Numero);

                foreach (var c in conduits)
                    metas.Add(new ConduitMeta
                    {
                        Conduit = c, SegKey = SegKey(c), CircuitoId = chave, CircuitoNumero = circuito.Numero,
                        NumCondutores = nCondutores, Secao = r.SecaoAdotadaMm2, Corrente = r.CorrenteProjetoA, Fct = r.Fct
                    });
            }
        }

        /// <summary>Modo "dispositivos selecionados": conecta os dispositivos escolhidos.</summary>
        public ConduitBuildReport BuildBetweenDevices(Document doc, DmProjectSettings settings, ConduitBuildOptions options)
        {
            var report = new ConduitBuildReport();
            var tetoId = options.ResolveTetoPiso(doc);
            var paredeId = options.ResolveParede(doc);
            if (tetoId == ElementId.InvalidElementId)
            {
                report.Avisos.Add("Nenhum tipo de conduíte carregado no projeto.");
                return report;
            }

            var elems = options.Dispositivos.Select(id => doc.GetElement(id)).Where(e => e != null).Cast<Element>().ToList();
            if (elems.Count < 2)
            {
                report.Avisos.Add("Selecione ao menos dois dispositivos.");
                return report;
            }

            var levelId = ResolveLevelId(doc, elems[0]);
            var levelElev = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;
            var spineZ = levelElev + UnitUtils.ConvertToInternalUnits(settings.OffsetPorAmbiente(AmbienteInstalacao.Teto), UnitTypeId.Meters);

            for (int i = 0; i < elems.Count - 1; i++)
            {
                var a = elems[i]; var b = elems[i + 1];
                var conA = ConduitConnectorMaisProximo(a, Origem(b));
                var conB = ConduitConnectorMaisProximo(b, Origem(a));
                var ptA = conA?.Origin ?? Origem(a);
                var ptB = conB?.Origin ?? Origem(b);

                var caminho = CaminhoComStubs(conA, ptA, conB, ptB, spineZ, options, settings);
                var conduits = CriarConduites(doc, tetoId, paredeId, levelId, caminho, report);
                CriarCurvas(doc, conduits, report);
                ConectarPontas(conduits, conA, conB, report);

                var diam = options.DiametroForcadoMm > 0 ? options.DiametroForcadoMm : 25;
                var diamFeet = UnitUtils.ConvertToInternalUnits(diam, UnitTypeId.Millimeters);
                foreach (var c in conduits) SetDouble(c, DmParameters.DiametroNominal, diamFeet);
            }

            return report;
        }

        // ===== Pós-passe multi-circuito =====

        private void AgregarTrechosCompartilhados(List<ConduitMeta> metas, ConduitBuildReport report)
        {
            foreach (var grupo in metas.GroupBy(m => m.SegKey))
            {
                var circuitos = grupo.Select(m => m.CircuitoId).Distinct().ToList();
                if (circuitos.Count <= 1) continue;

                report.TrechosCompartilhados++;
                var fca = Nbr5410Tables.Fca(circuitos.Count);
                var totalCondutores = grupo.Select(m => new { m.CircuitoId, m.NumCondutores })
                    .GroupBy(x => x.CircuitoId).Sum(g => g.First().NumCondutores);
                var numeros = string.Join(", ", grupo.Select(m => m.CircuitoNumero).Distinct());

                var secaoGovernante = 0.0;
                foreach (var m in grupo)
                {
                    var secao = m.Fct * fca > 0 ? Nbr5410Tables.SecaoPorCapacidade(m.Corrente / (m.Fct * fca)) : m.Secao;
                    secaoGovernante = Math.Max(secaoGovernante, secao);
                    m.Secao = secao;
                }

                var diamFeet = UnitUtils.ConvertToInternalUnits(ConduitSizing.DiametroNominal(secaoGovernante, totalCondutores), UnitTypeId.Millimeters);
                foreach (var m in grupo)
                {
                    SetDouble(m.Conduit, DmParameters.Fca, fca);
                    SetDouble(m.Conduit, DmParameters.SecaoAdotada, m.Secao);
                    SetDouble(m.Conduit, DmParameters.BitolaFase, m.Secao);
                    SetDouble(m.Conduit, DmParameters.BitolaTerra, m.Secao);
                    SetDouble(m.Conduit, DmParameters.DiametroNominal, diamFeet);
                    SetInt(m.Conduit, DmParameters.NumCondutores, totalCondutores);
                    SetString(m.Conduit, DmParameters.CircuitosNoTrecho, numeros);
                }
            }
        }

        // ===== Geometria =====

        private List<Conduit> CriarConduites(Document doc, ElementId tetoId, ElementId paredeId, ElementId levelId, IList<XYZ> caminho, ConduitBuildReport report)
        {
            var conduits = new List<Conduit>();
            for (int i = 0; i < caminho.Count - 1; i++)
            {
                var a = caminho[i]; var b = caminho[i + 1];
                if (a.DistanceTo(b) <= Tol) continue;
                var vertical = Math.Abs(b.Z - a.Z) > Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
                var typeId = vertical && paredeId != ElementId.InvalidElementId ? paredeId : tetoId;
                conduits.Add(Conduit.Create(doc, typeId, a, b, levelId));
                report.ConduitesCriados++;
            }
            return conduits;
        }

        private void CriarCurvas(Document doc, List<Conduit> conduits, ConduitBuildReport report)
        {
            for (int i = 0; i < conduits.Count - 1; i++)
            {
                var juncao = PontoComum(conduits[i], conduits[i + 1]);
                if (juncao == null) continue;
                var con1 = FindConnectorAt(conduits[i], juncao);
                var con2 = FindConnectorAt(conduits[i + 1], juncao);
                if (con1 == null || con2 == null) continue;
                try
                {
                    // NewElbowFitting respeita as Routing Preferences do ConduitType.
                    doc.Create.NewElbowFitting(con1, con2);
                    report.CurvasCriadas++;
                }
                catch { /* colinear/não suportado */ }
            }
        }

        private void ConectarPontas(List<Conduit> conduits, Connector? conInicio, Connector? conFim, ConduitBuildReport report)
        {
            if (conduits.Count == 0) return;
            if (TentarConectar(conduits[0], conInicio)) report.Conexoes++;
            if (TentarConectar(conduits[conduits.Count - 1], conFim)) report.Conexoes++;
        }

        /// <summary>
        /// Conecta apenas se a geometria permitir: conectores coincidentes e com
        /// eixos anti-paralelos (um de frente para o outro). Caso contrário NÃO
        /// tenta — evita o erro "conduíte na direção oposta / sem espaço".
        /// </summary>
        private static bool TentarConectar(Conduit conduit, Connector? alvo)
        {
            if (alvo == null || alvo.IsConnected) return false;
            var cc = FindConnectorAt(conduit, alvo.Origin);
            if (cc == null || cc.IsConnected) return false;

            // Coincidência de origem.
            if (cc.Origin.DistanceTo(alvo.Origin) > UnitUtils.ConvertToInternalUnits(0.005, UnitTypeId.Meters))
                return false;

            // Anti-paralelismo (os conectores precisam se encarar).
            var da = alvo.CoordinateSystem.BasisZ;
            var dc = cc.CoordinateSystem.BasisZ;
            if (!da.IsZeroLength() && !dc.IsZeroLength() &&
                da.Normalize().DotProduct(dc.Normalize()) > -0.85)
                return false;

            try { cc.ConnectTo(alvo); return true; }
            catch { return false; }
        }

        private void AplicarParametros(List<Conduit> conduits, TrechoResultado r, double potenciaVa,
            double diametroMm, int nCondutores, int nFases, string circuitoId, string dispositivoId, string circuitoNumero)
        {
            var diamFeet = UnitUtils.ConvertToInternalUnits(diametroMm, UnitTypeId.Millimeters);
            foreach (var c in conduits)
            {
                SetDouble(c, DmParameters.PotenciaAparente, potenciaVa);
                SetDouble(c, DmParameters.CorrenteProjeto, r.CorrenteProjetoA);
                SetDouble(c, DmParameters.Fct, r.Fct);
                SetDouble(c, DmParameters.Fca, r.Fca);
                SetDouble(c, DmParameters.SecaoAdotada, r.SecaoAdotadaMm2);
                SetDouble(c, DmParameters.QuedaTensao, r.QuedaTensaoPercent);
                SetDouble(c, DmParameters.DiametroNominal, diamFeet);
                SetInt(c, DmParameters.NumCondutores, nCondutores);
                SetString(c, DmParameters.CircuitoOrigemId, circuitoId);
                SetString(c, DmParameters.DispositivoId, dispositivoId);
                SetString(c, DmParameters.CircuitosNoTrecho, circuitoNumero);
                SetInt(c, DmParameters.NumFases, nFases);
                SetInt(c, DmParameters.NumNeutros, 1);
                SetInt(c, DmParameters.NumTerras, 1);
                SetInt(c, DmParameters.NumRetornos, 0);
                SetDouble(c, DmParameters.BitolaFase, r.SecaoAdotadaMm2);
                SetDouble(c, DmParameters.BitolaTerra, r.SecaoAdotadaMm2);
            }
        }

        /// <summary>
        /// Monta o caminho com "stubs" alinhados ao eixo de cada conector: um trecho
        /// sai do conector SEMPRE na direção para fora dele (+BasisZ — nunca
        /// invertida, senão o conduíte entraria no dispositivo) antes de virar para
        /// o roteamento principal. Pontos colineares são fundidos para que stub +
        /// subida virem um único conduíte (sem fitting impossível no meio).
        /// </summary>
        private static readonly double MergeTolFeet = UnitUtils.ConvertToInternalUnits(0.04, UnitTypeId.Meters); // ~4 cm

        private static IList<XYZ> CaminhoComStubs(Connector? conA, XYZ ptA, Connector? conB, XYZ ptB, double spineZ, ConduitBuildOptions options, DmProjectSettings settings)
        {
            // Arranque curto: só o necessário para sair da caixa e caber o cotovelo.
            var stub = UnitUtils.ConvertToInternalUnits(0.10, UnitTypeId.Meters); // 10 cm

            var startMid = ptA;
            var endMid = ptB;
            if (conA != null) startMid = conA.Origin + AxisOut(conA) * stub;
            if (conB != null) endMid = conB.Origin + AxisOut(conB) * stub;

            var middle = EscolherCaminho(startMid, endMid, spineZ, options, settings);

            // Se o caminho dobraria 180° logo após o stub (rota contra o eixo do
            // conector), abandona o stub desse lado e parte direto do conector.
            if (conA != null && Reverte(conA.Origin, startMid, middle, daPonta: true))
            {
                startMid = conA.Origin;
                middle = EscolherCaminho(startMid, endMid, spineZ, options, settings);
            }
            if (conB != null && Reverte(conB.Origin, endMid, middle, daPonta: false))
            {
                endMid = conB.Origin;
                middle = EscolherCaminho(startMid, endMid, spineZ, options, settings);
            }

            var pts = new List<XYZ>();
            if (conA != null && startMid != conA.Origin) pts.Add(conA.Origin);
            foreach (var p in middle) pts.Add(p);
            if (conB != null && endMid != conB.Origin) pts.Add(conB.Origin);
            return SimplificarColineares(Dedupe(pts));
        }

        /// <summary>Eixo do conector para FORA do dispositivo (nunca invertido).</summary>
        private static XYZ AxisOut(Connector con)
        {
            var d = con.CoordinateSystem.BasisZ;
            return d.IsZeroLength() ? XYZ.BasisZ : d.Normalize();
        }

        /// <summary>True se a rota dobra ~180° na emenda do stub com o caminho do meio.</summary>
        private static bool Reverte(XYZ origem, XYZ stubEnd, IList<XYZ> middle, bool daPonta)
        {
            if (middle.Count < 2) return false;
            var dStub = stubEnd - origem;
            XYZ dNext = daPonta
                ? middle[1] - middle[0]
                : middle[middle.Count - 2] - middle[middle.Count - 1];
            if (dStub.IsZeroLength() || dNext.IsZeroLength()) return false;
            return dStub.Normalize().DotProduct(dNext.Normalize()) < -0.9;
        }

        /// <summary>
        /// Funde pontos consecutivos a menos de ~4 cm para o Revit não tentar criar
        /// segmentos menores que um fitting. O último ponto (alvo da conexão) é
        /// sempre preservado: se ficar perto do anterior, o anterior é descartado.
        /// </summary>
        private static IList<XYZ> Dedupe(IList<XYZ> pts)
        {
            var result = new List<XYZ>();
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                var ultimo = i == pts.Count - 1;
                if (result.Count == 0) { result.Add(p); continue; }

                if (result[result.Count - 1].DistanceTo(p) > MergeTolFeet)
                    result.Add(p);
                else if (ultimo)
                    result[result.Count - 1] = p; // mantém o alvo exato
            }
            return result;
        }

        /// <summary>Remove vértices colineares: trechos na mesma direção viram um só conduíte.</summary>
        private static IList<XYZ> SimplificarColineares(IList<XYZ> pts)
        {
            if (pts.Count < 3) return pts;
            var result = new List<XYZ> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var d1 = pts[i] - result[result.Count - 1];
                var d2 = pts[i + 1] - pts[i];
                if (d1.IsZeroLength() || d2.IsZeroLength()) continue;
                if (d1.Normalize().IsAlmostEqualTo(d2.Normalize(), 1e-6)) continue; // colinear
                result.Add(pts[i]);
            }
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        private static IList<XYZ> EscolherCaminho(XYZ a, XYZ b, double spineZ, ConduitBuildOptions options, DmProjectSettings settings)
        {
            if (options.AnguloPlanta == AnguloPlanta.Livre || settings.Modo == ModoRoteamento.Direto)
                return OrthogonalRouter.RouteDireto(a, b);

            switch (options.Caminho)
            {
                case CaminhoConduite.Parede: return OrthogonalRouter.RouteParede(a, b);
                case CaminhoConduite.Teto: return OrthogonalRouter.RouteTeto(a, b, spineZ);
                default:
                    // Ambos: mesma altura → pela parede (ortogonal no plano);
                    // alturas diferentes → pelo teto (sobe, corre, desce na parede).
                    var mesmaAltura = Math.Abs(a.Z - b.Z) <= UnitUtils.ConvertToInternalUnits(0.30, UnitTypeId.Meters);
                    return mesmaAltura
                        ? OrthogonalRouter.RouteParede(a, b)
                        : OrthogonalRouter.RouteTeto(a, b, spineZ);
            }
        }

        // ===== Conectores =====

        private static ConnectorManager? ConnectorManagerOf(Element e)
            => (e as FamilyInstance)?.MEPModel?.ConnectorManager ?? (e as MEPCurve)?.ConnectorManager;

        /// <summary>
        /// Escolhe o conector de conduíte livre que melhor serve para sair em direção
        /// ao alvo: apenas conectores que apontam para FORA do dispositivo (descarta
        /// os que entram nele) e, entre esses, o cujo eixo melhor aponta para o alvo
        /// (ex.: o conector superior de uma tomada, para subir ao teto).
        /// </summary>
        private static Connector? ConduitConnectorMaisProximo(Element e, XYZ alvo)
        {
            var manager = ConnectorManagerOf(e);
            if (manager == null) return null;

            var centro = CentroDe(e);
            const double K = 5.0; // peso do alinhamento (pés)

            Connector? best = null;
            double bestScore = double.MaxValue;
            Connector? fallback = null;

            foreach (Connector c in manager.Connectors)
            {
                if (c.IsConnected) continue;
                if (c.Domain != Domain.DomainCableTrayConduit) continue;
                fallback ??= c;

                var eixo = c.CoordinateSystem.BasisZ;
                if (eixo.IsZeroLength()) continue;
                eixo = eixo.Normalize();

                // Descarta conectores cujo eixo aponta para dentro do dispositivo.
                var paraFora = c.Origin - centro;
                if (!paraFora.IsZeroLength() && eixo.DotProduct(paraFora.Normalize()) < -0.3) continue;

                var paraAlvo = alvo - c.Origin;
                var alinhamento = paraAlvo.IsZeroLength() ? 0.0 : eixo.DotProduct(paraAlvo.Normalize());

                var score = c.Origin.DistanceTo(alvo) - alinhamento * K;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best ?? fallback;
        }

        private static XYZ CentroDe(Element e)
        {
            var bb = e.get_BoundingBox(null);
            if (bb != null) return (bb.Min + bb.Max) * 0.5;
            return (e.Location as LocationPoint)?.Point ?? XYZ.Zero;
        }

        private static Connector? FindConnectorAt(MEPCurve conduit, XYZ pt)
        {
            var manager = conduit.ConnectorManager;
            if (manager == null) return null;
            Connector? best = null; double bestDist = double.MaxValue;
            foreach (Connector con in manager.Connectors)
            {
                var d = con.Origin.DistanceTo(pt);
                if (d < bestDist) { bestDist = d; best = con; }
            }
            return bestDist <= 1e-2 ? best : null;
        }

        private static XYZ Origem(Element e)
        {
            if ((e.Location as LocationPoint)?.Point is XYZ p) return p;
            var man = ConnectorManagerOf(e);
            if (man != null)
                foreach (Connector c in man.Connectors) return c.Origin;
            var bb = e.get_BoundingBox(null);
            return bb != null ? (bb.Min + bb.Max) * 0.5 : XYZ.Zero;
        }

        private static XYZ? PontoComum(Conduit a, Conduit b)
        {
            foreach (var pa in EndPoints(a))
                foreach (var pb in EndPoints(b))
                    if (pa.DistanceTo(pb) <= 1e-4) return pa;
            return null;
        }

        private static IEnumerable<XYZ> EndPoints(Conduit c)
        {
            var curve = (c.Location as LocationCurve)?.Curve;
            if (curve == null) yield break;
            yield return curve.GetEndPoint(0);
            yield return curve.GetEndPoint(1);
        }

        private static AmbienteInstalacao LerAmbiente(Element e)
        {
            var s = e.LookupParameter(DmParameters.Ambiente)?.AsString();
            return Enum.TryParse<AmbienteInstalacao>(s, true, out var a) ? a : AmbienteInstalacao.Teto;
        }

        private static ElementId ResolveLevelId(Document doc, Element e)
        {
            if (e.LevelId != null && e.LevelId != ElementId.InvalidElementId) return e.LevelId;
            var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
            return level?.Id ?? ElementId.InvalidElementId;
        }

        private static string SegKey(Conduit c)
        {
            var curve = (c.Location as LocationCurve)?.Curve;
            if (curve == null) return Guid.NewGuid().ToString();
            string a = Key(curve.GetEndPoint(0)), b = Key(curve.GetEndPoint(1));
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static string Key(XYZ p) => string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3}", p.X, p.Y, p.Z);

        private static double ReadDouble(Element e, string name, double fallback = 0)
        {
            var p = e.LookupParameter(name);
            if (p == null) return fallback;
            return p.StorageType switch
            {
                StorageType.Double => p.AsDouble(),
                StorageType.Integer => p.AsInteger(),
                _ => fallback
            };
        }

        private static void SetDouble(Element e, string name, double value) => e.LookupParameter(name)?.Set(value);
        private static void SetInt(Element e, string name, int value) => e.LookupParameter(name)?.Set(value);
        private static void SetString(Element e, string name, string value) => e.LookupParameter(name)?.Set(value);
    }
}
