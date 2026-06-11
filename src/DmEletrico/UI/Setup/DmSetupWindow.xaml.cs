using System.Windows;

namespace DmEletrico.UI.Setup
{
    public partial class DmSetupWindow : Window
    {
        public DmSetupWindow()
        {
            InitializeComponent();
        }

        private void OnConfirmar(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
