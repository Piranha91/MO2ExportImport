using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MO2ExportImport
{
    public class CommonFuncs
    {
        public static List<string> LoadModList(string filePath, bool reverseOrder = true)
        {
            if (!File.Exists(filePath))
            {
                return new List<string>();
            }

            var lines = File.ReadAllLines(filePath).Where(line => !line.StartsWith("#")).ToList();

            if (reverseOrder)
            {
                lines.Reverse(); // Reverse the list in place if needed
            }

            return lines;
        }

        public static List<string> LoadPluginList(string filePath, bool reverseOrder = false)
        {
            if (!File.Exists(filePath))
            {
                return new List<string>();
            }

            var lines = File.ReadAllLines(filePath).Where(line => !line.StartsWith("#")).ToList();

            if (reverseOrder)
            {
                lines.Reverse(); // Reverse the list in place if needed
            }

            return lines;
        }
    }
}
