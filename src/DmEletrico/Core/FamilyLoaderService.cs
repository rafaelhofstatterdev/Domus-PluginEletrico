using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace DmEletrico.Core
{
    /// <summary>
    /// Carrega as famílias (.rfa) necessárias ao DmEletrico — em especial a TAG de
    /// conduíte — a partir de pastas conhecidas, caso ainda não estejam no projeto.
    /// Procura em &lt;dll&gt;\Resources\Families e em %AppData%\DmEletrico\Families.
    ///
    /// Deve ser chamado dentro de uma transação aberta.
    /// </summary>
    public static class FamilyLoaderService
    {
        public static int LoadMissingFamilies(Document doc)
        {
            var carregadas = 0;
            foreach (var arquivo in EnumerarRfa())
            {
                if (doc.LoadFamily(arquivo, out _)) carregadas++;
            }
            return carregadas;
        }

        private static IEnumerable<string> EnumerarRfa()
        {
            var pastas = new List<string>();

            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dllDir != null) pastas.Add(Path.Combine(dllDir, "Resources", "Families"));

            pastas.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DmEletrico", "Families"));

            // Procura uma pasta "familias" subindo a partir do diretório do .dll
            // (layout do repositório: <raiz>\familias).
            var dir = dllDir;
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var cand = Path.Combine(dir, "familias");
                if (Directory.Exists(cand)) { pastas.Add(cand); break; }
                dir = Path.GetDirectoryName(dir);
            }

            foreach (var pasta in pastas.Distinct())
            {
                if (!Directory.Exists(pasta)) continue;
                foreach (var f in Directory.GetFiles(pasta, "*.rfa"))
                    yield return f;
            }
        }
    }
}
