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

    public bool Equals(IListing? other)
    {
        return other is not null && other is PluginListing && Name.Equals(other.Name);
    }

    public int GetIndexOf(IList<IListing> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Equals(this))
            {
                return i;
            }
        }
        return -1;
    }

    public override string ToString()
    {
        return Name;
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