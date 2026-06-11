using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DmEletrico.Core;

namespace DmEletrico.UI.Setup
{
    /// <summary>Opção de tipo de conduíte exibida no Setup.</summary>
    public sealed class ConduitTypeOption
    {
        public string Nome { get; }
        public string Id { get; }
        public ConduitTypeOption(string nome, string id) { Nome = nome; Id = id; }
        public override string ToString() => Nome;
    }

    /// <summary>ViewModel do diálogo de Setup (requisito 2).</summary>
    public sealed class DmSetupViewModel : INotifyPropertyChanged
    {
        public DmSetupViewModel(DmProjectSettings settings, IEnumerable<ConduitTypeOption>? conduitTypes = null)
        {
            _temperaturaAmbiente = settings.TemperaturaAmbiente;
            _tensaoNominal = settings.TensaoNominal;
            _alturaRoteamento = settings.AlturaRoteamento;
            _offsetLaje = settings.OffsetLaje;
            _offsetParede = settings.OffsetParede;
            _offsetContrapiso = settings.OffsetContrapiso;
            _metodo = settings.Metodo;
            _modo = settings.Modo;

            TiposConduite = new List<ConduitTypeOption>(conduitTypes ?? Enumerable.Empty<ConduitTypeOption>());
            _tipoConduite = TiposConduite.FirstOrDefault(t => t.Id == settings.ConduitTypeId)
                            ?? TiposConduite.FirstOrDefault();
        }

        public IReadOnlyList<double> TensoesDisponiveis { get; } = new double[] { 127, 220, 380 };

        public IReadOnlyList<MetodoInstalacao> MetodosDisponiveis { get; } = new[]
        {
            MetodoInstalacao.Embutido, MetodoInstalacao.Aparente, MetodoInstalacao.Eletrocalha
        };

        public IReadOnlyList<ModoRoteamento> ModosDisponiveis { get; } = new[]
        {
            ModoRoteamento.Ortogonal, ModoRoteamento.Direto
        };

        public IReadOnlyList<ConduitTypeOption> TiposConduite { get; }

        private double _temperaturaAmbiente;
        public double TemperaturaAmbiente { get => _temperaturaAmbiente; set { _temperaturaAmbiente = value; OnPropertyChanged(); } }

        private double _tensaoNominal;
        public double TensaoNominal { get => _tensaoNominal; set { _tensaoNominal = value; OnPropertyChanged(); } }

        private double _alturaRoteamento;
        public double AlturaRoteamento { get => _alturaRoteamento; set { _alturaRoteamento = value; OnPropertyChanged(); } }

        private double _offsetLaje;
        public double OffsetLaje { get => _offsetLaje; set { _offsetLaje = value; OnPropertyChanged(); } }

        private double _offsetParede;
        public double OffsetParede { get => _offsetParede; set { _offsetParede = value; OnPropertyChanged(); } }

        private double _offsetContrapiso;
        public double OffsetContrapiso { get => _offsetContrapiso; set { _offsetContrapiso = value; OnPropertyChanged(); } }

        private MetodoInstalacao _metodo;
        public MetodoInstalacao Metodo { get => _metodo; set { _metodo = value; OnPropertyChanged(); } }

        private ModoRoteamento _modo;
        public ModoRoteamento Modo { get => _modo; set { _modo = value; OnPropertyChanged(); } }

        private ConduitTypeOption? _tipoConduite;
        public ConduitTypeOption? TipoConduite { get => _tipoConduite; set { _tipoConduite = value; OnPropertyChanged(); } }

        public DmProjectSettings ToSettings() => new DmProjectSettings
        {
            TemperaturaAmbiente = TemperaturaAmbiente,
            TensaoNominal = TensaoNominal,
            AlturaRoteamento = AlturaRoteamento,
            OffsetLaje = OffsetLaje,
            OffsetParede = OffsetParede,
            OffsetContrapiso = OffsetContrapiso,
            Metodo = Metodo,
            Modo = Modo,
            ConduitTypeId = TipoConduite?.Id ?? "",
            SetupConcluido = true
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
