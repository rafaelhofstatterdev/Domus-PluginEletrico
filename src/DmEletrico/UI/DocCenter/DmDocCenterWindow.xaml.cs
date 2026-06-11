using System.Windows;
using Autodesk.Revit.DB;
using DmEletrico.Core.Documentation;

namespace DmEletrico.UI.DocCenter
{
    public partial class DmDocCenterWindow : Window
    {
        private readonly Document _doc;

        public DmDocCenterWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
        }

        private void OnQuadroCargas(object sender, RoutedEventArgs e)
        {
            var r = DocumentationService.GerarQuadroDeCargas(_doc);
            Status.Text = $"Quadro de cargas '{r.Schedule.Name}' criado.";
        }

        private void OnQuantitativos(object sender, RoutedEventArgs e)
        {
            var r = DocumentationService.GerarQuantitativos(_doc);
            Status.Text = $"Quantitativos gerados: '{r.Conduites.Name}' e '{r.Dispositivos.Name}'.";
        }

        private void OnUnifilar(object sender, RoutedEventArgs e)
        {
            Status.Text = "Diagrama unifilar (módulo 10) em desenvolvimento.";
        }

        private void OnAtualizar(object sender, RoutedEventArgs e)
        {
            DataContext = new DmDocCenterViewModel(_doc);
            Status.Text = "Lista atualizada.";
        }
    }
}
