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

        // --- Parâmetros de circuito / dispositivo ---
        public const string NumeroCircuito = "Dm_NumeroCircuito";
        public const string Potencia = "Dm_Potencia";                 // W/VA
        public const string NumeroPolos = "Dm_NumeroPolos";
        public const string TensaoOperacao = "Dm_TensaoOperacao";     // V
        public const string Fase = "Dm_Fase";                         // A/B/C
        public const string TipoCircuito = "Dm_TipoCircuito";         // Iluminação/TUG/TUE

        // --- Project Information (variáveis globais do Setup) ---
        public const string TemperaturaAmbiente = "Dm_TemperaturaAmbiente"; // °C
        public const string TensaoNominal = "Dm_TensaoNominal";             // V
        public const string MetodoInstalacao = "Dm_MetodoInstalacao";
        public const string AlturaRoteamento = "Dm_AlturaRoteamento";       // m (espinha de conduítes)
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
                new Definition(Comprimento,      SpecTypeId.Length,         isInstance: true, conduit),
                new Definition(PotenciaAparente, SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(CorrenteProjeto,  SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(Fct,              SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(Fca,              SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(SecaoAdotada,     SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(QuedaTensao,      SpecTypeId.Number,         isInstance: true, conduit),
                new Definition(DiametroNominal,  SpecTypeId.Length,         isInstance: true, conduit),

                // Circuito / dispositivo
                new Definition(NumeroCircuito,   SpecTypeId.String.Text,    isInstance: true, electrical),
                new Definition(Potencia,         SpecTypeId.Number,         isInstance: true, electrical),
                new Definition(NumeroPolos,      SpecTypeId.Int.Integer,    isInstance: true, electrical),
                new Definition(TensaoOperacao,   SpecTypeId.Number,         isInstance: true, electrical),
                new Definition(Fase,             SpecTypeId.String.Text,    isInstance: true, electrical),
                new Definition(TipoCircuito,     SpecTypeId.String.Text,    isInstance: true, electrical),
            };
        }
    }
}
