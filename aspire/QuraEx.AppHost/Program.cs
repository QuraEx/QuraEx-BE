var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ──────────────────────────────────────────────────────────
// One Postgres container per service (DB-per-service isolation, mirrors prod).
// Only postgres-authoring scaffolded this round; add others as services are built.
var postgresAuthoring = builder
    .AddPostgres("postgres-authoring")
    .WithPgAdmin();   // pgAdmin UI in dev

// Uncomment as each service is implemented:
// var postgresIdentity   = builder.AddPostgres("postgres-identity");
// var postgresWorkspace  = builder.AddPostgres("postgres-workspace");
// var postgresTestArtifact = builder.AddPostgres("postgres-testartifact");
// var postgresAiGen      = builder.AddPostgres("postgres-ai-generation");
// var postgresExecution  = builder.AddPostgres("postgres-execution");
// var postgresIntegration = builder.AddPostgres("postgres-integration");

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();  // RabbitMQ management UI

var redis = builder.AddRedis("redis");

// ── Services ────────────────────────────────────────────────────────────────
// WaitFor ensures Postgres + RabbitMQ are healthy before the service starts.
// Without WaitFor, first cold-start crashes on DB-not-ready; Aspire does NOT
// block app start on resource health automatically.

// Gateway and Authoring are referenced as project resources once their .csproj exist.
// Wiring shown below — uncomment after Phase 4/5:
//
// var gateway = builder
//     .AddProject<Projects.QuraEx_Gateway>("gateway")
//     .WithReference(redis)
//     .WaitFor(rabbitmq);
//
// var authoring = builder
//     .AddProject<Projects.QuraEx_Authoring_Api>("authoring")
//     .WithReference(postgresAuthoring)
//     .WithReference(rabbitmq)
//     .WithReference(redis)
//     .WaitFor(postgresAuthoring)
//     .WaitFor(rabbitmq);

builder.Build().Run();
