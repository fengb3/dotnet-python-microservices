var builder = DistributedApplication.CreateBuilder(args);

// Add Redis container
var redis = builder.AddRedis("redis")
    .WithRedisCommander();

// Add API Service with Redis reference
var apiService = builder.AddProject<Projects.MicroservicesApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(redis);

// Add Python worker as executable
var pythonWorker = builder.AddExecutable(
    "python-worker",
    "python3",
    workingDirectory: "../PythonWorker",
    args: ["worker.py"])
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.MicroservicesApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
