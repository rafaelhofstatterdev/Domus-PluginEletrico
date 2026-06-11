using System.Globalization;
using Autodesk.Revit.DB;

namespace DmEletrico.Core
{
    public enum MetodoInstalacao
    {
        Embutido,
        Aparente,
        Eletrocalha
    }

    /// <summary>
    /// Variáveis globais do projeto persistidas como parâmetros do
    /// ProjectInformation. Lidas pelo motor de cálculo e gravadas pelo DmSetup.
    /// </summary>
    public sealed class DmProjectSettings
    {
        public double TemperaturaAmbiente { get; set; } = 30.0; // °C (padrão NBR / Setup)
        public double TensaoNominal { get; set; } = 220.0;      // V
        public MetodoInstalacao Metodo { get; set; } = MetodoInstalacao.Embutido;
        public bool SetupConcluido { get; set; }

        public static DmProjectSettings Read(Document doc)
        {
            var info = doc.ProjectInformation;
            var s = new DmProjectSettings();

            if (TryGetDouble(info, DmParameters.TemperaturaAmbiente, out var temp)) s.TemperaturaAmbiente = temp;
            if (TryGetDouble(info, DmParameters.TensaoNominal, out var v)) s.TensaoNominal = v;

            var metodo = info.LookupParameter(DmParameters.MetodoInstalacao)?.AsString();
            if (!string.IsNullOrWhiteSpace(metodo) &&
                System.Enum.TryParse<MetodoInstalacao>(metodo, ignoreCase: true, out var m))
                s.Metodo = m;

            var concluido = info.LookupParameter(DmParameters.SetupConcluido)?.AsInteger() ?? 0;
            s.SetupConcluido = concluido != 0;

            return s;
        }

        /// <summary>Persiste as configurações no ProjectInformation. Requer transação aberta.</summary>
        public void Write(Document doc)
        {
            var info = doc.ProjectInformation;
            info.LookupParameter(DmParameters.TemperaturaAmbiente)?.Set(TemperaturaAmbiente);
            info.LookupParameter(DmParameters.TensaoNominal)?.Set(TensaoNominal);
            info.LookupParameter(DmParameters.MetodoInstalacao)?.Set(Metodo.ToString());
            info.LookupParameter(DmParameters.SetupConcluido)?.Set(SetupConcluido ? 1 : 0);
        }

        private static bool TryGetDouble(Element e, string name, out double value)
        {
            value = 0;
            var p = e.LookupParameter(name);
            if (p == null || p.StorageType != StorageType.Double) return false;
            value = p.AsDouble();
            return true;
        }

        public string MetodoToString() => Metodo.ToString();

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "Temp={0}°C, Vn={1}V, Método={2}, SetupOK={3}",
                TemperaturaAmbiente, TensaoNominal, Metodo, SetupConcluido);
    }
}
