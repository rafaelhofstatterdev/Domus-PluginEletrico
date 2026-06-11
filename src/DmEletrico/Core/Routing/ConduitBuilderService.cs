using System;
using System.Collections.Generic;
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
        public List<string> Avisos { get; } = new();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Circuitos processados: {CircuitosProcessados}");
            sb.AppendLine($"Conduítes criados: {ConduitesCriados}");
            sb.AppendLine($"Curvas (fittings) criadas: {CurvasCriadas}");
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
    /// Núcleo do Conduit Builder (requisito 3). Para cada ElectricalSystem do
    /// modelo, traça o caminho ortogonal entre cada dispositivo e o painel de
    /// origem, cria os Conduit e ConduitFitting, dimensiona pela NBR 5410 via
    /// <see cref="ElectricalCalculator"/> e grava os parâmetros Dm_ em cada trecho.
    ///
    /// Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public sealed class ConduitBuilderService
    {
        private const double Tol = 1e-6;
        private readonly ElectricalCalculator _calc = new();

        public ConduitBuildReport Build(Document doc, DmProjectSettings settings)
        {
            var report = new ConduitBuildReport();

            var conduitTypeId = new FilteredElementCollector(doc)
                .OfClass(typeof(ConduitType))
                .FirstElementId();

            if (conduitTypeId == ElementId.InvalidElementId)
            {
                report.Avisos.Add("Nenhum tipo de conduíte (ConduitType) carregado no projeto.");
                return report;
            }

            var systems = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .ToList();

            var spineFeet = UnitUtils.ConvertToInternalUnits(settings.AlturaRoteamento, UnitTypeId.Meters);

            foreach (var system in systems)
            {
                try
                {
                    ProcessSystem(doc, settings, system, conduitTypeId, spineFeet, report);
                }
                catch (Exception ex)
                {
                    report.Avisos.Add($"Circuito {system.Id}: {ex.Message}");
                }
            }

            return report;
        }

        private void ProcessSystem(
            Document doc,
            DmProjectSettings settings,
            ElectricalSystem system,
            ElementId conduitTypeId,
            double spineFeet,
            ConduitBuildReport report)
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
            if (dispositivos.Count == 0)
                return;

            report.CircuitosProcessados++;

            // --- Dimensionamento do circuito (NBR 5410) ---
            var potenciaVa = dispositivos.Sum(d => ReadDouble(d, DmParameters.Potencia));
            var poles = Math.Max(1, (int)ReadDouble(dispositivos[0], DmParameters.NumeroPolos, fallback: 1));
            var nCondutores = poles + 2; // fases + neutro + terra (estimativa v1)

            var levelId = ResolveLevelId(doc, panel);
            var levelElev = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;
            var spineZ = levelElev + spineFeet;

            foreach (var dispositivo in dispositivos)
            {
                var devPt = ElectricalConnectorOrigin(dispositivo) ?? LocationOf(dispositivo);
                if (devPt == null)
                {
                    report.Avisos.Add($"Dispositivo {dispositivo.Id}: sem conector elétrico/localização.");
                    continue;
                }

                var caminho = OrthogonalRouter.Route(devPt, panelPt, spineZ);
                var trechoLenFeet = ComprimentoTotal(caminho);
                var trechoLenM = UnitUtils.ConvertFromInternalUnits(trechoLenFeet, UnitTypeId.Meters);

                var resultado = _calc.Calcular(new TrechoInput
                {
                    ComprimentoM = trechoLenM,
                    PotenciaAparenteVa = potenciaVa,
                    TensaoNominalV = settings.TensaoNominal,
                    TemperaturaAmbienteC = settings.TemperaturaAmbiente,
                    CircuitosAgrupados = 1 // TODO: detectar agrupamento real para o FCA
                });

                var diametroMm = ConduitSizing.DiametroNominal(resultado.SecaoAdotadaMm2, nCondutores);

                var conduits = CriarConduites(doc, conduitTypeId, levelId, caminho, report);
                CriarCurvas(doc, conduits, report);
                AplicarParametros(doc, conduits, resultado, potenciaVa, diametroMm);
            }
        }

        // --- Criação de geometria ---

        private List<Conduit> CriarConduites(
            Document doc, ElementId typeId, ElementId levelId, IList<XYZ> caminho, ConduitBuildReport report)
        {
            var conduits = new List<Conduit>();
            for (int i = 0; i < caminho.Count - 1; i++)
            {
                var a = caminho[i];
                var b = caminho[i + 1];
                if (a.DistanceTo(b) <= Tol) continue;

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
                var c1 = conduits[i];
                var c2 = conduits[i + 1];

                // Ponto comum entre os dois trechos consecutivos.
                var juncao = PontoComum(c1, c2);
                if (juncao == null) continue;

                var con1 = FindConnectorAt(c1, juncao);
                var con2 = FindConnectorAt(c2, juncao);
                if (con1 == null || con2 == null) continue;

                try
                {
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
            Document doc, List<Conduit> conduits, TrechoResultado r, double potenciaVa, double diametroMm)
        {
            var diamFeet = UnitUtils.ConvertToInternalUnits(diametroMm, UnitTypeId.Millimeters);

            foreach (var c in conduits)
            {
                var lenFeet = (c.Location as LocationCurve)?.Curve?.Length ?? 0.0;
                var lenM = UnitUtils.ConvertFromInternalUnits(lenFeet, UnitTypeId.Meters);

                SetDouble(c, DmParameters.Comprimento, UnitUtils.ConvertToInternalUnits(lenM, UnitTypeId.Meters));
                SetDouble(c, DmParameters.PotenciaAparente, potenciaVa);
                SetDouble(c, DmParameters.CorrenteProjeto, r.CorrenteProjetoA);
                SetDouble(c, DmParameters.Fct, r.Fct);
                SetDouble(c, DmParameters.Fca, r.Fca);
                SetDouble(c, DmParameters.SecaoAdotada, r.SecaoAdotadaMm2);
                SetDouble(c, DmParameters.QuedaTensao, r.QuedaTensaoPercent);
                SetDouble(c, DmParameters.DiametroNominal, diamFeet);
            }
        }

        // --- Helpers de geometria/conector ---

        private static double ComprimentoTotal(IList<XYZ> pts)
        {
            double total = 0;
            for (int i = 0; i < pts.Count - 1; i++)
                total += pts[i].DistanceTo(pts[i + 1]);
            return total;
        }

        private static XYZ? PontoComum(Conduit a, Conduit b)
        {
            var ca = EndPoints(a);
            var cb = EndPoints(b);
            foreach (var pa in ca)
                foreach (var pb in cb)
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
            {
                if (con.Domain == Domain.DomainElectrical)
                    return con.Origin;
            }
            return null;
        }

        private static XYZ? LocationOf(Element e)
            => (e.Location as LocationPoint)?.Point;

        private static ElementId ResolveLevelId(Document doc, Element e)
        {
            if (e.LevelId != null && e.LevelId != ElementId.InvalidElementId)
                return e.LevelId;

            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

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

        private static void SetDouble(Element e, string name, double value)
            => e.LookupParameter(name)?.Set(value);
    }
}
