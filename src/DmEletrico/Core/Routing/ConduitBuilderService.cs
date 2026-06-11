using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DmEletrico.Core.Calculation;

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
    /// Núcleo do Conduit Builder (requisito 3). Para cada ElectricalSystem, traça o
    /// caminho (ortogonal em espinha ou direto) entre cada dispositivo e o painel,
    /// com elevação por ambiente (laje/parede/piso); cria Conduit e ConduitFitting,
    /// dimensiona pela NBR 5410 e grava os parâmetros Dm_. Em seguida, faz um
    /// pós-passe que detecta trechos compartilhados por vários circuitos para
    /// aplicar o FCA real e redimensionar o diâmetro pelo total de condutores.
    ///
    /// Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public sealed class ConduitBuilderService
    {
        private const double Tol = 1e-6;
        private readonly ElectricalCalculator _calc = new();

        /// <summary>Metadados em memória de cada conduíte criado, para o pós-passe.</summary>
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

        /// <summary>Ponto de entrada principal: usa as opções da janela de configuração.</summary>
        public ConduitBuildReport Build(Document doc, DmProjectSettings settings, ConduitBuildOptions options)
        {
            if (options.Modo == ModoSelecao.DispositivosSelecionados)
                return BuildBetweenDevices(doc, settings, options);

            var systems = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .ToList();
            return BuildForSystems(doc, settings, systems, options);
        }

        /// <summary>Constrói os conduítes apenas para os circuitos informados (usado pelo Route Fit).</summary>
        public ConduitBuildReport BuildForSystems(
            Document doc, DmProjectSettings settings, IEnumerable<ElectricalSystem> systems, ConduitBuildOptions? options = null)
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

            foreach (var system in systems)
            {
                try
                {
                    ProcessSystem(doc, settings, system, tetoId, paredeId, options, report, metas);
                }
                catch (Exception ex)
                {
                    report.Avisos.Add($"Circuito {system.Id}: {ex.Message}");
                }
            }

            AgregarTrechosCompartilhados(metas, report);
            return report;
        }

        /// <summary>
        /// Modo "dispositivos selecionados": conecta em sequência apenas os
        /// dispositivos escolhidos (ex.: dois dispositivos), sem passar pelo QDC.
        /// </summary>
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

            var pontos = options.Dispositivos
                .Select(id => doc.GetElement(id))
                .Where(e => e != null)
                .Select(e => (elem: e, pt: ElectricalConnectorOrigin(e) ?? LocationOf(e)))
                .Where(x => x.pt != null)
                .ToList();

            if (pontos.Count < 2)
            {
                report.Avisos.Add("Selecione ao menos dois dispositivos com conector/localização.");
                return report;
            }

            var levelId = ResolveLevelId(doc, pontos[0].elem);
            var levelElev = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;
            var offsetFeet = UnitUtils.ConvertToInternalUnits(settings.OffsetPorAmbiente(AmbienteInstalacao.Teto), UnitTypeId.Meters);
            var spineZ = levelElev + offsetFeet;

            for (int i = 0; i < pontos.Count - 1; i++)
            {
                var caminho = EscolherCaminho(pontos[i].pt!, pontos[i + 1].pt!, spineZ, options, settings);

                var conduits = CriarConduites(doc, tetoId, paredeId, levelId, caminho, report);
                CriarCurvas(doc, conduits, report);
                ConectarPontas(conduits, pontos[i].elem, caminho[0], pontos[i + 1].elem, caminho[caminho.Count - 1], report);

                var diam = options.DiametroForcadoMm > 0 ? options.DiametroForcadoMm : 25;
                var diamFeet = UnitUtils.ConvertToInternalUnits(diam, UnitTypeId.Millimeters);
                foreach (var c in conduits)
                    SetDouble(c, DmParameters.DiametroNominal, diamFeet);
            }

            return report;
        }

        private void ProcessSystem(
            Document doc, DmProjectSettings settings, ElectricalSystem system,
            ElementId tetoId, ElementId paredeId, ConduitBuildOptions options,
            ConduitBuildReport report, List<ConduitMeta> metas)
        {
            var panel = system.BaseEquipment;
            if (panel == null)
            {
                report.Avisos.Add($"Circuito {system.Id}: sem painel de origem (BaseEquipment).");
                return;
            }

            var panelPt = ElectricalConnectorOrigin(panel) ?? LocationOf(panel);
            if (panelPt == null)
            {
                report.Avisos.Add($"Circuito {system.Id}: não foi possível localizar o conector do painel.");
                return;
            }

            var dispositivos = system.Elements.Cast<Element>().Where(e => e.Id != panel.Id).ToList();
            if (dispositivos.Count == 0) return;

            report.CircuitosProcessados++;

            var potenciaVa = dispositivos.Sum(d => ReadDouble(d, DmParameters.Potencia));
            var poles = Math.Max(1, (int)ReadDouble(dispositivos[0], DmParameters.NumeroPolos, fallback: 1));
            var nCondutores = poles + 2; // fases + neutro + terra
            var circuitoNumero = string.IsNullOrWhiteSpace(system.CircuitNumber) ? system.Id.ToString() : system.CircuitNumber;

            var levelId = ResolveLevelId(doc, panel);
            var levelElev = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;

            // Disjuntor do circuito (gravado nos dispositivos).
            var correnteCirc = settings.TensaoNominal > 0 ? potenciaVa / settings.TensaoNominal : 0;
            var disjuntor = Nbr5410Tables.DisjuntorComercial(correnteCirc);
            foreach (var d in dispositivos)
                d.LookupParameter(DmParameters.Disjuntor)?.Set(disjuntor);

            foreach (var dispositivo in dispositivos)
            {
                var devPt = ElectricalConnectorOrigin(dispositivo) ?? LocationOf(dispositivo);
                if (devPt == null)
                {
                    report.Avisos.Add($"Dispositivo {dispositivo.Id}: sem conector elétrico/localização.");
                    continue;
                }

                var ambiente = LerAmbiente(dispositivo);
                var offsetFeet = UnitUtils.ConvertToInternalUnits(settings.OffsetPorAmbiente(ambiente), UnitTypeId.Meters);
                var spineZ = levelElev + offsetFeet;

                var caminho = EscolherCaminho(devPt, panelPt, spineZ, options, settings);

                var trechoLenM = UnitUtils.ConvertFromInternalUnits(ComprimentoTotal(caminho), UnitTypeId.Meters);

                var r = _calc.Calcular(new TrechoInput
                {
                    ComprimentoM = trechoLenM,
                    PotenciaAparenteVa = potenciaVa,
                    TensaoNominalV = settings.TensaoNominal,
                    TemperaturaAmbienteC = settings.TemperaturaAmbiente,
                    CircuitosAgrupados = 1 // ajustado no pós-passe (FCA real)
                });

                var diametroMm = options.DiametroForcadoMm > 0
                    ? options.DiametroForcadoMm
                    : ConduitSizing.DiametroNominal(r.SecaoAdotadaMm2, nCondutores);
                var conduits = CriarConduites(doc, tetoId, paredeId, levelId, caminho, report);
                CriarCurvas(doc, conduits, report);
                ConectarPontas(conduits, dispositivo, caminho[0], panel, caminho[caminho.Count - 1], report);
                AplicarParametros(doc, conduits, r, potenciaVa, diametroMm, nCondutores, poles,
                    system.Id.ToString(), dispositivo.Id.ToString(), circuitoNumero);

                foreach (var c in conduits)
                    metas.Add(new ConduitMeta
                    {
                        Conduit = c,
                        SegKey = SegKey(c),
                        CircuitoId = system.Id.ToString(),
                        CircuitoNumero = circuitoNumero,
                        NumCondutores = nCondutores,
                        Secao = r.SecaoAdotadaMm2,
                        Corrente = r.CorrenteProjetoA,
                        Fct = r.Fct
                    });
            }
        }

        /// <summary>
        /// Pós-passe: trechos coincidentes de circuitos distintos compartilham o
        /// eletroduto. Aplica o FCA pelo número de circuitos e redimensiona seção e
        /// diâmetro pelo total de condutores no trecho.
        /// </summary>
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
                    var secao = m.Fct * fca > 0
                        ? Nbr5410Tables.SecaoPorCapacidade(m.Corrente / (m.Fct * fca))
                        : m.Secao;
                    secaoGovernante = Math.Max(secaoGovernante, secao);
                    m.Secao = secao;
                }

                var diamMm = ConduitSizing.DiametroNominal(secaoGovernante, totalCondutores);
                var diamFeet = UnitUtils.ConvertToInternalUnits(diamMm, UnitTypeId.Millimeters);

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

        /// <summary>
        /// Conecta fisicamente as pontas do ramal: o início ao conector do
        /// dispositivo de origem e o fim ao conector do destino (outro dispositivo
        /// ou painel). Best-effort — se os conectores forem incompatíveis, mantém a
        /// coincidência geométrica sem lançar erro.
        /// </summary>
        private void ConectarPontas(List<Conduit> conduits, Element? origem, XYZ ptOrigem, Element? destino, XYZ ptDestino, ConduitBuildReport report)
        {
            if (conduits.Count == 0) return;
            if (TentarConectar(conduits[0], ptOrigem, origem)) report.Conexoes++;
            if (TentarConectar(conduits[conduits.Count - 1], ptDestino, destino)) report.Conexoes++;
        }

        private static bool TentarConectar(Conduit conduit, XYZ ponto, Element? alvo)
        {
            if (alvo == null) return false;
            var cc = FindConnectorAt(conduit, ponto);
            var dc = ConectorMaisProximo(alvo, ponto);
            if (cc == null || dc == null || cc.IsConnected || dc.IsConnected) return false;
            try { cc.ConnectTo(dc); return true; }
            catch { return false; }
        }

        private static Connector? ConectorMaisProximo(Element e, XYZ ponto)
        {
            var manager = (e as FamilyInstance)?.MEPModel?.ConnectorManager
                          ?? (e as MEPCurve)?.ConnectorManager;
            if (manager == null) return null;

            Connector? melhor = null;
            double melhorDist = double.MaxValue;
            foreach (Connector c in manager.Connectors)
            {
                var d = c.Origin.DistanceTo(ponto);
                if (d < melhorDist) { melhorDist = d; melhor = c; }
            }
            return melhor;
        }

        /// <summary>Escolhe o caminho (parede/teto/ambos) conforme as opções.</summary>
        private static IList<XYZ> EscolherCaminho(XYZ a, XYZ b, double spineZ, ConduitBuildOptions options, DmProjectSettings settings)
        {
            if (options.AnguloPlanta == AnguloPlanta.Livre || settings.Modo == ModoRoteamento.Direto)
                return OrthogonalRouter.RouteDireto(a, b);

            switch (options.Caminho)
            {
                case CaminhoConduite.Parede:
                    return OrthogonalRouter.RouteParede(a, b);
                case CaminhoConduite.Teto:
                    return OrthogonalRouter.RouteTeto(a, b, spineZ);
                default: // Ambos → mais curto
                    var parede = OrthogonalRouter.RouteParede(a, b);
                    var teto = OrthogonalRouter.RouteTeto(a, b, spineZ);
                    return OrthogonalRouter.Comprimento(parede) <= OrthogonalRouter.Comprimento(teto) ? parede : teto;
            }
        }

        // --- Criação de geometria ---

        private List<Conduit> CriarConduites(
            Document doc, ElementId tetoId, ElementId paredeId, ElementId levelId, IList<XYZ> caminho, ConduitBuildReport report)
        {
            var conduits = new List<Conduit>();
            for (int i = 0; i < caminho.Count - 1; i++)
            {
                var a = caminho[i];
                var b = caminho[i + 1];
                if (a.DistanceTo(b) <= Tol) continue;

                // Trecho predominantemente vertical → tipo de parede; senão teto/piso.
                var vertical = System.Math.Abs(b.Z - a.Z) > System.Math.Abs(b.X - a.X) + System.Math.Abs(b.Y - a.Y);
                var typeId = vertical && paredeId != ElementId.InvalidElementId ? paredeId : tetoId;

                var conduit = Conduit.Create(doc, typeId, a, b, levelId);
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
                    // NewElbowFitting seleciona a família de curva conforme as
                    // Routing Preferences do ConduitType escolhido no Setup — assim
                    // as preferências de roteamento do template são respeitadas.
                    doc.Create.NewElbowFitting(con1, con2);
                    report.CurvasCriadas++;
                }
                catch
                {
                    // Trecho colinear ou geometria não suportada: sem curva.
                }
            }
        }

        private void AplicarParametros(
            Document doc, List<Conduit> conduits, TrechoResultado r, double potenciaVa,
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

                // Fiação (consumido pela família de anotação em_smb_fiação).
                SetInt(c, DmParameters.NumFases, nFases);
                SetInt(c, DmParameters.NumNeutros, 1);
                SetInt(c, DmParameters.NumTerras, 1);
                SetInt(c, DmParameters.NumRetornos, 0);
                SetDouble(c, DmParameters.BitolaFase, r.SecaoAdotadaMm2);
                SetDouble(c, DmParameters.BitolaTerra, r.SecaoAdotadaMm2);
            }
        }

        // --- Helpers de geometria/conector ---

        private static string SegKey(Conduit c)
        {
            var curve = (c.Location as LocationCurve)?.Curve;
            if (curve == null) return Guid.NewGuid().ToString();
            var p = curve.GetEndPoint(0);
            var q = curve.GetEndPoint(1);
            string a = Key(p), b = Key(q);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static string Key(XYZ p) => string.Format(CultureInfo.InvariantCulture,
            "{0:F3},{1:F3},{2:F3}", p.X, p.Y, p.Z);

        private static double ComprimentoTotal(IList<XYZ> pts)
        {
            double total = 0;
            for (int i = 0; i < pts.Count - 1; i++) total += pts[i].DistanceTo(pts[i + 1]);
            return total;
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

        private static Connector? FindConnectorAt(MEPCurve conduit, XYZ pt)
        {
            var manager = conduit.ConnectorManager;
            if (manager == null) return null;

            Connector? best = null;
            double bestDist = double.MaxValue;
            foreach (Connector con in manager.Connectors)
            {
                var d = con.Origin.DistanceTo(pt);
                if (d < bestDist) { bestDist = d; best = con; }
            }
            return bestDist <= 1e-3 ? best : null;
        }

        private static XYZ? ElectricalConnectorOrigin(Element e)
        {
            var manager = (e as FamilyInstance)?.MEPModel?.ConnectorManager
                          ?? (e as MEPCurve)?.ConnectorManager;
            if (manager == null) return null;

            foreach (Connector con in manager.Connectors)
                if (con.Domain == Domain.DomainElectrical) return con.Origin;
            return null;
        }

        private static XYZ? LocationOf(Element e) => (e.Location as LocationPoint)?.Point;

        private static AmbienteInstalacao LerAmbiente(Element e)
        {
            var s = e.LookupParameter(DmParameters.Ambiente)?.AsString();
            return Enum.TryParse<AmbienteInstalacao>(s, ignoreCase: true, out var a) ? a : AmbienteInstalacao.Teto;
        }

        private static ElementId ResolveLevelId(Document doc, Element e)
        {
            if (e.LevelId != null && e.LevelId != ElementId.InvalidElementId)
                return e.LevelId;

            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation).FirstOrDefault();
            return level?.Id ?? ElementId.InvalidElementId;
        }

        // --- Helpers de parâmetro ---

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
