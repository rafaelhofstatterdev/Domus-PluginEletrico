using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DmEletrico.Core.Shortcuts
{
    /// <summary>
    /// Registro/mesclagem dos atalhos de teclado do DmEletrico (requisito 12) no
    /// KeyboardShortcuts.xml da versão do Revit. Reporta conflitos com atalhos já
    /// existentes. As mudanças tipicamente passam a valer após reiniciar o Revit.
    /// </summary>
    public static class ShortcutsService
    {
        public sealed class Entry
        {
            public string CommandName = "";
            public string CommandId = "";
            public string Paths = "";
        }

        public sealed class Result
        {
            public string Caminho = "";
            public int Adicionados;
            public int Atualizados;
            public bool ArquivoCriado;
            public List<string> Conflitos { get; } = new();

            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Arquivo: {Caminho}");
                if (ArquivoCriado) sb.AppendLine("(arquivo criado — pode ser necessário reiniciar/importar no Revit)");
                sb.AppendLine($"Atalhos adicionados: {Adicionados} | atualizados: {Atualizados}");
                if (Conflitos.Count > 0)
                {
                    sb.AppendLine("\nConflitos detectados:");
                    foreach (var c in Conflitos) sb.AppendLine("• " + c);
                }
                sb.AppendLine("\nReinicie o Revit para os atalhos entrarem em vigor.");
                return sb.ToString();
            }
        }

        public static IReadOnlyList<Entry> DefaultEntries() => new[]
        {
            new Entry { CommandName = "Construir Conduítes",     Paths = "CB",
                        CommandId = "CustomCtrl_%CustomCtrl_%DmEletrico%Modelagem%DmConduitBuilder" },
            new Entry { CommandName = "Central de Documentação", Paths = "DC",
                        CommandId = "CustomCtrl_%CustomCtrl_%DmEletrico%Documentação%DmDocCenter" },
            new Entry { CommandName = "Manual TAG",              Paths = "MT",
                        CommandId = "CustomCtrl_%CustomCtrl_%DmEletrico%Anotação%DmManualTag" },
            new Entry { CommandName = "Route Fit",               Paths = "RF",
                        CommandId = "CustomCtrl_%CustomCtrl_%DmEletrico%Modelagem%DmRouteFit" },
        };

        public static Result Apply(string versionNumber)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", $"Autodesk Revit {versionNumber}");
            var caminho = Path.Combine(dir, "KeyboardShortcuts.xml");
            var result = new Result { Caminho = caminho };

            XDocument xdoc;
            XElement root;

            if (File.Exists(caminho))
            {
                xdoc = XDocument.Load(caminho);
                root = xdoc.Element("Shortcuts") ?? new XElement("Shortcuts");
                if (xdoc.Root == null) xdoc.Add(root);
            }
            else
            {
                Directory.CreateDirectory(dir);
                root = new XElement("Shortcuts");
                xdoc = new XDocument(root);
                result.ArquivoCriado = true;
            }

            var itens = root.Elements("ShortcutItem").ToList();

            foreach (var entry in DefaultEntries())
            {
                // Conflito: outro comando já usa o mesmo atalho.
                foreach (var it in itens)
                {
                    var paths = (string?)it.Attribute("Paths");
                    var cid = (string?)it.Attribute("CommandId");
                    if (!string.IsNullOrEmpty(paths) && cid != entry.CommandId &&
                        paths!.Split(' ', '#').Contains(entry.Paths))
                    {
                        result.Conflitos.Add($"'{entry.Paths}' já usado por '{(string?)it.Attribute("CommandName")}'.");
                    }
                }

                var existente = itens.FirstOrDefault(it => (string?)it.Attribute("CommandId") == entry.CommandId);
                if (existente != null)
                {
                    existente.SetAttributeValue("Paths", entry.Paths);
                    result.Atualizados++;
                }
                else
                {
                    root.Add(new XElement("ShortcutItem",
                        new XAttribute("CommandName", entry.CommandName),
                        new XAttribute("CommandId", entry.CommandId),
                        new XAttribute("Paths", entry.Paths)));
                    result.Adicionados++;
                }
            }

            xdoc.Save(caminho);
            return result;
        }
    }
}
