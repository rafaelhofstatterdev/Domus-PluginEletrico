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

            var correnteCirc = settings.TensaoNominal > 0 ? potenciaVa / settings.TensaoNominal : 0;
            var disjuntor = Nbr5410Tables.DisjuntorComercial(correnteCirc);
            foreach (var d in dispositivos) d.LookupParameter(DmParameters.Disjuntor)?.Set(disjuntor);

            // Topologia "vizinho mais próximo": árvore mínima (Prim) enraizada no
            // quadro. A cada passo liga o nó FORA da árvore mais próximo de algum nó
            // JÁ na árvore. Assim uma luminária-junção encadeia para a vizinha mais
            // próxima (usando um conector lateral livre por aresta) em vez de cada
            // dispositivo voltar isolado ao quadro.
            var nodes = new List<Element> { panel };
            nodes.AddRange(dispositivos);
            var pos = nodes.Select(Origem).ToList();
            var naArvore = new bool[nodes.Count];
            naArvore[0] = true;

            for (int restantes = nodes.Count - 1; restantes > 0; restantes--)
            {
                int de = -1, para = -1;
                double melhor = double.MaxValue;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!naArvore[i]) continue;
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (naArvore[j]) continue;
                        var d = pos[i].DistanceTo(pos[j]);
                        if (d < melhor) { melhor = d; de = i; para = j; }
                    }
                }
                if (para < 0) break;
                naArvore[para] = true;

                RotearAresta(doc, nodes[de], nodes[para], tetoId, paredeId, levelId,
                    potenciaVa, nCondutores, poles, chave, circuito.Numero, options, settings, report, metas);
            }
        }

        /// <summary>
        /// Roteia e cria os conduítes de UMA aresta da árvore (quadro→dispositivo ou
        /// dispositivo→dispositivo): escolhe os conectores de cada ponta, monta o
        /// caminho ortogonal, cria conduítes/curvas, conecta as pontas e aplica os
        /// parâmetros elétricos. Cada conector é escolhido entre os AINDA livres, então
        /// um nó-junção distribui suas arestas por conectores diferentes.
        /// </summary>
        private void RotearAresta(
            Document doc, Element elemA, Element elemB,
            ElementId tetoId, ElementId paredeId, ElementId levelId,
            double potenciaVa, int nCondutores, int poles, string chave, string circuitoNumero,
            ConduitBuildOptions options, DmProjectSettings settings,
            ConduitBuildReport report, List<ConduitMeta> metas)
        {
            var locA = Origem(elemA);
            var locB = Origem(elemB);
            var spineBase = Math.Max(locA.Z, locB.Z);

            var conA = EscolherConectorConduite(elemA, locB, spineBase, options.Caminho);
            var conB = EscolherConectorConduite(elemB, locA, spineBase, options.Caminho);
            var ptA = conA?.Origin ?? locA;
            var ptB = conB?.Origin ?? locB;

            var spineZ = SpineElevation(conA, ptA, conB, ptB);

            var caminho = CaminhoComStubs(conA, ptA, conB, ptB, spineZ, options, settings);
            var trechoLenM = UnitUtils.ConvertFromInternalUnits(OrthogonalRouter.Comprimento(caminho), UnitTypeId.Meters);

            var r = _calc.Calcular(new TrechoInput
            {
                ComprimentoM = trechoLenM,
                PotenciaAparenteVa = potenciaVa,
                TensaoNominalV = settings.TensaoNominal,
                TemperaturaAmbienteC = settings.TemperaturaAmbiente,
                CircuitosAgrupados = 1
            });

            // Prioridade do diâmetro físico: forçado pelo usuário > tamanho do
            // conector do dispositivo (obrigatório para a conexão encaixar) > NBR.
            var diamConectorFeet = DiametroConectorFeet(conA, conB);
            var diametroMm = options.DiametroForcadoMm > 0
                ? options.DiametroForcadoMm
                : diamConectorFeet > 0
                    ? UnitUtils.ConvertFromInternalUnits(diamConectorFeet, UnitTypeId.Millimeters)
                    : ConduitSizing.DiametroNominal(r.SecaoAdotadaMm2, nCondutores);
            var diamFisicoFeet = UnitUtils.ConvertToInternalUnits(diametroMm, UnitTypeId.Millimeters);

            var conduits = CriarConduites(doc, tetoId, paredeId, levelId, caminho, report, diamFisicoFeet);
            CriarCurvas(doc, conduits, report);
            ConectarPontas(conduits, conA, conB, report);
            AplicarParametros(conduits, r, potenciaVa, diametroMm, nCondutores, poles,
                chave, elemB.Id.ToString(), circuitoNumero);

            foreach (var c in conduits)
                metas.Add(new ConduitMeta
                {
                    Conduit = c, SegKey = SegKey(c), CircuitoId = chave, CircuitoNumero = circuitoNumero,
                    NumCondutores = nCondutores, Secao = r.SecaoAdotadaMm2, Corrente = r.CorrenteProjetoA, Fct = r.Fct
                });
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

            for (int i = 0; i < elems.Count - 1; i++)
            {
                var a = elems[i]; var b = elems[i + 1];
                var locA = Origem(a); var locB = Origem(b);
                var spineBase = Math.Max(locA.Z, locB.Z);

                var conA = EscolherConectorConduite(a, locB, spineBase, options.Caminho);
                var conB = EscolherConectorConduite(b, locA, spineBase, options.Caminho);
                var ptA = conA?.Origin ?? locA;
                var ptB = conB?.Origin ?? locB;

                var spineZ = SpineElevation(conA, ptA, conB, ptB);

                var caminho = CaminhoComStubs(conA, ptA, conB, ptB, spineZ, options, settings);

                var diamConectorFeet = DiametroConectorFeet(conA, conB);
                var diamMm = options.DiametroForcadoMm > 0
                    ? options.DiametroForcadoMm
                    : diamConectorFeet > 0
                        ? UnitUtils.ConvertFromInternalUnits(diamConectorFeet, UnitTypeId.Millimeters)
                        : 25;
                var diamFeet = UnitUtils.ConvertToInternalUnits(diamMm, UnitTypeId.Millimeters);

                var conduits = CriarConduites(doc, tetoId, paredeId, levelId, caminho, report, diamFeet);
                CriarCurvas(doc, conduits, report);
                ConectarPontas(conduits, conA, conB, report);

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

        private List<Conduit> CriarConduites(
            Document doc, ElementId tetoId, ElementId paredeId, ElementId levelId,
            IList<XYZ> caminho, ConduitBuildReport report, double diamFeet = 0)
        {
            var conduits = new List<Conduit>();
            for (int i = 0; i < caminho.Count - 1; i++)
            {
                var a = caminho[i]; var b = caminho[i + 1];
                if (a.DistanceTo(b) <= Tol) continue;
                var vertical = Math.Abs(b.Z - a.Z) > Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
                var typeId = vertical && paredeId != ElementId.InvalidElementId ? paredeId : tetoId;
                var conduit = Conduit.Create(doc, typeId, a, b, levelId);

                // Diâmetro FÍSICO do conduíte (não só o parâmetro Dm_): sem isso o
                // conduíte fica no tamanho padrão do tipo, diferente do conector do
                // dispositivo, e a conexão/cotovelo falham.
                if (diamFeet > 0)
                {
                    try { conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM)?.Set(diamFeet); }
                    catch { /* tamanho fora da lista de tamanhos do tipo */ }
                }

                conduits.Add(conduit);
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

            // Anti-paralelismo (os conectores precisam se encarar). O conduíte é
            // criado ao longo do eixo exato do conector, então isto é ~-1.0; a folga
            // cobre apenas desvio numérico.
            var da = alvo.CoordinateSystem.BasisZ;
            var dc = cc.CoordinateSystem.BasisZ;
            if (!da.IsZeroLength() && !dc.IsZeroLength() &&
                da.Normalize().DotProduct(dc.Normalize()) > -0.6)
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
        /// sai do conector na direção do seu eixo snapado para FORA do dispositivo
        /// (lateral, para cima ou para baixo — o que a família definir; nunca para
        /// dentro) antes de virar para o roteamento principal. Pontos colineares são
        /// fundidos para que stub + trecho virem um único conduíte (sem fitting
        /// impossível no meio).
        /// </summary>
        private static readonly double MergeTolFeet = UnitUtils.ConvertToInternalUnits(0.04, UnitTypeId.Meters); // ~4 cm

        /// <summary>
        /// Arranque a partir do conector. PRECISA ser maior que o raio de curvatura
        /// do cotovelo (~15 cm para conduíte de 25 mm); com stub menor o fitting não
        /// cabe e o Revit acusa "não há espaço para criar as conexões".
        /// </summary>
        private static readonly double StubFeet = UnitUtils.ConvertToInternalUnits(0.25, UnitTypeId.Meters); // 25 cm

        /// <summary>
        /// Elevação da espinha horizontal: o topo do stub mais alto entre as duas
        /// pontas. Assim a rota nunca sobe acima de quem já está no teto (uma
        /// luminária a 2,85 m corre ~na própria altura, não num plano global a 3,0 m)
        /// e nenhuma ponta precisa "mergulhar" abaixo do seu stub vertical.
        /// </summary>
        private static double SpineElevation(Connector? a, XYZ pa, Connector? b, XYZ pb)
            => Math.Max(StubTopZ(a, pa), StubTopZ(b, pb));

        private static double StubTopZ(Connector? c, XYZ p)
        {
            if (c == null) return p.Z;
            // Só conta o stub que SOBE: um conector lateral/inferior não eleva a espinha.
            return AxisOut(c).Z > 0.5 ? p.Z + StubFeet : p.Z;
        }

        private static IList<XYZ> CaminhoComStubs(Connector? conA, XYZ ptA, Connector? conB, XYZ ptB, double spineZ, ConduitBuildOptions options, DmProjectSettings settings)
        {
            // Arranque curto: só o necessário para sair da caixa e caber o cotovelo.
            var stub = StubFeet;

            var startMid = ptA;
            var endMid = ptB;
            if (conA != null) startMid = conA.Origin + AxisOut(conA) * stub;
            if (conB != null) endMid = conB.Origin + AxisOut(conB) * stub;

            // Pernas verticais menores que um fitting não cabem: se a espinha está
            // quase na altura de uma das pontas, alinha-a a essa ponta (sem micro-subida).
            var minLeg = UnitUtils.ConvertToInternalUnits(0.15, UnitTypeId.Meters);
            var dA = Math.Abs(spineZ - startMid.Z);
            var dB = Math.Abs(spineZ - endMid.Z);
            if (dA < minLeg && dB < minLeg) spineZ = Math.Max(startMid.Z, endMid.Z);
            else if (dA < minLeg) spineZ = startMid.Z;
            else if (dB < minLeg) spineZ = endMid.Z;

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

        /// <summary>
        /// Eixo EXATO do conector (BasisZ), normalizado. NÃO snapar: o ConnectTo
        /// exige que o conduíte fique anti-paralelo ao eixo real do conector; snapar
        /// um conector radial quebraria isso ("direção oposta") e um leve componente
        /// vertical viraria ±Z, inflando a espinha (loops). A seleção é que prioriza
        /// conectores já alinhados a um eixo (ver EscolherConectorConduite).
        /// </summary>
        private static XYZ AxisOut(Connector con)
        {
            var d = con.CoordinateSystem.BasisZ;
            return d.IsZeroLength() ? XYZ.BasisZ : d.Normalize();
        }

        /// <summary>Quão alinhado a um eixo (±X/±Y/±Z) está o vetor: 1 = axial, ~0,71 = 45°.</summary>
        private static double Axialidade(XYZ v)
            => Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));

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
        /// Escolhe o conector de conduíte livre alinhado com o PRIMEIRO movimento da
        /// rota, não com a reta até o alvo. Esse é o ponto-chave para pegar o conector
        /// SUPERIOR de uma tomada quando o conduíte vai subir ao teto: mesmo que o
        /// destino esteja longe na horizontal, a rota sai verticalmente, então o
        /// conector certo é o que aponta para a espinha — não o lateral.
        ///
        ///  - via teto  → o alvo de mira é o ponto na espinha logo acima do dispositivo
        ///                (primeiro movimento = subir);
        ///  - parede/direto → o alvo de mira é o outro dispositivo na mesma altura
        ///                (primeiro movimento = correr na horizontal).
        /// </summary>
        private static Connector? EscolherConectorConduite(Element e, XYZ outroPonto, double spineZ, CaminhoConduite caminho)
        {
            var manager = ConnectorManagerOf(e);
            if (manager == null) return null;

            var loc = Origem(e);
            var subida = UnitUtils.ConvertToInternalUnits(0.30, UnitTypeId.Meters);
            bool viaTeto = caminho == CaminhoConduite.Teto
                || (caminho == CaminhoConduite.Ambos && Math.Abs(spineZ - loc.Z) > subida);

            // Ponto que a rota mira ao SAIR do dispositivo.
            var aim = viaTeto
                ? new XYZ(loc.X, loc.Y, spineZ)                  // sobe à espinha
                : new XYZ(outroPonto.X, outroPonto.Y, loc.Z);    // corre na horizontal

            var centro = CentroDe(e);

            Connector? best = null;
            double bestScore = double.MaxValue;
            Connector? fallback = null;

            foreach (Connector c in manager.Connectors)
            {
                if (c.IsConnected) continue;
                if (c.Domain != Domain.DomainCableTrayConduit) continue;
                fallback ??= c;

                var eixo = AxisOut(c); // eixo EXATO apontando para fora

                // Descarta conectores cujo eixo aponta para dentro do dispositivo.
                var paraFora = c.Origin - centro;
                if (!paraFora.IsZeroLength() && eixo.DotProduct(paraFora.Normalize()) < -0.3) continue;

                var paraAim = aim - c.Origin;
                var alinhamento = paraAim.IsZeroLength() ? 0.0 : eixo.DotProduct(paraAim.Normalize());

                // Menor score = melhor. Forte preferência por conectores AXIAIS (para
                // o stub + rota fecharem a 90°) e que apontem para o alvo da rota.
                var score = c.Origin.DistanceTo(aim)
                          - alinhamento * 5.0
                          - Axialidade(eixo) * 12.0;
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

        /// <summary>Maior diâmetro (pés) entre os conectores redondos das pontas; 0 se nenhum.</summary>
        private static double DiametroConectorFeet(Connector? a, Connector? b)
        {
            double d = 0;
            foreach (var c in new[] { a, b })
            {
                if (c == null) continue;
                try
                {
                    if (c.Shape == ConnectorProfileType.Round)
                        d = Math.Max(d, c.Radius * 2);
                }
                catch { /* conector sem perfil definido */ }
            }
            return d;
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
