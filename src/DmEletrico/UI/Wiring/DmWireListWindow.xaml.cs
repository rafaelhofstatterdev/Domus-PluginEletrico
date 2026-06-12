using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using DmEletrico.Core.Wiring;

namespace DmEletrico.UI.Wiring
{
    public partial class DmWireListWindow : Window
    {
        private readonly List<WireRow> _rows;

        public DmWireListWindow(List<WireRow> rows)
        {
            _rows = rows;
            DataContext = rows;
            InitializeComponent();
        }

        private void OnCopiar(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bitola (mm²)\tDescrição\tMetros\tCom folga");
            foreach (var r in _rows)
                sb.AppendLine($"{r.SecaoMm2:N2}\t{r.Descricao}\t{r.MetrosBase:N1}\t{r.MetrosComFolga:N1}");
            try { Clipboard.SetText(sb.ToString()); } catch { }
        }
    }
}
