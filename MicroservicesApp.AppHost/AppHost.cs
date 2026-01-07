var builder = DistributedApplication.CreateBuilder(args);

// Add Redis container
var redis = builder.AddRedis("redis").WithRedisCommander();

// Add API Service with Redis reference
var apiService = builder
    .AddProject<Projects.MicroservicesApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(redis)
    .WaitFor(redis);

// Add Python worker using Aspire Python hosting integration
var pythonWorker = builder
    .AddPythonApp(name: "python-worker", appDirectory: "../PythonWorker", scriptPath: "worker.py")
    .WithUv()
    .WithReference(redis)
    .WaitFor(redis);

builder
    .AddProject<Projects.MicroservicesApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
