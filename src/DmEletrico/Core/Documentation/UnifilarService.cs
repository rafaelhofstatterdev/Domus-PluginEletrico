using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DmEletrico.Core.Calculation;

namespace DmEletrico.Core.Documentation
{
    public sealed class UnifilarReport
    {
        public List<ViewDrafting> Vistas { get; } = new();
        public string? Aviso { get; set; }

        public override string ToString()
        {
            if (Aviso != null) return Aviso;
            if (Vistas.Count == 0) return "Nenhum QDC encontrado para gerar diagrama unifilar.";
            return $"{Vistas.Count} diagrama(s) unifilar(es) gerado(s):\n" +
                   string.Join("\n", Vistas.Select(v => "• " + v.Name));
        }
    }

    /// <summary>
    /// Requisito 10 — Diagrama Unifilar. Gera um ViewDrafting por QDC com o
    /// barramento, o disjuntor geral e um ramal por circuito (número, bitola,
    /// disjuntor e destino). O dimensionamento de cada ramal usa o
    /// <see cref="ElectricalCalculator"/>, de modo que o diagrama acompanha as
    /// cargas atribuídas. Gerencia a própria transação.
    /// </summary>
    public sealed class UnifilarService
    {
        private const double Passo = 1.5;       // ft entre ramais
        private const double RamalLen = 3.0;     // ft do barramento ao texto
        private const double TextoOffset = 0.3;  // ft

        private readonly ElectricalCalculator _calc = new();

        /// <summary>
        /// Regenera os unifilares: apaga as vistas de unifilar do DmEletrico
        /// existentes e gera novamente a partir do estado atual do modelo.
        /// </summary>
        public UnifilarReport Regenerate(Document doc, DmProjectSettings settings)
        {
            var antigas = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>()
                .Where(v => v.Name.StartsWith("DmEletrico - Unifilar"))
                .Select(v => v.Id).ToList();

            using (var tx = new Transaction(doc, "DmEletrico — Limpar Unifilares"))
            {
                tx.Start();
                foreach (var id in antigas)
                    if (doc.GetElement(id) != null) doc.Delete(id);
                tx.Commit();
            }

            return Generate(doc, settings);
        }

        public UnifilarReport Generate(Document doc, DmProjectSettings settings)
        {
            var report = new UnifilarReport();

            var vftId = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting)?.Id;

            if (vftId == null)
            {
                report.Aviso = "Nenhum tipo de vista de desenho (Drafting) disponível no projeto.";
                return report;
            }

            var textTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);

            var paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            var todos = Circuits.LogicalCircuits.All(doc);

            using var tx = new Transaction(doc, "DmEletrico — Diagrama Unifilar");
            tx.Start();

            foreach (var painel in paineis)
            {
                var circuitos = todos.Where(c => c.Quadro == painel.Name).ToList();
                if (circuitos.Count == 0) continue;

                var view = ViewDrafting.Create(doc, vftId);
                view.Name = UniqueViewName(doc, $"DmEletrico - Unifilar {painel.Name}");

                DesenharQuadro(doc, view, textTypeId, painel, circuitos, settings);
                report.Vistas.Add(view);
            }

            tx.Commit();
            return report;
        }

        private void DesenharQuadro(
            Document doc, ViewDrafting view, ElementId textTypeId,
            FamilyInstance painel, List<Circuits.LogicalCircuit> circuitos, DmProjectSettings settings)
        {
            // Cabeçalho + disjuntor geral.
            Texto(doc, view, textTypeId, new XYZ(-RamalLen, Passo, 0), $"QDC: {painel.Name}");
            Texto(doc, view, textTypeId, new XYZ(TextoOffset, 0, 0), "Disjuntor Geral");

            var topo = 0.0;
            var baixo = -circuitos.Count * Passo;

            // Barramento vertical.
            Linha(doc, view, new XYZ(0, topo, 0), new XYZ(0, baixo, 0));

            for (int i = 0; i < circuitos.Count; i++)
            {
                var s = circuitos[i];
                var y = -(i + 1) * Passo;

                // Ramal horizontal.
                Linha(doc, view, new XYZ(0, y, 0), new XYZ(RamalLen, y, 0));

                var (secao, disjuntor) = Dimensionar(s, settings);
                var numero = string.IsNullOrWhiteSpace(s.Numero) ? "?" : s.Numero;
                var texto = string.Format(CultureInfo.CurrentCulture,
                    "Circ {0} | {1:F1} mm² | Disj {2:F0} A",
                    numero, secao, disjuntor);

                Texto(doc, view, textTypeId, new XYZ(RamalLen + TextoOffset, y, 0), texto);
            }
        }

        private (double secao, double disjuntor) Dimensionar(Circuits.LogicalCircuit s, DmProjectSettings settings)
        {
            var potVa = s.Dispositivos.Sum(e => ReadDouble(e, DmParameters.Potencia));
            var r = _calc.Calcular(new TrechoInput
            {
                PotenciaAparenteVa = potVa,
                TensaoNominalV = settings.TensaoNominal,
                TemperaturaAmbienteC = settings.TemperaturaAmbiente,
                ComprimentoM = 0,
                CircuitosAgrupados = 1
            });
            return (r.SecaoAdotadaMm2, Nbr5410Tables.DisjuntorComercial(r.CorrenteProjetoA));
        }

        private static void Linha(Document doc, View view, XYZ a, XYZ b)
            => doc.Create.NewDetailCurve(view, Line.CreateBound(a, b));

        private static void Texto(Document doc, View view, ElementId typeId, XYZ pos, string conteudo)
            => TextNote.Create(doc, view.Id, pos, conteudo, typeId);

        private static string UniqueViewName(Document doc, string baseName)
        {
            var nomes = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Select(v => v.Name)
                .ToHashSet();

            if (!nomes.Contains(baseName)) return baseName;
            for (int i = 2; ; i++)
            {
                var c = $"{baseName} ({i})";
                if (!nomes.Contains(c)) return c;
            }
        }

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
    }
}
