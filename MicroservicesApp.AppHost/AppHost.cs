var builder = DistributedApplication.CreateBuilder(args);


// Add Redis container
var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithRedisInsight();

// Add Postgres container (+ a named database)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("jm-hell-data")
    .WithPgAdmin();

// Add a Named Database
var db = postgres.AddDatabase("jm-hell-db");

// Add Web App with Redis/Postgres reference
var web = builder.AddProject<Projects.JmHell>("web")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("CONSUMER_GROUP", "jm-consumers")
    .WithReference(redis)
    .WithReference(db, connectionName: "db")
    .WaitFor(redis)
    .WaitFor(db);

// Add Python worker using Aspire Python hosting integration
var pythonWorker = builder
    .AddPythonApp(name: "worker", appDirectory: "../JmHell.Worker", scriptPath: "./main.py")
    .WithUv()
    .WithEnvironment("CONSUMER_GROUP", "jm-consumers")
    .WithReference(redis)
    // .WithReference(db, connectionName: "db") // we dont need db for python worker
    .WaitFor(redis)
    .WaitFor(db);


builder.Build().Run();