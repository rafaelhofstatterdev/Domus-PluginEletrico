using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using DmEletrico.Core;
using DmEletrico.Core.Circuits;

namespace DmEletrico.UI.Circuits
{
    public sealed class CircuitRow
    {
        public List<ElementId> DeviceIds { get; set; } = new();
        public string Numero { get; set; } = "";
        public string Painel { get; set; } = "";
        public string Fase { get; set; } = "";
        public double Carga { get; set; }
        public string CargaStr => $"{Carga:F0} VA";
    }

    /// <summary>
    /// Gerenciador de circuitos (requisito 3). Lista os circuitos lógicos
    /// (Dm_Quadro + Dm_NumeroCircuito) com QDC, fase e carga, para reorganização.
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
            foreach (var c in LogicalCircuits.All(doc).OrderBy(c => c.Quadro).ThenBy(c => Num(c.Numero)))
            {
                var fase = c.Dispositivos
                    .Select(e => e.LookupParameter(DmParameters.Fase)?.AsString())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "—";

                var carga = c.Dispositivos.Sum(e => e.LookupParameter(DmParameters.Potencia)?.AsDouble() ?? 0);

                yield return new CircuitRow
                {
                    DeviceIds = c.Dispositivos.Select(e => e.Id).ToList(),
                    Numero = string.IsNullOrWhiteSpace(c.Numero) ? "?" : c.Numero,
                    Painel = string.IsNullOrEmpty(c.Quadro) ? "(sem QDC)" : c.Quadro,
                    Fase = fase,
                    Carga = carga
                };
            }
        }

        private static int Num(string s) => int.TryParse(s, out var n) ? n : 0;
    }
}
