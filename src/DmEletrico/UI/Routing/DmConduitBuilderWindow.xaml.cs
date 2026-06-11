using System.Windows;

namespace DmEletrico.UI.Routing
{
    public partial class DmConduitBuilderWindow : Window
    {
        public DmConduitBuilderWindow()
        {
            InitializeComponent();
        }

        private void OnSelecionar(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
