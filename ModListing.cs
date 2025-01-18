using System.Text.RegularExpressions;

namespace MO2ExportImport;

public class ModListing : IEquatable<ModListing>, IListing
{
    private string _originalEntryString;
    public string Name { get; set;  } 
    public bool? Enabled { get; private set; }
    
    public bool IsNoDelete { get; private set; }
    public string NoDeletePrefix { get; private set; }

    public ModListing(string entryString)
    {
        _originalEntryString = entryString;
        Enabled = FormatHandler.GetModActivationStatus(_originalEntryString);
        Name = FormatHandler.TrimModActivationStatus(entryString);
        IsNoDelete = CommonFuncs.IsNoDelete(Name, out var noDeletePrefix);
        NoDeletePrefix = noDeletePrefix;
        Name = CommonFuncs.RemoveNoDeletePrefix(Name);
    }
    
    
    
    public string GetCurrentEntryString()
    {
        string prefix = string.Empty;
        if (!Enabled.HasValue)
        {
            prefix = "*";
        }
        else if (Enabled.Value == true)
        {
            prefix = "+";
        }
        else
        {
            prefix = "-";
        }

        if (IsNoDelete)
        {
            if (NoDeletePrefix == string.Empty)
            {
                NoDeletePrefix = "[NoDelete]";
            }
            prefix += NoDeletePrefix + " ";
        }
        
        return prefix + Name;
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

    public bool Equals(ModListing? other)
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