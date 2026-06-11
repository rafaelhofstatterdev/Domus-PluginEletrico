using System.Collections.Generic;
using System.Linq;
using DmEletrico.Core.Routing;
using DmEletrico.UI.Setup;

namespace DmEletrico.UI.Routing
{
    public sealed class DiametroOption
    {
        public string Rotulo { get; }
        public double Valor { get; }
        public DiametroOption(string rotulo, double valor) { Rotulo = rotulo; Valor = valor; }
        public override string ToString() => Rotulo;
    }

    /// <summary>ViewModel da janela "Construir Conduítes".</summary>
    public sealed class DmConduitBuilderViewModel
    {
        public DmConduitBuilderViewModel(IEnumerable<ConduitTypeOption> tiposConduite)
        {
            TiposConduite = tiposConduite.ToList();
            TipoTetoPiso = TiposConduite.FirstOrDefault();
            TipoParede = TiposConduite.FirstOrDefault();
            Diametro = Diametros[0];
        }

        public IReadOnlyList<ModoSelecao> Modos { get; } = new[]
        {
            ModoSelecao.CircuitosCompletos, ModoSelecao.DispositivosSelecionados
        };
        public ModoSelecao Modo { get; set; } = ModoSelecao.CircuitosCompletos;

        public IReadOnlyList<ConduitTypeOption> TiposConduite { get; }
        public ConduitTypeOption? TipoTetoPiso { get; set; }
        public ConduitTypeOption? TipoParede { get; set; }

        public IReadOnlyList<DiametroOption> Diametros { get; } = new[]
        {
            new DiametroOption("Calculado (NBR)", 0),
            new DiametroOption("16 mm", 16), new DiametroOption("20 mm", 20),
            new DiametroOption("25 mm", 25), new DiametroOption("32 mm", 32),
            new DiametroOption("40 mm", 40), new DiametroOption("50 mm", 50),
            new DiametroOption("60 mm", 60), new DiametroOption("75 mm", 75),
        };
        public DiametroOption Diametro { get; set; }

        public IReadOnlyList<AnguloPlanta> AngulosPlanta { get; } = new[]
        {
            AnguloPlanta.Livre, AnguloPlanta.A90, AnguloPlanta.A45
        };
        public AnguloPlanta AnguloPlanta { get; set; } = AnguloPlanta.A90;

        public IReadOnlyList<AnguloParede> AngulosParede { get; } = new[] { AnguloParede.Livre, AnguloParede.A90 };
        public AnguloParede AnguloParede { get; set; } = AnguloParede.A90;

        public IReadOnlyList<OrientacaoConduite> Orientacoes { get; } = new[]
        {
            OrientacaoConduite.Default, OrientacaoConduite.Horizontal, OrientacaoConduite.Vertical
        };
        public OrientacaoConduite Orientacao { get; set; } = OrientacaoConduite.Default;

        public IReadOnlyList<CaminhoConduite> Caminhos { get; } = new[]
        {
            CaminhoConduite.Parede, CaminhoConduite.Teto, CaminhoConduite.Ambos
        };
        public CaminhoConduite Caminho { get; set; } = CaminhoConduite.Ambos;

        public DmConduitBuilderViewModel WithDefaults()
        {
            Diametro = Diametros[0];
            return this;
        }

        public ConduitBuildOptions ToOptions() => new ConduitBuildOptions
        {
            Modo = Modo,
            TipoTetoPisoId = TipoTetoPiso?.Id ?? "",
            TipoParedeId = TipoParede?.Id ?? TipoTetoPiso?.Id ?? "",
            DiametroForcadoMm = Diametro?.Valor ?? 0,
            AnguloPlanta = AnguloPlanta,
            AnguloParede = AnguloParede,
            Orientacao = Orientacao,
            Caminho = Caminho
        };
    }
}
