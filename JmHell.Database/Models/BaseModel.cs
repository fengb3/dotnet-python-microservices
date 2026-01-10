using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JmHell.Database.Models;

public class BaseModel<TKey> : BaseModel
{
    [Key]
    public TKey Id { get; set; }
}

public class BaseModel
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }
    
    public bool IsDelete { get; set; }
}