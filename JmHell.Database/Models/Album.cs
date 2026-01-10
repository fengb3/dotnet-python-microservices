namespace JmHell.Database.Models;

public class Album : BaseModel<string>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Many-to-many navigation property with Tags
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    
    // One-to-many navigation property - Album has many Photographs
    public ICollection<Photograph> Episodes { get; set; } = new List<Photograph>();
}