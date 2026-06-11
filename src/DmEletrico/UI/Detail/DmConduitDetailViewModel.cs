using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using DmEletrico.Core;

namespace DmEletrico.UI.Detail
{
    /// <summary>Uma linha (parâmetro → valor) exibida na janela de detalhamento.</summary>
    public sealed class DetailRow
    {
        public string Parametro { get; }
        public string Valor { get; }
        public DetailRow(string parametro, string valor)
        {
            Parametro = parametro;
            Valor = valor;
        }
    }

    /// <summary>
    /// ViewModel da janela de detalhamento de um trecho de conduíte (requisito 5).
    /// Lê os parâmetros Dm_ calculados e os apresenta com os fatores aplicados,
    /// para rastreabilidade total.
    /// </summary>
    public sealed class DmConduitDetailViewModel
    {
        public string Titulo { get; }
        public IReadOnlyList<DetailRow> Linhas { get; }

        public DmConduitDetailViewModel(Element conduit)
        {
            Titulo = $"Trecho de conduíte — Id {conduit.Id}";

            var linhas = new List<DetailRow>
            {
                new("Comprimento (m)",        Length(conduit, DmParameters.Comprimento, UnitTypeId.Meters)),
                new("Diâmetro nominal (mm)",  Length(conduit, DmParameters.DiametroNominal, UnitTypeId.Millimeters)),
                new("Potência aparente (VA)", Num(conduit, DmParameters.PotenciaAparente)),
                new("Corrente de projeto (A)",Num(conduit, DmParameters.CorrenteProjeto)),
                new("FCT",                    Num(conduit, DmParameters.Fct, casas: 3)),
                new("FCA",                    Num(conduit, DmParameters.Fca, casas: 3)),
                new("Seção adotada (mm²)",    Num(conduit, DmParameters.SecaoAdotada)),
                new("Queda de tensão (%)",    Num(conduit, DmParameters.QuedaTensao, casas: 3)),
            };

            Linhas = linhas;
        }

        private static string Num(Element e, string name, int casas = 2)
        {
            var p = e.LookupParameter(name);
            if (p == null || p.StorageType != StorageType.Double) return "—";
            return p.AsDouble().ToString("F" + casas, CultureInfo.CurrentCulture);
        }

        private static string Length(Element e, string name, ForgeTypeId display)
        {
            var p = e.LookupParameter(name);
            if (p == null || p.StorageType != StorageType.Double) return "—";
            var conv = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), display);
            return conv.ToString("F2", CultureInfo.CurrentCulture);
        }
    }
}
