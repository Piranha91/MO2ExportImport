using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MO2ExportImport
{
    public class FormatHandler
    {
        public static string TrimModActivationStatus(string modName)
        {
            return modName.TrimStart('+', '-');
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
    }
}
