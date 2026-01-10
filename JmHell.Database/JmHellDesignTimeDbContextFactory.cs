using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JmHell.Database;

public class JmHellDesignTimeDbContextFactory : IDesignTimeDbContextFactory<JmHellDbContext>
{
    public JmHellDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JmHellDbContext>()
            .UseNpgsql("")
            .Options;
        
        return new JmHellDbContext(options);
    }
}