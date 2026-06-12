using System.Collections.ObjectModel;
using DmEletrico.Core.Wiring;

namespace DmEletrico.UI.Wiring
{
    /// <summary>ViewModel da janela "Configuração de Fiação".</summary>
    public sealed class DmWiringConfigViewModel
    {
        public DmWiringConfigViewModel(DmWiringSettings settings)
        {
            MargemPercent = settings.MargemSeguranca * 100.0;
            ForcarNeutroTrifasico = settings.ForcarNeutroTrifasico;
            SepararTerra = settings.SepararTerra;
            ForcarTerraIluminacao = settings.ForcarTerraIluminacao;
            Especificacoes = new ObservableCollection<WireSpec>(settings.Especificacoes);
        }

        public double MargemPercent { get; set; }
        public bool ForcarNeutroTrifasico { get; set; }
        public bool SepararTerra { get; set; }
        public bool ForcarTerraIluminacao { get; set; }
        public ObservableCollection<WireSpec> Especificacoes { get; }

        public DmWiringSettings ToSettings() => new DmWiringSettings
        {
            MargemSeguranca = MargemPercent / 100.0,
            ForcarNeutroTrifasico = ForcarNeutroTrifasico,
            SepararTerra = SepararTerra,
            ForcarTerraIluminacao = ForcarTerraIluminacao,
            Especificacoes = new System.Collections.Generic.List<WireSpec>(Especificacoes)
        };
    }
}
