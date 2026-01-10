namespace JmHell.Database.Models;

public class Photograph : BaseModel<int>
{
    public ushort Index { get; set; }
    
    public required string Name { get; set; }
    
    // Foreign key for Album
    public string AlbumId { get; set; } = null!;
    
    // Many-to-one navigation property - Many Photographs belong to one Album
    public Album Album { get; set; } = null!;

    // One-to-many navigation property - One Photograph has many Pages
    public ICollection<Page> Pages { get; set; } = new List<Page>();
}