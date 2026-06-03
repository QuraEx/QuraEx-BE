var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ──────────────────────────────────────────────────────────
// One Postgres container per service (DB-per-service isolation, mirrors prod).
var postgresAuthoring = builder
    .AddPostgres("postgres-authoring")
    .WithPgAdmin();

// Uncomment as each service is added:
// var postgresIdentity    = builder.AddPostgres("postgres-identity");
// var postgresWorkspace   = builder.AddPostgres("postgres-workspace");
// var postgresTestArtifact = builder.AddPostgres("postgres-testartifact");
// var postgresAiGen       = builder.AddPostgres("postgres-ai-generation");
// var postgresExecution   = builder.AddPostgres("postgres-execution");
// var postgresIntegration = builder.AddPostgres("postgres-integration");

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var redis = builder.AddRedis("redis");

// ── Services ────────────────────────────────────────────────────────────────
// WaitFor prevents crash-loops on cold-container start — Aspire does NOT block
// app start on resource health without explicit WaitFor.

var gateway = builder
    .AddProject<Projects.QuraEx_Gateway>("gateway")
    .WithReference(redis)
    .WaitFor(rabbitmq);

var authoring = builder
    .AddProject<Projects.QuraEx_Authoring_Api>("authoring")
    .WithReference(postgresAuthoring)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgresAuthoring)
    .WaitFor(rabbitmq);

_ = gateway.WithReference(authoring);

builder.Build().Run();
