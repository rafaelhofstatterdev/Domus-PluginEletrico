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

        private static void CopiarParametrosFiacao(Element conduit, Element inst)
        {
            Copiar(conduit, inst, DmParameters.CircuitosNoTrecho, DmParameters.NumeroCircuito);
            Copiar(conduit, inst, DmParameters.SecaoAdotada, DmParameters.SecaoAdotada);
            Copiar(conduit, inst, DmParameters.NumCondutores, DmParameters.NumCondutores);
        }

        private static void Copiar(Element origem, Element destino, string nomeOrigem, string nomeDestino)
        {
            var po = origem.LookupParameter(nomeOrigem);
            var pd = destino.LookupParameter(nomeDestino);
            if (po == null || pd == null || pd.IsReadOnly) return;

            switch (pd.StorageType)
            {
                case StorageType.String: pd.Set(po.StorageType == StorageType.String ? po.AsString() ?? "" : po.AsValueString() ?? ""); break;
                case StorageType.Double: if (po.StorageType == StorageType.Double) pd.Set(po.AsDouble()); break;
                case StorageType.Integer: if (po.StorageType == StorageType.Integer) pd.Set(po.AsInteger()); break;
            }
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
