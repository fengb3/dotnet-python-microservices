using Microsoft.Extensions.Hosting;

namespace JmHell.Database;

public static class Extensions
{
    public static IHostApplicationBuilder AddJmHellDatabase(this IHostApplicationBuilder builder)
    {
        // 注册数据库相关服务
        builder.AddNpgsqlDbContext<JmHellDbContext>(
            connectionName: "db");
        return builder;
    }
}