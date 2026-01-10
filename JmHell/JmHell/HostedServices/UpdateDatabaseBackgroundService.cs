using JmHell.Database;
using JmHell.Services;
using Microsoft.EntityFrameworkCore;

namespace JmHell.HostedServices;

public class UpdateDatabaseBackgroundService(IServiceProvider rootSp, InitializationService initializationService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // migrate and seed database
        using var scope = rootSp.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<JmHellDbContext>();
        await dbContext.Database.MigrateAsync(stoppingToken);

        initializationService.CompleteInitialization();
    }
}