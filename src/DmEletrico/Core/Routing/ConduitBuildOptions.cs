using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace DmEletrico.Core.Routing
{
    public enum ModoSelecao
    {
        CircuitosCompletos,     // roteia todos os circuitos do modelo até o QDC
        DispositivosSelecionados // conecta apenas os dispositivos escolhidos
    }

    public enum AnguloPlanta { Livre, A90, A45 }
    public enum AnguloParede { Livre, A90 }
    public enum OrientacaoConduite { Default, Horizontal, Vertical }

    /// <summary>
    /// Opções do Conduit Builder definidas na janela de configuração (requisito 3),
    /// no espírito do diálogo "Construir Conduítes".
    /// </summary>
    public sealed class ConduitBuildOptions
    {
        public ModoSelecao Modo { get; set; } = ModoSelecao.CircuitosCompletos;

        /// <summary>Tipo de conduíte para trechos horizontais (teto/piso).</summary>
        public string TipoTetoPisoId { get; set; } = "";
        /// <summary>Tipo de conduíte para trechos verticais (parede).</summary>
        public string TipoParedeId { get; set; } = "";

        /// <summary>Diâmetro forçado (mm); 0 = calculado pela NBR.</summary>
        public double DiametroForcadoMm { get; set; }

        public AnguloPlanta AnguloPlanta { get; set; } = AnguloPlanta.A90;
        public AnguloParede AnguloParede { get; set; } = AnguloParede.A90;
        public OrientacaoConduite Orientacao { get; set; } = OrientacaoConduite.Default;

        /// <summary>Dispositivos a conectar quando Modo = DispositivosSelecionados.</summary>
        public IList<ElementId> Dispositivos { get; set; } = new List<ElementId>();

        public ElementId ResolveTetoPiso(Document doc) => ResolveType(doc, TipoTetoPisoId);
        public ElementId ResolveParede(Document doc) => ResolveType(doc, TipoParedeId);

        private static ElementId ResolveType(Document doc, string id)
        {
            if (long.TryParse(id, out var raw))
            {
                var eid = new ElementId(raw);
                if (doc.GetElement(eid) is ConduitType) return eid;
            }
            return new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).FirstElementId();
        }

        /// <summary>Opções padrão (tipos = primeiro ConduitType disponível).</summary>
        public static ConduitBuildOptions Default(Document doc)
        {
            var first = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).FirstElementId();
            var id = first == ElementId.InvalidElementId ? "" : first.Value.ToString();
            return new ConduitBuildOptions { TipoTetoPisoId = id, TipoParedeId = id };
        }
    }
}
