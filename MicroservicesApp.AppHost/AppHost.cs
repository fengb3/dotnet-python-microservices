var builder = DistributedApplication.CreateBuilder(args);


// Add Redis container
var redis = builder.AddRedis("redis").WithRedisInsight();

// Add Postgres container (+ a named database)
var db = builder.AddPostgres("postgres").AddDatabase("db");

var web = builder.AddProject<Projects.JmHell>("web")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("CONSUMER_GROUP", "a-consumers")
    .WithReference(redis)
    .WithReference(db, connectionName: "db")
    .WaitFor(redis)
    .WaitFor(db);

// Add API Service with Redis/Postgres reference
// var apiService = builder
//     .AddProject<Projects.MicroservicesApp_ApiService>("apiservice")
//     .WithHttpHealthCheck("/health")
//     .WithEnvironment("CONSUMER_GROUP", "a-consumers")
//     .WithReference(redis)
//     .WithReference(db)
//     .WaitFor(redis)
//     .WaitFor(db);

// Add Python worker using Aspire Python hosting integration
var pythonWorker = builder
    .AddPythonApp(name: "worker", appDirectory: "../JmHell.Worker", scriptPath: "./main.py")
    .WithUv()
    .WithEnvironment("CONSUMER_GROUP", "a-consumers")
    .WithReference(redis)
    // .WithReference(db, connectionName: "db") // we dont need db for python worker
    .WaitFor(redis)
    .WaitFor(db);


builder.Build().Run();