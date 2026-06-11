using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DmEletrico.Availability
{
    /// <summary>
    /// Disponibiliza um comando apenas quando o documento ativo contém pelo menos
    /// um elemento das categorias elétricas nativas (tomadas/interruptores,
    /// luminárias ou quadros). Implementa o comportamento de "botão cinza" exigido
    /// no requisito 1.
    ///
    /// Precisa ter construtor público sem parâmetros e ser pública para que o Revit
    /// a instancie a partir do AvailabilityClassName.
    /// </summary>
    public sealed class ElectricalElementsAvailability : IExternalCommandAvailability
    {
        private static readonly BuiltInCategory[] ElectricalCategories =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalEquipment
        };

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var doc = applicationData?.ActiveUIDocument?.Document;
            if (doc == null)
                return false;

            var filter = new ElementMulticategoryFilter(ElectricalCategories);

            // FirstElement() para curto-circuito: não percorre todo o modelo.
            using (var collector = new FilteredElementCollector(doc))
            {
                return collector
                    .WherePasses(filter)
                    .WhereElementIsNotElementType()
                    .FirstElement() != null;
            }
        }
    }
}
