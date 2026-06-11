using System.Windows;

namespace DmEletrico.UI.Load
{
    public partial class DmCircuitLoadWindow : Window
    {
        public DmCircuitLoadWindow()
        {
            InitializeComponent();
        }

        private void OnAplicar(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
