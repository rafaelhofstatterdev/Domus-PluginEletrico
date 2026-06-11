using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

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
            return $"TAGs criadas: {TagsCriadas}\nTrechos já etiquetados (ignorados): {JaEtiquetados}";
        }
    }

    /// <summary>
    /// Requisito 7 — inserção de TAGs de fiação nos conduítes. As TAGs são
    /// IndependentTag nativos, vinculados ao conduíte: o conteúdo (número do
    /// circuito, seção, traços fase/neutro/terra) é definido pela família de
    /// anotação carregada, que deve exibir os parâmetros Dm_. Por serem nativas,
    /// as TAGs atualizam automaticamente quando o dimensionamento muda.
    ///
    /// Métodos gerenciam a própria transação.
    /// </summary>
    public static class TagService
    {
        public static AutoTagReport AutoTag(Document doc, View view)
        {
            var report = new AutoTagReport();

            if (!ViewSuportaTags(view))
            {
                report.Aviso = "A vista ativa não suporta TAGs. Abra uma planta (ViewPlan).";
                return report;
            }

            var symbol = ConduitTagSymbol(doc);
            if (symbol == null)
            {
                report.Aviso = "Nenhuma família de TAG de conduíte (OST_ConduitTags) carregada no projeto.";
                return report;
            }

            var jaEtiquetados = ConduitesJaEtiquetados(doc, view);

            var conduites = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Conduit)
                .WhereElementIsNotElementType()
                .ToElementIds();

            using var tx = new Transaction(doc, "DmEletrico — Auto TAG");
            tx.Start();

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

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

                CriarTag(doc, view, symbol.Id, new Reference(conduit), ponto);
                report.TagsCriadas++;
            }

            tx.Commit();
            return report;
        }

        /// <summary>TAG manual: insere uma etiqueta no conduíte indicado, no ponto clicado.</summary>
        public static bool ManualTag(Document doc, View view, Reference conduitRef, XYZ ponto)
        {
            if (!ViewSuportaTags(view)) return false;

            var symbol = ConduitTagSymbol(doc);
            if (symbol == null) return false;

            using var tx = new Transaction(doc, "DmEletrico — Manual TAG");
            tx.Start();

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            CriarTag(doc, view, symbol.Id, conduitRef, ponto);
            tx.Commit();
            return true;
        }

        // --- Helpers ---

        private static IndependentTag CriarTag(
            Document doc, View view, ElementId tagTypeId, Reference reference, XYZ ponto)
            => IndependentTag.Create(
                doc, tagTypeId, view.Id, reference,
                addLeader: false, TagOrientation.Horizontal, ponto);

        private static FamilySymbol? ConduitTagSymbol(Document doc)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_ConduitTags)
                .Cast<FamilySymbol>()
                .FirstOrDefault();

        private static HashSet<ElementId> ConduitesJaEtiquetados(Document doc, View view)
        {
            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>();

            var set = new HashSet<ElementId>();
            foreach (var t in tags)
                foreach (var id in t.GetTaggedLocalElementIds())
                    set.Add(id);
            return set;
        }

        private static XYZ? MidPoint(Element conduit)
        {
            var curve = (conduit.Location as LocationCurve)?.Curve;
            return curve?.Evaluate(0.5, true);
        }

        private static bool ViewSuportaTags(View view)
            => view is ViewPlan || view is View3D || view is ViewSection;
    }
}
