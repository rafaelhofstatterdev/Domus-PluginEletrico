using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DmEletrico.Core;
using DmEletrico.Core.Calculation;
using DmEletrico.UI.Load;

namespace DmEletrico.Commands
{
    /// <summary>
    /// Requisito 6 — Atribuição de carga. Configura potência, polos, tensão e tipo
    /// nos dispositivos elétricos selecionados (ou escolhidos), gravando os
    /// parâmetros Dm_ correspondentes.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DmCircuitLoadCommand : DmCommandBase
    {
        private static readonly BuiltInCategory[] Categorias =
        {
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalEquipment
        };

        private static readonly string[] NenhumExcluir = System.Array.Empty<string>();

        protected override Result Run(ExternalCommandData data, UIDocument uiDoc, Document doc)
        {
            var dispositivos = DispositivosSelecionados(uiDoc, doc);
            if (dispositivos.Count == 0)
                dispositivos = DispositivosEscolhidos(uiDoc, doc);
            if (dispositivos.Count == 0)
                return Result.Cancelled;

            var vm = new DmCircuitLoadViewModel(dispositivos.Count);
            var window = new DmCircuitLoadWindow { DataContext = vm };
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            using (var tx = new Transaction(doc, "DmEletrico — Atribuir Carga"))
            {
                tx.Start();
                var corrente = vm.TensaoOperacao > 0 ? vm.Potencia / vm.TensaoOperacao : 0;
                var disjuntor = Nbr5410Tables.DisjuntorComercial(corrente);

                foreach (var e in dispositivos)
                {
                    e.LookupParameter(DmParameters.Potencia)?.Set(vm.Potencia);
                    e.LookupParameter(DmParameters.NumeroPolos)?.Set(vm.NumeroPolos);
                    e.LookupParameter(DmParameters.TensaoOperacao)?.Set(vm.TensaoOperacao);
                    e.LookupParameter(DmParameters.TipoCircuito)?.Set(vm.TipoCircuito);
                    e.LookupParameter(DmParameters.Disjuntor)?.Set(disjuntor);

                    // Propaga para os parâmetros nativos/da família.
                    PropagarParaFamilia(e, vm.Potencia, vm.TensaoOperacao);
                    ParamPropagation.SetInteiro(e, vm.NumeroPolos, new[] { "polo" }, NenhumExcluir);
                    ParamPropagation.SetTexto(e, vm.TipoCircuito, new[] { "tipo" }, new[] { "ifc", "ponto", "parte" });
                }
                tx.Commit();
            }

            TaskDialog.Show("DmEletrico — Atribuir Carga",
                $"Carga aplicada a {dispositivos.Count} dispositivo(s).");
            return Result.Succeeded;
        }

        /// <summary>
        /// Grava a potência/tensão também nos parâmetros da família que aparentam ser
        /// entradas de carga (ex.: "..._Potência Aparente", "..._Tensão"), pulando
        /// fator de potência e potência ativa (calculados). Parâmetros tipados do
        /// Revit exigem conversão para unidades internas (senão 200 VA viram ~18,6).
        /// </summary>
        private static void PropagarParaFamilia(Element e, double potencia, double tensao)
        {
            foreach (Parameter p in e.Parameters)
            {
                if (p.IsReadOnly || p.StorageType != StorageType.Double) continue;
                var nome = p.Definition?.Name ?? "";
                var low = nome.ToLowerInvariant();
                if (low.StartsWith("dm_")) continue;

                var ehPotencia = (low.Contains("potência") || low.Contains("potencia") || low.Contains("(va)"))
                                 && low.Contains("aparente")
                                 && !low.Contains("fator") && !low.Contains("ativa");
                var ehTensao = low.Contains("tensão") || low.Contains("tensao");

                if (ehPotencia) p.Set(ParaInterno(p, potencia, UnitTypeId.VoltAmperes));
                else if (ehTensao) p.Set(ParaInterno(p, tensao, UnitTypeId.Volts));
            }
        }

        /// <summary>Converte o valor digitado para as unidades internas do parâmetro.</summary>
        private static double ParaInterno(Parameter p, double valor, ForgeTypeId unidadePadrao)
        {
            try
            {
                var unit = p.GetUnitTypeId();
                return UnitUtils.ConvertToInternalUnits(valor, unit);
            }
            catch
            {
                try { return UnitUtils.ConvertToInternalUnits(valor, unidadePadrao); }
                catch { return valor; } // parâmetro sem unidade (Number)
            }
        }

        private static List<Element> DispositivosSelecionados(UIDocument uiDoc, Document doc)
            => uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(EhEletrico)
                .ToList();

        private static List<Element> DispositivosEscolhidos(UIDocument uiDoc, Document doc)
        {
            var refs = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                new ElectricalFilter(),
                "Selecione os dispositivos elétricos e pressione Concluir.");
            return refs.Select(r => doc.GetElement(r)).ToList();
        }

        private static bool EhEletrico(Element e)
        {
            var bic = (BuiltInCategory)(e.Category?.Id.Value ?? 0);
            return Categorias.Contains(bic);
        }

        private sealed class ElectricalFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => EhEletrico(elem);
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
