using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DmEletrico.Core;

namespace DmEletrico.UI.Setup
{
    /// <summary>ViewModel do diálogo de Setup (requisito 2).</summary>
    public sealed class DmSetupViewModel : INotifyPropertyChanged
    {
        public DmSetupViewModel(DmProjectSettings settings)
        {
            _temperaturaAmbiente = settings.TemperaturaAmbiente;
            _tensaoNominal = settings.TensaoNominal;
            _metodo = settings.Metodo;
        }

        public IReadOnlyList<double> TensoesDisponiveis { get; } = new double[] { 127, 220, 380 };

        public IReadOnlyList<MetodoInstalacao> MetodosDisponiveis { get; } = new[]
        {
            MetodoInstalacao.Embutido,
            MetodoInstalacao.Aparente,
            MetodoInstalacao.Eletrocalha
        };

        private double _temperaturaAmbiente;
        public double TemperaturaAmbiente
        {
            get => _temperaturaAmbiente;
            set { _temperaturaAmbiente = value; OnPropertyChanged(); }
        }

        private double _tensaoNominal;
        public double TensaoNominal
        {
            get => _tensaoNominal;
            set { _tensaoNominal = value; OnPropertyChanged(); }
        }

        private MetodoInstalacao _metodo;
        public MetodoInstalacao Metodo
        {
            get => _metodo;
            set { _metodo = value; OnPropertyChanged(); }
        }

        public DmProjectSettings ToSettings() => new DmProjectSettings
        {
            TemperaturaAmbiente = TemperaturaAmbiente,
            TensaoNominal = TensaoNominal,
            Metodo = Metodo,
            SetupConcluido = true
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
