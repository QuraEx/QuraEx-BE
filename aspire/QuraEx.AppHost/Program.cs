var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ──────────────────────────────────────────────────────────
// One Postgres container per service (DB-per-service isolation, mirrors prod).
var postgresAuthoring = builder
    .AddPostgres("postgres-authoring")
    .WithPgAdmin();

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

await builder.Build().RunAsync();
