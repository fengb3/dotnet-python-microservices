var builder = DistributedApplication.CreateBuilder(args);


// Add Redis container
var redis = builder.AddRedis("redis").WithRedisInsight();

var web = builder.AddProject<Projects.JmHell>("web")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("CONSUMER_GROUP", "a-consumers")
    .WithReference(redis)
    .WaitFor(redis);
;

// Add API Service with Redis reference
// var apiService = builder
//     .AddProject<Projects.MicroservicesApp_ApiService>("apiservice")
//     .WithHttpHealthCheck("/health")
//     .WithEnvironment("CONSUMER_GROUP", "a-consumers")
//     .WithReference(redis)
//     .WaitFor(redis);

// Add Python worker using Aspire Python hosting integration
var pythonWorker = builder
    .AddPythonApp(name: "worker", appDirectory: "../JmHell.Worker", scriptPath: "./main.py")
    .WithUv()
    .WithEnvironment("CONSUMER_GROUP", "a-consumers")
    .WithReference(redis)
    .WaitFor(redis);


builder.Build().Run();