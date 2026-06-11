using System;
using System.Reflection;
using Autodesk.Revit.UI;
using DmEletrico.Commands;

namespace DmEletrico.Application
{
    /// <summary>
    /// Constrói a aba e os painéis da Ribbon do DmEletrico.
    ///
    /// Comportamento dinâmico de habilitação:
    ///   - DmSetup está sempre disponível (precisa rodar antes de tudo).
    ///   - Os demais botões usam <see cref="Availability.ElectricalElementsAvailability"/>,
    ///     ficando cinza enquanto o modelo não tiver elementos elétricos
    ///     (OST_ElectricalFixtures, OST_LightingFixtures, OST_ElectricalEquipment).
    /// </summary>
    internal static class RibbonBuilder
    {
        public static void Build(UIControlledApplication app)
        {
            app.CreateRibbonTab(DmApplication.TabName);

            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            BuildConfigPanel(app, assemblyPath);
            BuildModelingPanel(app, assemblyPath);
            BuildCircuitsPanel(app, assemblyPath);
            BuildAnnotationPanel(app, assemblyPath);
            BuildDocPanel(app, assemblyPath);
            BuildCoordinationPanel(app, assemblyPath);
        }

        private static void BuildConfigPanel(UIControlledApplication app, string asm)
        {
            var panel = app.CreateRibbonPanel(DmApplication.TabName, "Configuração");

            // Setup: sempre habilitado (sem availability class).
            AddButton(panel, asm,
                name: "DmSetup",
                text: "Setup",
                command: typeof(DmSetupCommand),
                tooltip: "Injeta os parâmetros compartilhados do DmEletrico, configura templates e variáveis globais do projeto.");
        }

        private static void BuildModelingPanel(UIControlledApplication app, string asm)
        {
            var panel = app.CreateRibbonPanel(DmApplication.TabName, "Modelagem");

            AddButton(panel, asm,
                name: "DmConduitBuilder",
                text: "Construir\nConduítes",
                command: typeof(DmConduitBuilderCommand),
                tooltip: "Traça automaticamente o roteamento 3D de conduítes entre dispositivos e quadros, dimensionando pela NBR 5410. (Atalho: CB)",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmRouteFit",
                text: "Ajuste de\nRotas",
                command: typeof(DmRouteFitCommand),
                tooltip: "Recalcula trechos com geometria inválida após movimentação de dispositivos. (Atalho: RF)",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmConduitDetail",
                text: "Detalhar\nTrecho",
                command: typeof(DmConduitDetailCommand),
                tooltip: "Mostra os parâmetros calculados (corrente, FCT, FCA, seção, queda) do conduíte selecionado.",
                availability: AvailabilityNames.ElectricalElements);
        }

        private static void BuildCircuitsPanel(UIControlledApplication app, string asm)
        {
            var panel = app.CreateRibbonPanel(DmApplication.TabName, "Circuitos");

            AddButton(panel, asm,
                name: "DmCircuitLoad",
                text: "Atribuir\nCarga",
                command: typeof(DmCircuitLoadCommand),
                tooltip: "Configura potência, polos, tensão e tipo dos dispositivos selecionados.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmCreateCircuit",
                text: "Criar\nCircuito",
                command: typeof(DmCreateCircuitCommand),
                tooltip: "Cria um circuito a partir dos dispositivos selecionados e atribui a um QDC.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmCircuitManager",
                text: "Gerenciar\nCircuitos",
                command: typeof(DmCircuitManagerCommand),
                tooltip: "Lista os circuitos e permite reatribuir QDC, renumerar e balancear fases.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmCheckDisconnected",
                text: "Dispositivos\nDesconectados",
                command: typeof(DmCheckDisconnectedCommand),
                tooltip: "Varre o modelo e lista famílias elétricas sem circuito atribuído.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmPhaseBalance",
                text: "Balancear\nFases",
                command: typeof(DmPhaseBalanceCommand),
                tooltip: "Distribui os circuitos dos QDCs entre as fases A, B e C buscando equilíbrio de carga.",
                availability: AvailabilityNames.ElectricalElements);
        }

        private static void BuildCoordinationPanel(UIControlledApplication app, string asm)
        {
            var panel = app.CreateRibbonPanel(DmApplication.TabName, "Coordenação");

            // Sem availability: útil mesmo quando o hospedeiro não tem elementos
            // elétricos (os elementos vêm dos modelos vinculados).
            AddButton(panel, asm,
                name: "DmLinkCoordination",
                text: "Modelos\nVinculados",
                command: typeof(DmLinkCoordinationCommand),
                tooltip: "Lê elementos elétricos dos modelos vinculados (RevitLinkInstance) e reporta no hospedeiro.");
        }

        private static void BuildAnnotationPanel(UIControlledApplication app, string asm)
        {
            var panel = app.CreateRibbonPanel(DmApplication.TabName, "Anotação");

            AddButton(panel, asm,
                name: "DmAutoTag",
                text: "Auto\nTAG",
                command: typeof(DmAutoTagCommand),
                tooltip: "Insere automaticamente TAGs de fiação nos trechos de conduíte.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmManualTag",
                text: "Manual\nTAG",
                command: typeof(DmManualTagCommand),
                tooltip: "Seleção pontual de trechos para inserir TAGs. (Atalho: MT)",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmTagRemove",
                text: "Remover\nTAG",
                command: typeof(DmTagRemoveCommand),
                tooltip: "Seleciona e remove uma TAG.",
                availability: AvailabilityNames.ElectricalElements);
        }

        private static void BuildDocPanel(UIControlledApplication app, string asm)
        {
            var panel = app.CreateRibbonPanel(DmApplication.TabName, "Documentação");

            AddButton(panel, asm,
                name: "DmDocCenter",
                text: "Central de\nDocumentação",
                command: typeof(DmDocCenterCommand),
                tooltip: "Painel central: QDCs, circuitos, vistas, quadros de cargas e diagramas unifilares. (Atalho: DC)",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmLoadSchedule",
                text: "Quadros de\nCargas",
                command: typeof(DmLoadScheduleCommand),
                tooltip: "Gera o quadro de cargas (ViewSchedule) de cada QDC.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmUnifilar",
                text: "Diagrama\nUnifilar",
                command: typeof(DmUnifilarCommand),
                tooltip: "Gera o diagrama unifilar dinâmico (ViewDrafting) de cada QDC.",
                availability: AvailabilityNames.ElectricalElements);

            AddButton(panel, asm,
                name: "DmMaterials",
                text: "Quantitativos",
                command: typeof(DmMaterialsCommand),
                tooltip: "Gera o quadro de materiais: conduítes, condutores, dispositivos e quadros.",
                availability: AvailabilityNames.ElectricalElements);
        }

        private static PushButton AddButton(
            RibbonPanel panel,
            string assemblyPath,
            string name,
            string text,
            Type command,
            string tooltip,
            string? availability = null)
        {
            var data = new PushButtonData(name, text, assemblyPath, command.FullName)
            {
                ToolTip = tooltip
            };

            if (!string.IsNullOrEmpty(availability))
                data.AvailabilityClassName = availability;

            return (PushButton)panel.AddItem(data);
        }
    }

    /// <summary>Nomes totalmente qualificados das classes de disponibilidade.</summary>
    internal static class AvailabilityNames
    {
        public const string ElectricalElements = "DmEletrico.Availability.ElectricalElementsAvailability";
    }
}
