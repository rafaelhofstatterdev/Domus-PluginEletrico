using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DmEletrico.Core;

namespace DmEletrico.UI.Circuits
{
    public sealed class CircuitRow
    {
        public ElementId SystemId { get; set; } = ElementId.InvalidElementId;
        public string Numero { get; set; } = "";
        public string Painel { get; set; } = "";
        public string Fase { get; set; } = "";
        public double Carga { get; set; }
        public string CargaStr => $"{Carga:F0} VA";
    }

    /// <summary>
    /// Gerenciador de circuitos (requisito 3). Lista os ElectricalSystem do modelo
    /// com QDC, número, fase e carga, para reorganização.
    /// </summary>
    public sealed class DmCircuitManagerViewModel
    {
        public ObservableCollection<CircuitRow> Circuitos { get; }
        public IReadOnlyList<PanelOption> Paineis { get; }

        public DmCircuitManagerViewModel(Document doc)
        {
            Circuitos = new ObservableCollection<CircuitRow>(Carregar(doc));
            Paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Select(p => new PanelOption(p.Name, p.Id))
                .ToList();
        }

        private static IEnumerable<CircuitRow> Carregar(Document doc)
        {
            var sistemas = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .ToList();

            foreach (var s in sistemas)
            {
                var fase = s.Elements.Cast<Element>()
                    .Select(e => e.LookupParameter(DmParameters.Fase)?.AsString())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "—";

                var carga = s.Elements.Cast<Element>()
                    .Sum(e => e.LookupParameter(DmParameters.Potencia)?.AsDouble() ?? 0);

                yield return new CircuitRow
                {
                    SystemId = s.Id,
                    Numero = string.IsNullOrWhiteSpace(s.CircuitNumber) ? "?" : s.CircuitNumber,
                    Painel = s.BaseEquipment?.Name ?? "(sem QDC)",
                    Fase = fase,
                    Carga = carga
                };
            }
        }
    }
}
