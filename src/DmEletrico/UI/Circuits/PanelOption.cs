using Autodesk.Revit.DB;

namespace DmEletrico.UI.Circuits
{
    /// <summary>Opção de QDC (painel) em diálogos de circuito.</summary>
    public sealed class PanelOption
    {
        public string Nome { get; }
        public ElementId Id { get; }
        public PanelOption(string nome, ElementId id) { Nome = nome; Id = id; }
        public override string ToString() => Nome;
    }
}
