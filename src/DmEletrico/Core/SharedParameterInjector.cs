using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace DmEletrico.Core
{
    /// <summary>
    /// Garante a existência dos parâmetros compartilhados do DmEletrico no modelo
    /// ativo (requisito 2). Cria um arquivo de parâmetros compartilhados dedicado
    /// se necessário, define o grupo "DmEletrico" e vincula cada parâmetro às
    /// categorias-alvo via binding de instância.
    ///
    /// Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public static class SharedParameterInjector
    {
        private const string GroupName = "DmEletrico";
        private const string SharedFileName = "DmEletrico_SharedParameters.txt";

        public static void EnsureParameters(Document doc)
        {
            var app = doc.Application;
            var originalFile = app.SharedParametersFilename;

            try
            {
                var defFile = OpenOrCreateSharedFile(app);
                var group = defFile.Groups.get_Item(GroupName) ?? defFile.Groups.Create(GroupName);

                foreach (var def in DmParameters.All())
                {
                    var externalDef = group.Definitions.get_Item(def.Name) as ExternalDefinition
                                      ?? CreateDefinition(group, def);

                    BindIfNeeded(doc, externalDef, def);
                }

                EnsureProjectInfoParameters(doc, defFile, group);
            }
            finally
            {
                // Restaura o arquivo de parâmetros compartilhados original do usuário.
                if (!string.IsNullOrEmpty(originalFile))
                    app.SharedParametersFilename = originalFile;
            }
        }

        private static DefinitionFile OpenOrCreateSharedFile(Autodesk.Revit.ApplicationServices.Application app)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DmEletrico");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, SharedFileName);
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);

            app.SharedParametersFilename = path;
            return app.OpenSharedParameterFile();
        }

        private static ExternalDefinition CreateDefinition(DefinitionGroup group, DmParameters.Definition def)
        {
            var options = new ExternalDefinitionCreationOptions(def.Name, def.DataType)
            {
                Visible = true
            };
            return (ExternalDefinition)group.Definitions.Create(options);
        }

        private static void BindIfNeeded(Document doc, ExternalDefinition externalDef, DmParameters.Definition def)
        {
            if (def.Categories == null || def.Categories.Count == 0)
                return;

            var catSet = doc.Application.Create.NewCategorySet();
            foreach (var bic in def.Categories)
            {
                var cat = Category.GetCategory(doc, bic);
                if (cat != null) catSet.Insert(cat);
            }
            if (catSet.IsEmpty) return;

            var binding = def.IsInstance
                ? (Binding)doc.Application.Create.NewInstanceBinding(catSet)
                : doc.Application.Create.NewTypeBinding(catSet);

            var map = doc.ParameterBindings;
            if (map.Contains(externalDef))
                map.ReInsert(externalDef, binding, GroupTypeId.Electrical);
            else
                map.Insert(externalDef, binding, GroupTypeId.Electrical);
        }

        /// <summary>Parâmetros globais ligados ao ProjectInformation.</summary>
        private static void EnsureProjectInfoParameters(Document doc, DefinitionFile defFile, DefinitionGroup group)
        {
            // Valores numéricos como SpecTypeId.Number (valor bruto, sem conversão
            // de unidade) para round-trip previsível entre Setup e leitura.
            var globais = new (string name, ForgeTypeId type)[]
            {
                (DmParameters.TemperaturaAmbiente, SpecTypeId.Number),
                (DmParameters.TensaoNominal,       SpecTypeId.Number),
                (DmParameters.AlturaRoteamento,    SpecTypeId.Number),
                (DmParameters.OffsetLaje,          SpecTypeId.Number),
                (DmParameters.OffsetParede,        SpecTypeId.Number),
                (DmParameters.OffsetContrapiso,    SpecTypeId.Number),
                (DmParameters.ModoRoteamento,      SpecTypeId.String.Text),
                (DmParameters.ConduitTypeId,       SpecTypeId.String.Text),
                (DmParameters.MetodoInstalacao,    SpecTypeId.String.Text),
                (DmParameters.SetupConcluido,      SpecTypeId.Boolean.YesNo),
            };

            var projInfoCat = Category.GetCategory(doc, BuiltInCategory.OST_ProjectInformation);
            foreach (var (name, type) in globais)
            {
                var ext = group.Definitions.get_Item(name) as ExternalDefinition
                          ?? CreateDefinition(group, new DmParameters.Definition(name, type, true));

                var catSet = doc.Application.Create.NewCategorySet();
                catSet.Insert(projInfoCat);
                var binding = doc.Application.Create.NewInstanceBinding(catSet);

                var map = doc.ParameterBindings;
                if (!map.Contains(ext))
                    map.Insert(ext, binding, GroupTypeId.Electrical);
            }
        }
    }
}
