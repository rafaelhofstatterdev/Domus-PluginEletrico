using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using DmEletrico.Core.Circuits;

namespace DmEletrico.UI.DocCenter
{
    public sealed class CircuitoVm
    {
        public string Descricao { get; }
        public CircuitoVm(string descricao) { Descricao = descricao; }
    }

    public sealed class QuadroVm
    {
        public string Nome { get; }
        public ObservableCollection<CircuitoVm> Circuitos { get; }
        public QuadroVm(string nome, IEnumerable<CircuitoVm> circuitos)
        {
            Nome = nome;
            Circuitos = new ObservableCollection<CircuitoVm>(circuitos);
        }
    }

    /// <summary>
    /// ViewModel da Central de Documentação (requisito 8). Lista os QDCs
    /// (OST_ElectricalEquipment) e seus circuitos (ElectricalSystem). Somente
    /// leitura — as ações de geração são disparadas pela janela.
    /// </summary>
    public sealed class DmDocCenterViewModel
    {
        public ObservableCollection<QuadroVm> Quadros { get; }

        public DmDocCenterViewModel(Document doc)
        {
            Quadros = new ObservableCollection<QuadroVm>(Carregar(doc));
        }

        private static IEnumerable<QuadroVm> Carregar(Document doc)
        {
            var circuitos = LogicalCircuits.All(doc);

            var paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var painel in paineis)
            {
                var lista = circuitos
                    .Where(c => c.Quadro == painel.Name)
                    .OrderBy(c => int.TryParse(c.Numero, out var n) ? n : 0)
                    .Select(c => new CircuitoVm($"Circuito {c.Numero} — {c.Dispositivos.Count} dispositivo(s)"))
                    .ToList();

                yield return new QuadroVm($"{painel.Name}  (Id {painel.Id})", lista);
            }
        }
    }
}
