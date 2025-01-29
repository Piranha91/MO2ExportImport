using System.IO;
using System.Windows;

namespace MO2ExportImport;

// These functions are not intended for end users. They're for me trying to do things that are specific to my load order.

public class PersonalFunctions
{
    public static void LaunchPersonalFunction()
    {
        //CheckPluginsOrder();
    }
    
    private static void CheckPluginsOrder()
    {
        string pluginsPath = "S:\\Mad God Overhaul\\profiles\\00 - Testing - 5 - Replace ASLAL\\plugins.txt";
        string modListPath = "S:\\Mad God Overhaul\\profiles\\00 - Testing - 5 - Replace ASLAL\\modlist.txt";
        string modPath = "S:\\Mad God Overhaul\\mods";

        var pluginListings = CommonFuncs.LoadPluginListRaw(pluginsPath);
        var modListings = CommonFuncs.LoadModList(modListPath);
        
        var dictionary = MapPluginsToWinningMods(pluginListings, modListings, modPath);

        string firstAddedMod = "Engine Fixes VR - Custom INI";
        string firstAddedPlugin = "AlternatePerspective.esp";

        var firstModIndex = modListings.FindIndex(x => x.Name == firstAddedMod);
        var firstPluginIndex = pluginListings.FindIndex(x => x.Name == firstAddedPlugin);

        List<string> outOfOrder = new();
        for (int i = firstPluginIndex + 1; i < pluginListings.Count; i++)
        {
            var sourceMod = dictionary[pluginListings[i].Name];
            var currentModIndex = modListings.FindIndex(x => x.Name == sourceMod);
            if (currentModIndex < firstModIndex)
            {
                outOfOrder.Add($"{pluginListings[i].Name} from {sourceMod}");
            }
        }

        if (outOfOrder.Count > 0)
        {
            var outputStr = string.Join("\n", outOfOrder);
            Clipboard.SetText(outputStr);
            MessageBox.Show(outputStr);
        }
        else
        {
            MessageBox.Show("No out of order plugins found");
        }
    }
    
    public static Dictionary<string, string> MapPluginsToWinningMods(List<PluginListing> pluginListings,
        List<ModListing> modListings, string modsDir)
    {
        Dictionary<string, string> outputDict = new();
        
        foreach (var plugin in pluginListings)
        {
            for (int i = modListings.Count - 1; i >= 0; i--)
            {
                var currentMod = modListings[i];
                if (!currentMod.Enabled.HasValue || !currentMod.Enabled.Value)
                {
                    continue;
                }
                var currentModDir = Path.Combine(modsDir, currentMod.GetCurrentFolderName());
                var plugins = CommonFuncs.GetPluginPathsInDir(currentModDir).Select(x => Path.GetFileName(x)).ToArray();
                if (plugins.Any(x => plugin.Name == x))
                {
                    if (!outputDict.ContainsKey(plugin.Name))
                    {
                        outputDict.Add(plugin.Name, currentMod.Name);
                    }
                    break;
                }
            }
        }

        return outputDict;
    }
}