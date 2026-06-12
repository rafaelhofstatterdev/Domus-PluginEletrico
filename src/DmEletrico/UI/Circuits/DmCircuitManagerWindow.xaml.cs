using System.Windows;
using Autodesk.Revit.DB;
using DmEletrico.Core.Circuits;

namespace DmEletrico.UI.Circuits
{
    public partial class DmCircuitManagerWindow : Window
    {
        private readonly Document _doc;

        public DmCircuitManagerWindow(Document doc)
        {
            _doc = doc;
            DataContext = new DmCircuitManagerViewModel(doc);
            InitializeComponent();
        }

        private CircuitRow? Selecionado() => Grid.SelectedItem as CircuitRow;

        private void OnReatribuir(object sender, RoutedEventArgs e)
        {
            var row = Selecionado();
            if (row == null) { Status.Text = "Selecione um circuito."; return; }

            var vm = (DmCircuitManagerViewModel)DataContext;
            var picker = new DmPanelPickerWindow(vm.Paineis);
            if (picker.ShowDialog() != true || picker.Selecionado == null) return;

            var painel = (FamilyInstance)_doc.GetElement(picker.Selecionado.Id);
            CircuitService.Reassign(_doc, row.DeviceIds, painel);
            Status.Text = $"Circuito reatribuído a {painel.Name}.";
            Recarregar();
        }

        private void OnRenumerar(object sender, RoutedEventArgs e)
        {
            var row = Selecionado();
            if (row == null) { Status.Text = "Selecione um circuito."; return; }
            if (string.IsNullOrWhiteSpace(NovoNumero.Text)) { Status.Text = "Informe o novo número."; return; }

            CircuitService.SetNumero(_doc, row.DeviceIds, NovoNumero.Text.Trim());
            Status.Text = "Circuito renumerado (Dm_NumeroCircuito).";
            Recarregar();
        }

        private void OnBalancear(object sender, RoutedEventArgs e)
        {
            var report = new PhaseBalanceService().Balance(_doc);
            Status.Text = $"Balanceamento: {report.CircuitosBalanceados} circuito(s).";
            Recarregar();
        }

        private void OnAtualizar(object sender, RoutedEventArgs e) => Recarregar();

        private void Recarregar() => DataContext = new DmCircuitManagerViewModel(_doc);
    }
}
