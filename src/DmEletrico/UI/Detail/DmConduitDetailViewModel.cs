using System;
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

    /// <summary>Representação de um condutor no trecho (para o esquema).</summary>
    public sealed class ConductorMark
    {
        public string Cor { get; }
        public string Rotulo { get; }
        public ConductorMark(string cor, string rotulo) { Cor = cor; Rotulo = rotulo; }
    }

    /// <summary>
    /// ViewModel da janela de detalhamento de um trecho de conduíte (requisito 4).
    /// Lê os parâmetros Dm_ calculados, a tensão do projeto, o mapeamento de
    /// circuitos no trecho e monta a representação esquemática dos condutores.
    /// </summary>
    public sealed class DmConduitDetailViewModel
    {
        public string Titulo { get; }
        public IReadOnlyList<DetailRow> Linhas { get; }
        public IReadOnlyList<ConductorMark> Condutores { get; }

        public DmConduitDetailViewModel(Element conduit)
        {
            Titulo = $"Trecho de conduíte — Id {conduit.Id}";

            var tensao = DmProjectSettings.Read(conduit.Document).TensaoNominal;
            var circuitos = conduit.LookupParameter(DmParameters.CircuitosNoTrecho)?.AsString();

            Linhas = new List<DetailRow>
            {
                new("Comprimento (m)",        Length(conduit, DmParameters.Comprimento, UnitTypeId.Meters)),
                new("Diâmetro nominal (mm)",  Length(conduit, DmParameters.DiametroNominal, UnitTypeId.Millimeters)),
                new("Circuitos no trecho",    string.IsNullOrWhiteSpace(circuitos) ? "—" : circuitos),
                new("Nº de condutores",       Int(conduit, DmParameters.NumCondutores)),
                new("Potência aparente (VA)", Num(conduit, DmParameters.PotenciaAparente)),
                new("Corrente de projeto (A)",Num(conduit, DmParameters.CorrenteProjeto)),
                new("Voltagem (V)",           tensao.ToString("F0", CultureInfo.CurrentCulture)),
                new("FCT",                    Num(conduit, DmParameters.Fct, casas: 3)),
                new("FCA",                    Num(conduit, DmParameters.Fca, casas: 3)),
                new("Seção adotada (mm²)",    Num(conduit, DmParameters.SecaoAdotada)),
                new("Queda de tensão (%)",    Num(conduit, DmParameters.QuedaTensao, casas: 3)),
            };

            Condutores = MontarEsquema(conduit);
        }

        private static IReadOnlyList<ConductorMark> MontarEsquema(Element conduit)
        {
            var p = conduit.LookupParameter(DmParameters.NumCondutores);
            var total = p?.StorageType == StorageType.Integer ? p.AsInteger() : 0;

            var lista = new List<ConductorMark>();
            var fases = Math.Max(1, total - 2); // total = fases + neutro + terra
            for (int i = 0; i < fases; i++) lista.Add(new ConductorMark("#C0392B", "F")); // fase
            lista.Add(new ConductorMark("#2980B9", "N")); // neutro
            lista.Add(new ConductorMark("#27AE60", "T")); // terra
            return lista;
        }

        private static string Num(Element e, string name, int casas = 2)
        {
            var p = e.LookupParameter(name);
            if (p == null || p.StorageType != StorageType.Double) return "—";
            return p.AsDouble().ToString("F" + casas, CultureInfo.CurrentCulture);
        }

        private static string Int(Element e, string name)
        {
            var p = e.LookupParameter(name);
            if (p == null || p.StorageType != StorageType.Integer) return "—";
            var v = p.AsInteger();
            return v == 0 ? "—" : v.ToString(CultureInfo.CurrentCulture);
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
