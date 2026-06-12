using System.Windows;

namespace DmEletrico.UI.Wiring
{
    public partial class DmWiringConfigWindow : Window
    {
        public DmWiringConfigWindow()
        {
            InitializeComponent();
        }

        private void OnSalvar(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
