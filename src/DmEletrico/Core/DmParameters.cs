using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DmEletrico.Core
{
    /// <summary>
    /// Catálogo central dos parâmetros compartilhados do DmEletrico.
    ///
    /// Todos seguem o prefixo "Dm" exigido no padrão de nomenclatura. O DmSetup
    /// usa este catálogo para criar/garantir os parâmetros no SharedParameterFile
    /// e vinculá-los às categorias apropriadas.
    /// </summary>
    public static class DmParameters
    {
        // --- Parâmetros de trecho de conduíte (resultado do motor de cálculo) ---
        public const string Comprimento = "Dm_Comprimento";          // m
        public const string PotenciaAparente = "Dm_PotenciaAparente"; // VA
        public const string CorrenteProjeto = "Dm_CorrenteProjeto";   // A
        public const string Fct = "Dm_FCT";                           // adimensional
        public const string Fca = "Dm_FCA";                           // adimensional
        public const string SecaoAdotada = "Dm_SecaoAdotada";         // mm²
        public const string QuedaTensao = "Dm_QuedaTensao";           // %
        public const string DiametroNominal = "Dm_DiametroNominal";   // mm
        public const string CircuitoOrigemId = "Dm_CircuitoOrigemId"; // Id do ElectricalSystem que originou o trecho
        public const string DispositivoId = "Dm_DispositivoId";       // Id do dispositivo terminal do ramal
        public const string CircuitosNoTrecho = "Dm_CircuitosNoTrecho"; // números de circuitos que passam (lista)
        public const string NumCondutores = "Dm_NumCondutores";       // total de condutores no trecho
        public const string NumFases = "Dm_NumFases";                 // fases no trecho
        public const string NumNeutros = "Dm_NumNeutros";             // neutros no trecho
        public const string NumTerras = "Dm_NumTerras";               // terras no trecho
        public const string NumRetornos = "Dm_NumRetornos";           // retornos no trecho
        public const string BitolaFase = "Dm_BitolaFase";             // mm² (fase)
        public const string BitolaTerra = "Dm_BitolaTerra";           // mm² (terra)
        public const string NoA = "Dm_NoA";                           // Id do elemento numa ponta da aresta
        public const string NoB = "Dm_NoB";                           // Id do elemento na outra ponta
        public const string FiacaoDetalhe = "Dm_FiacaoDetalhe";       // detalhe por circuito: "num:F,N,T,bit;..."

        // --- Parâmetros de circuito / dispositivo ---
        public const string NumeroCircuito = "Dm_NumeroCircuito";
        public const string Potencia = "Dm_Potencia";                 // W/VA
        public const string NumeroPolos = "Dm_NumeroPolos";
        public const string TensaoOperacao = "Dm_TensaoOperacao";     // V
        public const string Fase = "Dm_Fase";                         // A/B/C
        public const string TipoCircuito = "Dm_TipoCircuito";         // Iluminação/TUG/TUE
        public const string Disjuntor = "Dm_Disjuntor";               // A (corrente nominal do disjuntor)
        public const string Ambiente = "Dm_Ambiente";                 // Teto/Parede/Piso
        public const string Quadro = "Dm_Quadro";                     // nome do QDC ao qual o dispositivo pertence

        // --- Project Information (variáveis globais do Setup) ---
        public const string TemperaturaAmbiente = "Dm_TemperaturaAmbiente"; // °C
        public const string TensaoNominal = "Dm_TensaoNominal";             // V
        public const string MetodoInstalacao = "Dm_MetodoInstalacao";
        public const string AlturaRoteamento = "Dm_AlturaRoteamento";       // m (espinha de conduítes - padrão)
        public const string OffsetLaje = "Dm_OffsetLaje";                   // m
        public const string OffsetParede = "Dm_OffsetParede";               // m
        public const string OffsetContrapiso = "Dm_OffsetContrapiso";       // m
        public const string ModoRoteamento = "Dm_ModoRoteamento";           // Ortogonal/Direto
        public const string ConduitTypeId = "Dm_ConduitTypeId";             // Id do ConduitType escolhido
        public const string WiringConfig = "Dm_WiringConfig";               // JSON da configuração de fiação
        public const string SetupConcluido = "Dm_SetupConcluido";           // Yes/No

        /// <summary>
        /// Descreve um parâmetro a ser injetado: nome, tipo de dado e categorias-alvo.
        /// O grupo "DmEletrico" no arquivo de parâmetros compartilhados agrupa todos.
        /// </summary>
        public sealed class Definition
        {
            public string Name { get; }
            public ForgeTypeId DataType { get; }
            public IReadOnlyList<BuiltInCategory> Categories { get; }
            public bool IsInstance { get; }

            public Definition(string name, ForgeTypeId dataType, bool isInstance, params BuiltInCategory[] categories)
            {
                Name = name;
                DataType = dataType;
                IsInstance = isInstance;
                Categories = categories;
            }
        }

        /// <summary>Conjunto completo de parâmetros que o DmSetup deve garantir no modelo.</summary>
        public static IReadOnlyList<Definition> All()
        {
            var conduit = new[] { BuiltInCategory.OST_Conduit };
            var electrical = new[]
            {
                BuiltInCategory.OST_ElectricalFixtures,
                BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_ElectricalEquipment
            };

            // Nota: corrente/potência/queda/seção usam SpecTypeId.Number (valor bruto)
            // em vez de specs tipados — evita conversões de unidade interna do Revit
            // e mantém os valores exatamente como calculados, para rastreabilidade.
            // Comprimento e DiâmetroNominal permanecem Length (convertidos no build).
            return new List<Definition>
            {
                // Trecho de conduíte
                new Definition(Comprimento,       SpecTypeId.Length,         isInstance: true, conduit),
                new Definition(PotenciaAparente,  SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(CorrenteProjeto,   SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(Fct,               SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(Fca,               SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(SecaoAdotada,      SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(QuedaTensao,       SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(DiametroNominal,   SpecTypeId.Length,         isInstance: true, conduit),
                new Definition(CircuitoOrigemId,  SpecTypeId.String.Text,    isInstance: true, conduit),
                new Definition(DispositivoId,     SpecTypeId.String.Text,    isInstance: true, conduit),
                new Definition(CircuitosNoTrecho, SpecTypeId.String.Text,    isInstance: true, conduit),
                new Definition(NumCondutores,     SpecTypeId.Int.Integer,    isInstance: true, conduit),
                new Definition(NumFases,          SpecTypeId.Int.Integer,    isInstance: true, conduit),
                new Definition(NumNeutros,        SpecTypeId.Int.Integer,    isInstance: true, conduit),
                new Definition(NumTerras,         SpecTypeId.Int.Integer,    isInstance: true, conduit),
                new Definition(NumRetornos,       SpecTypeId.Int.Integer,    isInstance: true, conduit),
                new Definition(BitolaFase,        SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(BitolaTerra,       SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(NoA,               SpecTypeId.String.Text,    isInstance: true, conduit),
                new Definition(NoB,               SpecTypeId.String.Text,    isInstance: true, conduit),
                new Definition(FiacaoDetalhe,     SpecTypeId.String.Text,    isInstance: true, conduit),

                // Circuito / dispositivo
                new Definition(NumeroCircuito,    SpecTypeId.String.Text,    isInstance: true, electrical),
                new Definition(Potencia,          SpecTypeId.Number,         isInstance: true, electrical),
                new Definition(NumeroPolos,       SpecTypeId.Int.Integer,    isInstance: true, electrical),
                new Definition(TensaoOperacao,    SpecTypeId.Number,         isInstance: true, electrical),
                new Definition(Fase,              SpecTypeId.String.Text,    isInstance: true, electrical),
                new Definition(TipoCircuito,      SpecTypeId.String.Text,    isInstance: true, electrical),
                new Definition(Disjuntor,         SpecTypeId.Number,         isInstance: true, electrical),
                new Definition(Ambiente,          SpecTypeId.String.Text,    isInstance: true, electrical),
                new Definition(Quadro,            SpecTypeId.String.Text,    isInstance: true, electrical),
            };
        }
    }
}
