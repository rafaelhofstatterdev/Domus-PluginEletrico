using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core.Circuits
{
    /// <summary>Um circuito lógico: dispositivos agrupados por QDC + número.</summary>
    public sealed class LogicalCircuit
    {
        public string Quadro { get; set; } = "";
        public string Numero { get; set; } = "";
        public List<Element> Dispositivos { get; } = new();
        public FamilyInstance? Painel { get; set; }
    }

    /// <summary>
    /// Lê os circuitos lógicos do modelo a partir dos parâmetros Dm_Quadro e
    /// Dm_NumeroCircuito gravados nos dispositivos elétricos.
    /// </summary>
    public static class LogicalCircuits
    {
        private static readonly BuiltInCategory[] Terminais =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures
        };

        public static List<Element> DispositivosEletricos(Document doc)
        {
            var filter = new ElementMulticategoryFilter(Terminais);
            return new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .ToList();
        }

        public static List<LogicalCircuit> All(Document doc)
        {
            var paineis = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            FamilyInstance? PainelPorNome(string nome) =>
                paineis.FirstOrDefault(p => p.Name == nome);

            var circuitos = new Dictionary<string, LogicalCircuit>();

            foreach (var e in DispositivosEletricos(doc))
            {
                var numero = e.LookupParameter(DmParameters.NumeroCircuito)?.AsString();
                if (string.IsNullOrWhiteSpace(numero)) continue;
                var quadro = e.LookupParameter(DmParameters.Quadro)?.AsString() ?? "";

                var chave = quadro + "|" + numero;
                if (!circuitos.TryGetValue(chave, out var c))
                {
                    c = new LogicalCircuit { Quadro = quadro, Numero = numero, Painel = PainelPorNome(quadro) };
                    circuitos[chave] = c;
                }
                c.Dispositivos.Add(e);
            }

            return circuitos.Values.ToList();
        }
    }
}
