using System.Text.RegularExpressions;

namespace MO2ExportImport;

public class ModListing : IEquatable<ModListing>, IListing
{
    private string _originalEntryString;
    public string Name { get; set;  } 
    public bool? Enabled { get; private set; }
    
    public bool IsNoDelete { get; private set; }
    public string NoDeletePrefix { get; private set; }
    public string Prefix { get; private set; } = string.Empty;

    public ModListing(string entryString)
    {
        _originalEntryString = entryString;
        Enabled = FormatHandler.GetModActivationStatus(_originalEntryString);
        Name = FormatHandler.TrimModActivationStatus(entryString);
        IsNoDelete = CommonFuncs.IsNoDelete(Name, out var noDeletePrefix);
        NoDeletePrefix = noDeletePrefix;
        Name = CommonFuncs.RemoveNoDeletePrefix(Name);
    }
    
    public ModListing() // for Json deserialization
    {
        Name = string.Empty;
        IsNoDelete = false;
        NoDeletePrefix = string.Empty;
        _originalEntryString = string.Empty;
    }
    
    public string GetCurrentEntryString()
    {
        string fullPrefix = string.Empty;
        if (!Enabled.HasValue)
        {
            fullPrefix = "*";
        }
        else if (Enabled.Value == true)
        {
            fullPrefix = "+";
        }
        else
        {
            fullPrefix = "-";
        }

        if (IsNoDelete)
        {
            if (NoDeletePrefix == string.Empty)
            {
                NoDeletePrefix = "[NoDelete]";
            }
            fullPrefix += NoDeletePrefix + " ";
        }

        fullPrefix += Prefix;
        
        return fullPrefix + Name;
    }

    public string GetCurrentFolderName()
    {
        return FormatHandler.TrimModActivationStatus(GetCurrentEntryString());
    }

    public void Enable()
    {
        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
    }

    public void MakeNoDelete()
    {
        Name = CommonFuncs.RemoveNoDeletePrefix(Name);
        IsNoDelete = true;
    }

    public void SetPrefix(string prefix)
    {
        Prefix = prefix;
    }

    public bool Equals(ModListing? other)
    {
        return other is not null && Name.Equals(other.Name);
    }

    public bool Equals(IListing? other)
    {
        return other is not null && other is ModListing && Name.Equals(other.Name);
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
    public static bool operator ==(ModListing left, ModListing right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    // Overload != operator
    public static bool operator !=(ModListing left, ModListing right)
    {
        return !(left == right);
    }
}