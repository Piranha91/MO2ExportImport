namespace MO2ExportImport;

public interface IListing
{
    public string Name { get; set; } 
    public bool? Enabled { get; }

    public bool Equals(IListing? other);

    public int GetIndexOf(IList<IListing> list);
}