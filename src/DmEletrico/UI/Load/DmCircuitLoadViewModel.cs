using System.Collections.Generic;

namespace DmEletrico.UI.Load
{
    /// <summary>
    /// ViewModel da atribuição de carga (requisito 6). Configura potência, número
    /// de polos, tensão de operação e tipo do circuito para os dispositivos
    /// selecionados.
    /// </summary>
    public sealed class DmCircuitLoadViewModel
    {
        public int QuantidadeSelecionada { get; }

        public double Potencia { get; set; } = 100;   // W/VA
        public int NumeroPolos { get; set; } = 1;
        public double TensaoOperacao { get; set; } = 220; // V
        public string TipoCircuito { get; set; } = "TUG";

        public IReadOnlyList<int> PolosDisponiveis { get; } = new[] { 1, 2, 3 };
        public IReadOnlyList<double> TensoesDisponiveis { get; } = new double[] { 127, 220, 380 };
        public IReadOnlyList<string> TiposDisponiveis { get; } = new[] { "Iluminação", "TUG", "TUE" };

        public DmCircuitLoadViewModel(int quantidadeSelecionada)
        {
            QuantidadeSelecionada = quantidadeSelecionada;
        }

        public string Resumo => $"{QuantidadeSelecionada} dispositivo(s) selecionado(s).";
    }
}
