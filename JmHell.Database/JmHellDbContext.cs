using JmHell.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace JmHell.Database;

public class JmHellDbContext(DbContextOptions<JmHellDbContext> options) : DbContext(options)
{
    public DbSet<Album> Albums { get; set; } 
    public DbSet<Page> Images { get; set; } 
    public DbSet<Photograph> Episodes { get; set; }
    public DbSet<Tag> Tags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure many-to-many relationship between Tags and Albums
        modelBuilder.Entity<Tag>()
            .HasMany(t => t.Albums)
            .WithMany(a => a.Tags)
            .UsingEntity(j => j.ToTable("AlbumTagsRelation"));

        // Configure one-to-many relationship between Album and Photograph
        modelBuilder.Entity<Album>()
            .HasMany(a => a.Episodes)
            .WithOne(p => p.Album)
            .HasForeignKey(p => p.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure one-to-many relationship between Photograph and Pages
        modelBuilder.Entity<Photograph>()
            .HasMany(p => p.Pages)
            .WithOne(pg => pg.Photograph)
            .HasForeignKey(pg => pg.PhotographId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}