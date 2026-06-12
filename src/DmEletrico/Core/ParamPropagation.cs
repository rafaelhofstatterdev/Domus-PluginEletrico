using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core
{
    /// <summary>
    /// Propaga valores do DmEletrico para os parâmetros NATIVOS da família, casando
    /// pelo nome (palavras-chave), para que a informação apareça no Revit (tags,
    /// tabelas, propriedades) além dos parâmetros Dm_. Best-effort: parâmetros
    /// tipados recebem conversão de unidade; nomes com "dm_" são ignorados.
    /// </summary>
    public static class ParamPropagation
    {
        public static void SetNumero(Element e, double valor, ForgeTypeId unidade, string[] contem, string[] excluir)
        {
            foreach (var p in Candidatos(e, contem, excluir))
            {
                if (p.StorageType != StorageType.Double) continue;
                p.Set(ParaInterno(p, valor, unidade));
            }
        }

        public static void SetInteiro(Element e, int valor, string[] contem, string[] excluir)
        {
            foreach (var p in Candidatos(e, contem, excluir))
            {
                if (p.StorageType == StorageType.Integer) p.Set(valor);
                else if (p.StorageType == StorageType.Double) p.Set(valor);
                else if (p.StorageType == StorageType.String) p.Set(valor.ToString());
            }
        }

        public static void SetTexto(Element e, string valor, string[] contem, string[] excluir)
        {
            foreach (var p in Candidatos(e, contem, excluir))
                if (p.StorageType == StorageType.String) p.Set(valor);
        }

        private static System.Collections.Generic.IEnumerable<Parameter> Candidatos(Element e, string[] contem, string[] excluir)
        {
            foreach (Parameter p in e.Parameters)
            {
                if (p.IsReadOnly) continue;
                var low = (p.Definition?.Name ?? "").ToLowerInvariant();
                if (low.StartsWith("dm_")) continue;
                if (excluir.Any(low.Contains)) continue;
                if (contem.All(low.Contains)) yield return p;
            }
        }

        private static double ParaInterno(Parameter p, double valor, ForgeTypeId unidadePadrao)
        {
            try { return UnitUtils.ConvertToInternalUnits(valor, p.GetUnitTypeId()); }
            catch
            {
                try { return UnitUtils.ConvertToInternalUnits(valor, unidadePadrao); }
                catch { return valor; }
            }
        }
    }
}
