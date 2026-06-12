using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DmEletrico.Core;

namespace DmEletrico.Core.Annotation
{
    public sealed class AutoTagReport
    {
        public int TagsCriadas { get; set; }
        public int JaEtiquetados { get; set; }
        public string? Aviso { get; set; }

        public override string ToString()
        {
            if (Aviso != null) return Aviso;
            return $"Anotações de fiação criadas: {TagsCriadas}\nTrechos já etiquetados (ignorados): {JaEtiquetados}";
        }
    }

    /// <summary>
    /// Requisito 7 — inserção da anotação de fiação nos conduítes. Prefere a
    /// família de anotação genérica "em_smb_fiação - Geral" (fase/neutro/terra,
    /// número do circuito, seção), posicionada como AnnotationSymbol e com os
    /// parâmetros Dm_ copiados do conduíte. Se a família não estiver carregada,
    /// recai numa TAG nativa (IndependentTag de OST_ConduitTags).
    ///
    /// Métodos gerenciam a própria transação.
    /// </summary>
    public static class TagService
    {
        /// <summary>Nome da família genérica de fiação solicitada para as anotações.</summary>
        public const string FamiliaFiacao = "em_smb_fiação - Geral";

        public static AutoTagReport AutoTag(Document doc, View view)
        {
            var report = new AutoTagReport();

            if (!ViewSuportaTags(view))
            {
                report.Aviso = "A vista ativa não suporta anotações. Abra uma planta (ViewPlan).";
                return report;
            }

            var fiacao = FamiliaFiacaoSymbol(doc);
            var tagSymbol = fiacao == null ? ConduitTagSymbol(doc) : null;
            if (fiacao == null && tagSymbol == null)
            {
                report.Aviso = $"Carregue a família de anotação '{FamiliaFiacao}' (ou uma TAG de conduíte) e tente de novo.";
                return report;
            }

            var jaEtiquetados = ConduitesJaEtiquetados(doc, view);
            var ocupados = PosicoesDeTags(doc, view);

            var conduites = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Conduit)
                .WhereElementIsNotElementType()
                .ToElementIds();

            using var tx = new Transaction(doc, "DmEletrico — Auto TAG");
            tx.Start();

            Ativar(doc, fiacao);
            Ativar(doc, tagSymbol);

            foreach (var id in conduites)
            {
                if (jaEtiquetados.Contains(id))
                {
                    report.JaEtiquetados++;
                    continue;
                }

                var conduit = doc.GetElement(id);
                var ponto = MidPoint(conduit);
                if (ponto == null) continue;

                ponto = EvitarSobreposicao(ponto, ocupados);
                ocupados.Add(ponto);

                ColocarAnotacao(doc, view, conduit, ponto, fiacao, tagSymbol);
                report.TagsCriadas++;
            }

            tx.Commit();
            return report;
        }

        /// <summary>Anotação manual: insere a anotação de fiação no conduíte indicado.</summary>
        public static bool ManualTag(Document doc, View view, Reference conduitRef, XYZ ponto)
        {
            if (!ViewSuportaTags(view)) return false;

            var fiacao = FamiliaFiacaoSymbol(doc);
            var tagSymbol = fiacao == null ? ConduitTagSymbol(doc) : null;
            if (fiacao == null && tagSymbol == null) return false;

            var conduit = doc.GetElement(conduitRef);

            using var tx = new Transaction(doc, "DmEletrico — Manual TAG");
            tx.Start();
            Ativar(doc, fiacao);
            Ativar(doc, tagSymbol);
            ColocarAnotacao(doc, view, conduit, ponto, fiacao, tagSymbol);
            tx.Commit();
            return true;
        }

