namespace DmEletrico.Core.Calculation
{
    /// <summary>Entradas para o cálculo de um trecho de conduíte.</summary>
    public sealed class TrechoInput
    {
        public double ComprimentoM { get; set; }
        public double PotenciaAparenteVa { get; set; }
        public double TensaoNominalV { get; set; }
        public double TemperaturaAmbienteC { get; set; } = 30.0;
        public int CircuitosAgrupados { get; set; } = 1;
    }

    /// <summary>Resultado do dimensionamento de um trecho, conforme NBR 5410.</summary>
    public sealed class TrechoResultado
    {
        public double CorrenteProjetoA { get; set; }
        public double Fct { get; set; }
        public double Fca { get; set; }
        public double CorrenteCorrigidaA { get; set; }
        public double SecaoAdotadaMm2 { get; set; }
        public double QuedaTensaoPercent { get; set; }
    }

    /// <summary>
    /// Motor de cálculo elétrico (requisito 5). Aplica a sequência:
    ///   I_projeto = VA / V  →  I_corrigida = I_projeto / (FCT × FCA)
    ///   → seção comercial por capacidade de condução  →  queda de tensão.
    ///
    /// Puro e sem dependência da Revit API para permitir testes unitários.
    /// </summary>
    public sealed class ElectricalCalculator
    {
        public TrechoResultado Calcular(TrechoInput input)
        {
            var fct = Nbr5410Tables.Fct(input.TemperaturaAmbienteC);
            var fca = Nbr5410Tables.Fca(input.CircuitosAgrupados);

            var iProjeto = input.TensaoNominalV > 0
                ? input.PotenciaAparenteVa / input.TensaoNominalV
                : 0;

            var divisor = fct * fca;
            var iCorrigida = divisor > 0 ? iProjeto / divisor : iProjeto;

            var secao = Nbr5410Tables.SecaoPorCapacidade(iCorrigida);

            // Queda de tensão monofásica: ΔV% = (2 · L · I · ρ) / (S · V) · 100
            var quedaPercent = (secao > 0 && input.TensaoNominalV > 0)
                ? (2.0 * input.ComprimentoM * iProjeto * Nbr5410Tables.ResistividadeCobre)
                  / (secao * input.TensaoNominalV) * 100.0
                : 0;

            return new TrechoResultado
            {
                CorrenteProjetoA = iProjeto,
                Fct = fct,
                Fca = fca,
                CorrenteCorrigidaA = iCorrigida,
                SecaoAdotadaMm2 = secao,
                QuedaTensaoPercent = quedaPercent
            };
        }
    }
}
