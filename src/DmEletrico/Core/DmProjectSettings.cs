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

    public enum ModoRoteamento
    {
        Ortogonal,
        Direto
    }

    public enum AmbienteInstalacao
    {
        Teto,
        Parede,
        Piso
    }

    /// <summary>
    /// Variáveis globais do projeto persistidas como parâmetros do
    /// ProjectInformation. Lidas pelo motor de cálculo e gravadas pelo DmSetup.
    /// </summary>
    public sealed class DmProjectSettings
    {
        public double TemperaturaAmbiente { get; set; } = 30.0; // °C (padrão NBR / Setup)
        public double TensaoNominal { get; set; } = 220.0;      // V
        public double AlturaRoteamento { get; set; } = 3.0;     // m — espinha padrão
        public double OffsetLaje { get; set; } = 3.0;           // m — roteamento no teto/laje
        public double OffsetParede { get; set; } = 1.3;         // m — roteamento em parede
        public double OffsetContrapiso { get; set; } = 0.1;     // m — roteamento no piso
        public ModoRoteamento Modo { get; set; } = ModoRoteamento.Ortogonal;
        public string ConduitTypeId { get; set; } = "";         // Id do ConduitType escolhido
        public MetodoInstalacao Metodo { get; set; } = MetodoInstalacao.Embutido;
        public bool SetupConcluido { get; set; }

        /// <summary>Offset (m) a aplicar conforme o ambiente do dispositivo.</summary>
        public double OffsetPorAmbiente(AmbienteInstalacao ambiente) => ambiente switch
        {
            AmbienteInstalacao.Teto => OffsetLaje,
            AmbienteInstalacao.Parede => OffsetParede,
            AmbienteInstalacao.Piso => OffsetContrapiso,
            _ => AlturaRoteamento
        };

        public static DmProjectSettings Read(Document doc)
        {
            var info = doc.ProjectInformation;
            var s = new DmProjectSettings();

            if (TryGetDouble(info, DmParameters.TemperaturaAmbiente, out var temp)) s.TemperaturaAmbiente = temp;
            if (TryGetDouble(info, DmParameters.TensaoNominal, out var v)) s.TensaoNominal = v;
            if (TryGetDouble(info, DmParameters.AlturaRoteamento, out var h)) s.AlturaRoteamento = h;
            if (TryGetDouble(info, DmParameters.OffsetLaje, out var ol)) s.OffsetLaje = ol;
            if (TryGetDouble(info, DmParameters.OffsetParede, out var op)) s.OffsetParede = op;
            if (TryGetDouble(info, DmParameters.OffsetContrapiso, out var oc)) s.OffsetContrapiso = oc;

            var metodo = info.LookupParameter(DmParameters.MetodoInstalacao)?.AsString();
            if (!string.IsNullOrWhiteSpace(metodo) &&
                System.Enum.TryParse<MetodoInstalacao>(metodo, ignoreCase: true, out var m))
                s.Metodo = m;

            var modo = info.LookupParameter(DmParameters.ModoRoteamento)?.AsString();
            if (!string.IsNullOrWhiteSpace(modo) &&
                System.Enum.TryParse<ModoRoteamento>(modo, ignoreCase: true, out var mr))
                s.Modo = mr;

            s.ConduitTypeId = info.LookupParameter(DmParameters.ConduitTypeId)?.AsString() ?? "";

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
            info.LookupParameter(DmParameters.AlturaRoteamento)?.Set(AlturaRoteamento);
            info.LookupParameter(DmParameters.OffsetLaje)?.Set(OffsetLaje);
            info.LookupParameter(DmParameters.OffsetParede)?.Set(OffsetParede);
            info.LookupParameter(DmParameters.OffsetContrapiso)?.Set(OffsetContrapiso);
            info.LookupParameter(DmParameters.ModoRoteamento)?.Set(Modo.ToString());
            info.LookupParameter(DmParameters.ConduitTypeId)?.Set(ConduitTypeId ?? "");
            info.LookupParameter(DmParameters.MetodoInstalacao)?.Set(Metodo.ToString());
            info.LookupParameter(DmParameters.SetupConcluido)?.Set(SetupConcluido ? 1 : 0);
        }

        /// <summary>Resolve o ConduitType configurado, ou o primeiro disponível como fallback.</summary>
        public ElementId ResolveConduitTypeId(Document doc)
        {
            if (long.TryParse(ConduitTypeId, out var raw))
            {
                var id = new ElementId(raw);
                if (doc.GetElement(id) != null) return id;
            }
            var first = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Electrical.ConduitType))
                .FirstElementId();
            return first;
        }

        private static bool TryGetDouble(Element e, string name, out double value)
        {
            value = 0;
            var p = e.LookupParameter(name);
            if (p == null || p.StorageType != StorageType.Double) return false;
            value = p.AsDouble();
            return true;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "Temp={0}°C, Vn={1}V, Método={2}, Modo={3}, SetupOK={4}",
                TemperaturaAmbiente, TensaoNominal, Metodo, Modo, SetupConcluido);
    }
}