        /// <summary>
        /// Fiação Automática (passo 1): anota os conduítes indicados. Remove primeiro
        /// as anotações de fiação existentes na vista (re-clique = re-alinha) e
        /// recoloca para os conduítes em que <paramref name="incluir"/> é verdadeiro
        /// (permite ocultar bitolas configuradas), com anti-sobreposição.
        /// </summary>
        public static AutoTagReport AnotarFiacao(Document doc, View view, ICollection<ElementId> conduitIds, System.Func<Element, bool> incluir)
        {
            var report = new AutoTagReport();
            if (!ViewSuportaTags(view))
            {
                report.Aviso = "A vista ativa não suporta anotações. Abra uma planta ou vista 3D.";
                return report;
            }
            if (conduitIds.Count == 0)
            {
                report.Aviso = "Selecione conduítes (ou rode com conduítes na vista) para aplicar a fiação.";
                return report;
            }

            var fiacao = FamiliaFiacaoSymbol(doc);
            var tagSymbol = fiacao == null ? ConduitTagSymbol(doc) : null;
            if (fiacao == null && tagSymbol == null)
            {
                report.Aviso = $"Carregue a família de anotação '{FamiliaFiacao}' (ou uma TAG de conduíte).";
                return report;
            }

            using var tx = new Transaction(doc, "DmEletrico — Fiação Automática");
            tx.Start();
            Ativar(doc, fiacao);
            Ativar(doc, tagSymbol);

            // 1) Limpa anotações de fiação existentes na vista (re-alinhamento).
            foreach (var id in AnotacoesDeFiacao(doc, view)) doc.Delete(id);
            doc.Regenerate();

            // 2) Recoloca para os conduítes incluídos.
            var ocupados = new List<XYZ>();
            foreach (var id in conduitIds)
            {
                var conduit = doc.GetElement(id);
                if (conduit == null || !incluir(conduit)) continue;
                var ponto = MidPoint(conduit);
                if (ponto == null) continue;

                ponto = EvitarSobreposicao(ponto, ocupados);
                ocupados.Add(ponto);
                ColocarAnotacao(doc, view, conduit, ponto, fiacao, tagSymbol);
                report.TagsCriadas++;
            }

            tx.Commit();
            return report;
        }

        private static List<ElementId> AnotacoesDeFiacao(Document doc, View view)
        {
            var ids = new List<ElementId>();
            ids.AddRange(new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(f => string.Equals(f.Symbol?.Family?.Name?.Trim(), FamiliaFiacao, System.StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Id));
            ids.AddRange(new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
                .Where(t => t.GetTaggedLocalElementIds().Count > 0)
                .Select(t => t.Id));
            return ids;
        }

        /// <summary>Remove uma anotação/TAG.</summary>
        public static void Remove(Document doc, ElementId tagId)
        {
            using var tx = new Transaction(doc, "DmEletrico — Remover TAG");
            tx.Start();
            if (doc.GetElement(tagId) != null) doc.Delete(tagId);
            tx.Commit();
        }

        // --- Colocação ---

        private static void ColocarAnotacao(
            Document doc, View view, Element conduit, XYZ ponto, FamilySymbol? fiacao, FamilySymbol? tagSymbol)
        {
            if (fiacao != null)
            {
                var inst = doc.Create.NewFamilyInstance(ponto, fiacao, view);
                CopiarParametrosFiacao(conduit, inst);
            }
            else if (tagSymbol != null)
            {
                IndependentTag.Create(doc, tagSymbol.Id, view.Id, new Reference(conduit),
                    addLeader: false, TagOrientation.Horizontal, ponto);
            }
        }

        /// <summary>
        /// Preenche os parâmetros da família de fiação (em_smb_fiação) com os dados
        /// do circuito daquele conduíte: nº de fases/neutros/terras/retornos, bitolas
        /// e número do circuito. Mapeia os nomes nativos da família a partir dos Dm_.
        /// </summary>
        private static void CopiarParametrosFiacao(Element conduit, Element inst)
        {
            // Número do circuito (texto).
            SetTexto(inst, "N_Circuito", conduit.LookupParameter(DmParameters.CircuitosNoTrecho)?.AsString() ?? "");

            // Contagens (inteiros).
            SetInteiro(inst, "N_Fase", LerInt(conduit, DmParameters.NumFases, 1));
            SetInteiro(inst, "N_Neutro", LerInt(conduit, DmParameters.NumNeutros, 1));
            SetInteiro(inst, "N_Terra", LerInt(conduit, DmParameters.NumTerras, 1));
            SetInteiro(inst, "N_Retorno", LerInt(conduit, DmParameters.NumRetornos, 0));

            // Bitolas (mm²).
            var bitFase = LerDouble(conduit, DmParameters.BitolaFase, LerDouble(conduit, DmParameters.SecaoAdotada, 0));
            var bitTerra = LerDouble(conduit, DmParameters.BitolaTerra, bitFase);
            SetNumero(inst, "Bit_Fase", bitFase);
            SetNumero(inst, "Bit_Terra", bitTerra);
        }

        private static int LerInt(Element e, string nome, int padrao)
        {
            var p = e.LookupParameter(nome);
            return p?.StorageType == StorageType.Integer ? p.AsInteger() : padrao;
        }

        private static double LerDouble(Element e, string nome, double padrao)
        {
            var p = e.LookupParameter(nome);
            return p?.StorageType == StorageType.Double ? p.AsDouble() : padrao;
        }

        private static void SetTexto(Element e, string nome, string valor)
        {
            var p = e.LookupParameter(nome);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(valor);
        }

        private static void SetInteiro(Element e, string nome, int valor)
        {
            var p = e.LookupParameter(nome);
            if (p == null || p.IsReadOnly) return;
            if (p.StorageType == StorageType.Integer) p.Set(valor);
            else if (p.StorageType == StorageType.Double) p.Set(valor);
            else if (p.StorageType == StorageType.String) p.Set(valor.ToString());
        }

        private static void SetNumero(Element e, string nome, double valor)
        {
            var p = e.LookupParameter(nome);
            if (p == null || p.IsReadOnly) return;
            if (p.StorageType == StorageType.Double) p.Set(valor);
            else if (p.StorageType == StorageType.String) p.Set(valor.ToString(System.Globalization.CultureInfo.CurrentCulture));
        }

        // --- Símbolos ---

        private static FamilySymbol? FamiliaFiacaoSymbol(Document doc)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => string.Equals(s.Family?.Name?.Trim(), FamiliaFiacao,
                    System.StringComparison.OrdinalIgnoreCase));

