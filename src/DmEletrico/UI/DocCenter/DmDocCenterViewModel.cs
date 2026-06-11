using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

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
            var sistemas = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .ToList();

            var paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var painel in paineis)
            {
                var circuitos = sistemas
                    .Where(s => s.BaseEquipment != null && s.BaseEquipment.Id == painel.Id)
                    .Select(s => new CircuitoVm(DescreverCircuito(s)))
                    .ToList();

                yield return new QuadroVm($"{painel.Name}  (Id {painel.Id})", circuitos);
            }
        }

        private static string DescreverCircuito(ElectricalSystem s)
        {
            var numero = string.IsNullOrWhiteSpace(s.CircuitNumber) ? "?" : s.CircuitNumber;
            return $"Circuito {numero} — {s.Name}";
        }
    }
}
