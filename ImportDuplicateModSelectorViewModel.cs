using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Media;
using Alphaleonis.Win32.Filesystem;
using ReactiveUI;

namespace MO2ExportImport;

public class ImportDuplicateModSelectorViewModel
{
    public ObservableCollection<DuplicateModItem> DuplicateMods { get; set; }
    private ImportDuplicateModSelectorWindow _window { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public List<Mod> UnselectedModsForOverwrite { get; set; } = new();
    private string _sourceModsDirPath;

    public ImportDuplicateModSelectorViewModel(List<DuplicateModItem> duplicateMods, string sourceModsDirPath)
    {
        ExitCommand = ReactiveCommand.Create(Close);
        _sourceModsDirPath = sourceModsDirPath;
        DuplicateMods = new ObservableCollection<DuplicateModItem>(duplicateMods);
        GetModVersions();
        SelectIfOverriding();

        _window = new ImportDuplicateModSelectorWindow();
        _window.DataContext = this;
        _window.ShowDialog();
    }

    public void GetModVersions()
    {
        foreach (var mod in DuplicateMods)
        {
            var sourceDir = Path.Combine(_sourceModsDirPath, mod.ImportedMod.SourceDirectoryName);
            mod.ImportedModVersion = GetModVersion(sourceDir) ?? "Unknown";
            mod.DestinationExistingModVersion = GetModVersion(mod.MatchedDestinationModPath) ?? "Unknown";
        }
    }

    public string? GetModVersion(string modDirectory)
    {
        string? version = null;
        string metaPath = Path.Combine(modDirectory, "meta.ini");
        if (File.Exists(metaPath))
        {
            var lines = File.ReadAllLines(metaPath);
            var versionLine = lines.FirstOrDefault(x => x.StartsWith("version="));
            if (versionLine != null)
            {
                version = versionLine.Replace("version=", "");
            }
        }

        return version;
    }

    public void Close()
    {
        UnselectedModsForOverwrite =
            DuplicateMods.Where(x => !x.OverwriteExistingMod).Select(x => x.ImportedMod).ToList();
        _window.Close();
    }

    public void SelectIfOverriding()
    {
        foreach (var item in DuplicateMods)
        {
            int? comparison = CompareVersionStrings(item.ImportedModVersion, item.DestinationExistingModVersion);
            if (comparison.HasValue)
            {
                if (comparison.Value > 0)
                {
                    // Imported version is newer.
                    item.OverwriteExistingMod = true;
                    item.ImportedVersionColor = Brushes.Green;
                    item.DestinationExistingModColor = Brushes.Black;
                }
                else if (comparison.Value < 0)
                {
                    // Destination version is newer.
                    item.OverwriteExistingMod = false;
                    item.ImportedVersionColor = Brushes.Black;
                    item.DestinationExistingModColor = Brushes.Green;
                }
                else
                {
                    // Versions are equal.
                    item.OverwriteExistingMod = false;
                    item.ImportedVersionColor = Brushes.Black;
                    item.DestinationExistingModColor = Brushes.Black;
                }
            }
            else
            {
                // Versions are not comparable.
                item.OverwriteExistingMod = false;
                item.ImportedVersionColor = Brushes.DarkGoldenrod;
                item.DestinationExistingModColor = Brushes.DarkGoldenrod;
            }
        }
    }

    private int? CompareVersionStrings(string versionA, string versionB)
    {
        if (string.IsNullOrWhiteSpace(versionA) || string.IsNullOrWhiteSpace(versionB))
            return null;

        try
        {
            var partsA = versionA.Split('.');
            var partsB = versionB.Split('.');
            int maxLength = Math.Max(partsA.Length, partsB.Length);

            for (int i = 0; i < maxLength; i++)
            {
                // Get the i-th part or default to "0"
                string partA = i < partsA.Length ? partsA[i] : "0";
                string partB = i < partsB.Length ? partsB[i] : "0";

                // Extract numeric portion and suffix (if any) for partA
                int j = 0;
                while (j < partA.Length && char.IsDigit(partA[j])) j++;
                if (j == 0) return null; // Not comparable
                int numA = int.Parse(partA.Substring(0, j));
                string suffixA = j < partA.Length ? partA.Substring(j) : "";

                // Extract numeric portion and suffix (if any) for partB
                int k = 0;
                while (k < partB.Length && char.IsDigit(partB[k])) k++;
                if (k == 0) return null; // Not comparable
                int numB = int.Parse(partB.Substring(0, k));
                string suffixB = k < partB.Length ? partB.Substring(k) : "";

                // First compare the numeric parts.
                if (numA != numB)
                    return numA.CompareTo(numB);

                // Numeric parts are equal; compare suffixes.
                if (suffixA == suffixB)
                    continue;
                else if (string.IsNullOrEmpty(suffixA) && !string.IsNullOrEmpty(suffixB))
                    return -1; // e.g. "1.2" is considered older than "1.2a"
                else if (!string.IsNullOrEmpty(suffixA) && string.IsNullOrEmpty(suffixB))
                    return 1; // e.g. "1.2a" is considered newer than "1.2"
                else
                {
                    // Both have suffixes; compare lexicographically.
                    int suffixComparison = string.Compare(suffixA, suffixB, StringComparison.OrdinalIgnoreCase);
                    if (suffixComparison != 0)
                        return suffixComparison;
                }
            }

            return 0; // All parts are equal.
        }
        catch
        {
            return null;
        }
    }
}

public class DuplicateModItem : ReactiveObject
{
    public DuplicateModItem(Mod importedModTemplate, string matchMethod)
    {
        ImportedMod = importedModTemplate;
        Label = ImportedMod.DisplayName;
        MatchMethod = matchMethod;
    }
    public Mod ImportedMod { get; }
    public string Label { get; set; }
    public string MatchedDestinationModPath { get; set; }
    public string ImportedModVersion { get; set; }
    public string DestinationExistingModVersion { get; set; }
    public string MatchMethod { get; set; }
    public const string MatchMethodName = "Destination mod has same name";
    public const string MatchMethodPlugin = "Destination mod has same plugins";
    private bool _overwriteExistingMod;

    public SolidColorBrush ImportedVersionColor { get; set; } = Brushes.Black;
    public SolidColorBrush DestinationExistingModColor { get; set; } = Brushes.Black;

    public bool OverwriteExistingMod
    {
        get => _overwriteExistingMod;
        set => this.RaiseAndSetIfChanged(ref _overwriteExistingMod, value);
    }
}