        private static FamilySymbol? ConduitTagSymbol(Document doc)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_ConduitTags)
                .Cast<FamilySymbol>()
                .FirstOrDefault();

        private static void Ativar(Document doc, FamilySymbol? symbol)
        {
            if (symbol != null && !symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }
        }

        // --- Anti-sobreposição / dedupe ---

        private const double SobrepTolFeet = 0.8; // ~24 cm

        private static List<XYZ> PosicoesDeTags(Document doc, View view)
        {
            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
                .Select(t => t.TagHeadPosition);
            var anots = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(f => string.Equals(f.Symbol?.Family?.Name?.Trim(), FamiliaFiacao, System.StringComparison.OrdinalIgnoreCase))
                .Select(f => (f.Location as LocationPoint)?.Point)
                .Where(p => p != null)
                .Select(p => p!);
            return tags.Concat(anots).ToList();
        }

        private static XYZ EvitarSobreposicao(XYZ ponto, List<XYZ> ocupados)
        {
            var p = ponto;
            int tentativas = 0;
            while (ocupados.Any(o => o.DistanceTo(p) < SobrepTolFeet) && tentativas < 12)
            {
                p = new XYZ(p.X, p.Y + SobrepTolFeet, p.Z);
                tentativas++;
            }
            return p;
        }

        private static HashSet<ElementId> ConduitesJaEtiquetados(Document doc, View view)
        {
            var set = new HashSet<ElementId>();
            foreach (var t in new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
                foreach (var id in t.GetTaggedLocalElementIds())
                    set.Add(id);
            return set;
        }

        private static XYZ? MidPoint(Element conduit)
            => (conduit.Location as LocationCurve)?.Curve?.Evaluate(0.5, true);

        private static bool ViewSuportaTags(View view)
            => view is ViewPlan || view is View3D || view is ViewSection || view is ViewDrafting;
    }
}
