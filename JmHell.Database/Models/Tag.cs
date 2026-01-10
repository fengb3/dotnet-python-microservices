namespace JmHell.Database.Models;

public class Tag : BaseModel<string>
{
    public string Name { get; set; } = string.Empty;

    public TagType Type { get; set; } = TagType.Category;

    // Many-to-many navigation property
    public virtual ICollection<Album> Albums { get; set; } = new List<Album>();

    public override string ToString() => $"{Name}";

    public enum TagType
    {
        Category,
        Artist,
        Language,
        Doujin,
        Character,
    }
}