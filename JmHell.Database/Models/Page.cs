namespace JmHell.Database.Models;

public class Page : BaseModel<int>
{
    public ushort Index { get; set; }

    public required string Url { get; set; }

    // Foreign key for Photograph (many-to-one relationship)
    public int PhotographId { get; set; }
    
    // Many-to-one navigation property - Many Pages belong to one Photograph
    public Photograph Photograph { get; set; } = null!;
}