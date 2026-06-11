using System.Collections.Generic;
using System.Windows;

namespace DmEletrico.UI.Circuits
{
    public partial class DmPanelPickerWindow : Window
    {
        public IReadOnlyList<PanelOption> Paineis { get; }
        public PanelOption? Selecionado { get; set; }

        public DmPanelPickerWindow(IReadOnlyList<PanelOption> paineis)
        {
            Paineis = paineis;
            Selecionado = paineis.Count > 0 ? paineis[0] : null;
            DataContext = this;
            InitializeComponent();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Selecionado = Combo.SelectedItem as PanelOption;
            DialogResult = Selecionado != null;
            Close();
        }
    }
}
