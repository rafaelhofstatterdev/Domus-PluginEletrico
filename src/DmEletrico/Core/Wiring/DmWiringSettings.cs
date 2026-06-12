using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Wiring
{
    /// <summary>Especificação de um cabo por bitola (descrição comercial + ocultar TAG).</summary>
    public sealed class WireSpec
    {
        public double SecaoMm2 { get; set; }
        public string Descricao { get; set; } = "";
        public bool Ocultar { get; set; }
    }

    /// <summary>
    /// Configuração de Fiação (passos 3/4/5): descrições e bitolas para a lista de
    /// materiais, ocultação de anotações repetitivas, margem de segurança e regras
    /// de neutro/aterramento. Persistida como JSON no ProjectInformation.
    /// </summary>
    public sealed class DmWiringSettings
    {
        // Passo 4 — folga no quantitativo (0,10 = +10%).
        public double MargemSeguranca { get; set; } = 0.0;

        // Passo 5 — neutro e aterramento.
        public bool ForcarNeutroTrifasico { get; set; } = false;
        public bool SepararTerra { get; set; } = false;
        public bool ForcarTerraIluminacao { get; set; } = true;

        // Passo 3 — descrições/ocultação por bitola.
        public List<WireSpec> Especificacoes { get; set; } = PadraoEspecs();

        public WireSpec EspecPara(double secao)
        {
            var e = Especificacoes.FirstOrDefault(x => System.Math.Abs(x.SecaoMm2 - secao) < 0.01);
            return e ?? new WireSpec { SecaoMm2 = secao, Descricao = DescricaoPadrao(secao) };
        }

        public bool Oculta(double secao) => EspecPara(secao).Ocultar;

        private static string DescricaoPadrao(double secao)
            => $"Cabo de cobre flexível 750V {secao:0.##} mm²";

        private static List<WireSpec> PadraoEspecs() =>
            new[] { 1.5, 2.5, 4, 6, 10, 16, 25, 35 }
                .Select(s => new WireSpec { SecaoMm2 = s, Descricao = DescricaoPadrao(s), Ocultar = false })
                .ToList();

        // --- Persistência (JSON no ProjectInformation) ---

        public static DmWiringSettings Read(Document doc)
        {
            var json = doc.ProjectInformation.LookupParameter(Core.DmParameters.WiringConfig)?.AsString();
            if (string.IsNullOrWhiteSpace(json)) return new DmWiringSettings();
            try { return JsonSerializer.Deserialize<DmWiringSettings>(json) ?? new DmWiringSettings(); }
            catch { return new DmWiringSettings(); }
        }

        /// <summary>Persiste no ProjectInformation. Requer transação aberta.</summary>
        public void Write(Document doc)
        {
            var json = JsonSerializer.Serialize(this);
            doc.ProjectInformation.LookupParameter(Core.DmParameters.WiringConfig)?.Set(json);
        }
    }
}
