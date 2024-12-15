using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MO2ExportImport
{
    public class FormatHandler
    {
        public static string TrimModActivationStatus(string modListName)
        {
            return modListName.TrimStart('+', '-', '*');
        }

        public static bool? GetModActivationStatus(string modListName)
        {
            bool hasActivationStatus = modListName.StartsWith('+') || modListName.StartsWith('*') || modListName.StartsWith('-');
            if (!hasActivationStatus)
            {
                return null;
            }
            return modListName.StartsWith('+') || modListName.StartsWith('*');
        }

        public static string GetModActivationStatusCharStr(string modListName)
        {
            bool hasActivationStatus = modListName.StartsWith('+') || modListName.StartsWith('*') || modListName.StartsWith('-');
            if (hasActivationStatus)
            {
                return modListName[0].ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        public static IEnumerable<string> TrimModActivationStatus(IEnumerable<string> modNames)
        {
            return modNames.Select(x => TrimModActivationStatus(x));
        }

        public static string TrimPluginActivationStatus(string pluginName)
        {
            return pluginName.TrimStart('*');
        }

        public static IEnumerable<string> TrimPluginActivationStatus(IEnumerable<string> pluginNames)
        {
            return pluginNames.Select(x => TrimPluginActivationStatus(x));
        }

        public static string TrimActivationStatus(string str, StringType stringType)
        {
            switch(stringType)
            {
                case StringType.Mod: return TrimModActivationStatus(str);
                case StringType.Plugin: return TrimPluginActivationStatus(str);
            }
            return str;
        }

        public static IEnumerable<string> TrimActivationStatus(IEnumerable<string> strings, StringType stringType)
        {
            switch (stringType)
            {
                case StringType.Mod: return TrimModActivationStatus(strings);
                case StringType.Plugin: return TrimPluginActivationStatus(strings);
            }
            return strings;
        }

        public enum StringType
        {
            Mod,
            Plugin
        }

        public static string RemoveNoDeletePrefix(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Define a case-insensitive regex pattern
            string pattern = @"^\[NoDelete.*?\]";

            // Match the pattern at the start of the string
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

            // Check if a match is found at the start of the string
            if (match.Success && match.Index == 0)
            {
                // Remove the matched substring and trim the result
                return input.Substring(match.Length).Trim();
            }

            // Return the original string if no match
            return input;
        }
    }
}
