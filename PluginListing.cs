namespace MO2ExportImport;

public class PluginListing: IEquatable<PluginListing>, IListing
{
    private string _originalEntryString;
    public string Name { get; set;  } 
    public bool? Enabled { get; private set; }

    public PluginListing(string entryString)
    {
        _originalEntryString = entryString;
        Name = FormatHandler.TrimPluginActivationStatus(entryString);
        Enabled = FormatHandler.GetPluginActivationStatus(entryString);
    }

    public string GetCurrentEntryString()
    {
        string prefix = string.Empty;
        if (Enabled.HasValue && Enabled.Value)
        {
            prefix += "*";
        }

        return prefix + Name;
    }
    
    public bool Equals(PluginListing? other)
    {
        return other is not null && Name.Equals(other.Name);
    }
    
    // Override GetHashCode
    public override int GetHashCode()
    {
        // Combine hash codes of properties
        return Name.GetHashCode();
    }

    // Overload == operator
    public static bool operator ==(PluginListing left, PluginListing right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    // Overload != operator
    public static bool operator !=(PluginListing left, PluginListing right)
    {
        return !(left == right);
    }
}