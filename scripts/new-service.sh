#!/usr/bin/env bash
# scripts/new-service.sh <PascalName> [--db mongo]
#
# Scaffolds a new QuraEx microservice from the authoring template.
# The script is ATOMIC: stages in services/.scaffold-tmp-<slug>/ (same depth
# as authoring so relative ProjectReferences resolve), validates fully
# (build + EF migration + guards), then moves into services/<slug>/ and
# registers in QuraEx.slnx. Any failure triggers rollback.
#
# Usage:
#   ./scripts/new-service.sh Identity
#   ./scripts/new-service.sh Notification --db mongo

set -euo pipefail

# ── CONSTANTS ────────────────────────────────────────────────────────────────

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SLN="${REPO_ROOT}/QuraEx.slnx"
AUTHORING_SRC="${REPO_ROOT}/services/authoring"

# ── ARG PARSE ────────────────────────────────────────────────────────────────

NAME=""
DB_MODE="postgres"  # or "mongo"

for arg in "$@"; do
    if [[ "$arg" == "--db" ]]; then
        :
    elif [[ "$arg" == "mongo" ]]; then
        DB_MODE="mongo"
    elif [[ "$arg" == "--db=mongo" ]]; then
        DB_MODE="mongo"
    elif [[ "$arg" =~ ^-- ]]; then
        echo "ERROR: Unknown flag: $arg" >&2
        exit 1
    else
        NAME="$arg"
    fi
done

if [[ -z "$NAME" ]]; then
    echo "Usage: $0 <PascalName> [--db mongo]" >&2
    echo "  Examples: $0 Identity" >&2
    echo "            $0 Notification --db mongo" >&2
    exit 1
fi

# ── NAME DERIVATIONS ─────────────────────────────────────────────────────────

# kebab-case slug: Identity → identity, TestArtifact → test-artifact
SLUG="$(echo "$NAME" | sed -E 's/([a-z0-9])([A-Z])/\1-\2/g' | tr '[:upper:]' '[:lower:]')"

# underscore slug: test-artifact → test_artifact
SLUG_US="$(echo "$SLUG" | tr '-' '_')"

# UPPER_SNAKE env prefix: test_artifact → TEST_ARTIFACT
ENV_PREFIX="$(echo "$SLUG_US" | tr '[:lower:]' '[:upper:]')"

echo "=== QuraEx Service Scaffolder ==="
echo "Name       : QuraEx.${NAME}.*"
echo "Slug       : ${SLUG}"
echo "Env prefix : ${ENV_PREFIX}__"
echo "DB mode    : ${DB_MODE}"
echo ""

# ── STAGING DIR (same depth as authoring so relative refs resolve) ───────────
# Stage at services/.scaffold-tmp-<slug>/ so that
#   services/.scaffold-tmp-<slug>/QuraEx.<Name>.Api/
# is 3 levels below repo root — identical depth to authoring → relative
# ProjectReferences (..\..\..\building-blocks\...) resolve correctly.

STAGE="${REPO_ROOT}/services/.scaffold-tmp-${SLUG}"

# ── GUARDS ───────────────────────────────────────────────────────────────────

TARGET_DIR="${REPO_ROOT}/services/${SLUG}"
TARGET_API_CSPROJ="${TARGET_DIR}/QuraEx.${NAME}.Api/QuraEx.${NAME}.Api.csproj"

# Refuse if already scaffolded (csproj exists and is not just a .gitkeep)
if [[ -f "${TARGET_API_CSPROJ}" ]]; then
    echo "ERROR: ${TARGET_API_CSPROJ} already exists — service '${NAME}' is already scaffolded." >&2
    exit 1
fi

# Refuse if target dir exists with non-.gitkeep files (half/dirty state)
if [[ -d "${TARGET_DIR}" ]]; then
    non_gitkeep_count=$(find "${TARGET_DIR}" -type f ! -name ".gitkeep" 2>/dev/null | wc -l | tr -d ' ')
    if [[ "$non_gitkeep_count" -gt 0 ]]; then
        echo "ERROR: services/${SLUG}/ contains non-.gitkeep files — possible half-scaffolded state." >&2
        echo "       Remove it manually before retrying." >&2
        exit 1
    fi
fi

# Refuse if staging dir already exists (leftover from prior failed run)
if [[ -d "${STAGE}" ]]; then
    echo "ERROR: Staging dir ${STAGE} already exists. Remove it and retry:" >&2
    echo "       rm -rf '${STAGE}'" >&2
    exit 1
fi

# ── ROLLBACK TRAP ────────────────────────────────────────────────────────────

cleanup() {
    local exit_code=$?
    echo ""
    echo "--- Rollback triggered (exit $exit_code) ---"
    # Remove staging dir
    rm -rf "${STAGE}" 2>/dev/null || true
    # Restore slnx snapshot if we made one
    if [[ -f "${SLN}.bak" ]]; then
        mv "${SLN}.bak" "${SLN}"
        echo "Restored QuraEx.slnx from snapshot."
    fi
    # If we already moved the service into place but slnx was already cleaned up, remove it
    if [[ -d "${TARGET_DIR}" ]]; then
        # Only remove if it's NOT a clean gitkeep-only skeleton (we may have replaced it)
        # We remove it only if we created it (it has real content)
        non_gitkeep=$(find "${TARGET_DIR}" -type f ! -name ".gitkeep" 2>/dev/null | wc -l | tr -d ' ')
        if [[ "$non_gitkeep" -gt 0 ]]; then
            rm -rf "${TARGET_DIR}"
            echo "Removed services/${SLUG}/ (rollback)."
        fi
    fi
    echo "Rollback complete. Repository state is unchanged."
}

trap cleanup ERR INT TERM

# ── PORTABLE SED HELPER ──────────────────────────────────────────────────────

# Works on both BSD (macOS) and GNU sed: no -i, uses tmp file.
# Delimiter is | so patterns with / work without escaping.
replace_in_file() {
    local file="$1"
    local pattern="$2"
    local replacement="$3"
    sed "s|${pattern}|${replacement}|g" "${file}" > "${file}.tmp" && mv "${file}.tmp" "${file}"
}

# ── STEP 1: COPY AUTHORING TO STAGING DIR ────────────────────────────────────

echo "[1/11] Copying authoring template to staging dir..."
cp -R "${AUTHORING_SRC}" "${STAGE}"

# Remove bin and obj directories (build artifacts, not needed)
find "${STAGE}" -type d \( -name "bin" -o -name "obj" \) | while read -r d; do
    rm -rf "${d}"
done

echo "       Staged at: ${STAGE}"

# ── STEP 2: RENAME PROJECT DIRS AND .csproj FILES ────────────────────────────

echo "[2/11] Renaming project directories and .csproj files..."

# The 5 project suffixes
for suffix in Api Contracts Domain Infrastructure Tests; do
    old_dir="${STAGE}/QuraEx.Authoring.${suffix}"
    new_dir="${STAGE}/QuraEx.${NAME}.${suffix}"

    if [[ -d "${old_dir}" ]]; then
        mv "${old_dir}" "${new_dir}"

        # Rename the .csproj inside
        old_csproj="${new_dir}/QuraEx.Authoring.${suffix}.csproj"
        new_csproj="${new_dir}/QuraEx.${NAME}.${suffix}.csproj"
        if [[ -f "${old_csproj}" ]]; then
            mv "${old_csproj}" "${new_csproj}"
        fi
    else
        echo "WARNING: Expected dir ${old_dir} not found — skipping rename." >&2
    fi
done

# ── STEP 3: GLOBAL SED PASS OVER ALL TEXT FILES ──────────────────────────────

echo "[3/11] Running global sed rename pass..."

# Collect all text files (exclude bin/obj — already removed, but be safe)
while IFS= read -r -d '' file; do
    # Skip binary files (check if file command says it's binary)
    if file "${file}" | grep -qE 'binary|data|ELF'; then
        continue
    fi

    # Namespace + assembly references: QuraEx.Authoring → QuraEx.<Name>
    replace_in_file "${file}" "QuraEx\.Authoring" "QuraEx.${NAME}"

    # User secrets ID
    replace_in_file "${file}" "quraex-authoring-secrets" "quraex-${SLUG}-secrets"

    # Aspire / compose resource name
    replace_in_file "${file}" "postgres-authoring" "postgres-${SLUG}"

    # Dockerfile service path segments
    replace_in_file "${file}" "services/authoring/" "services/${SLUG}/"

    # EF design-time connection string DB name
    replace_in_file "${file}" "authoring_design" "${SLUG_US}_design"

    # Advisory lock string in MigrationHostedService
    replace_in_file "${file}" "authoring_migration" "${SLUG_US}_migration"

    # MassTransit serviceName in Extensions.cs
    replace_in_file "${file}" 'serviceName: "authoring"' "serviceName: \"${SLUG}\""

    # Program.cs error message env prefix (uppercase)
    replace_in_file "${file}" "AUTHORING__" "${ENV_PREFIX}__"

    # Method name: AddAuthoringInfrastructure → Add<Name>Infrastructure
    replace_in_file "${file}" "AddAuthoringInfrastructure" "Add${NAME}Infrastructure"

    # Dockerfile comments / docker tag: "Authoring API" → "<Name> API", "quraex-authoring" → "quraex-<slug>"
    replace_in_file "${file}" "Authoring API" "${NAME} API"
    replace_in_file "${file}" "quraex-authoring" "quraex-${SLUG}"

done < <(find "${STAGE}" -type f \( ! -name "*.tmp" \) -print0)

echo "       Global sed pass complete."

# ── STEP 4: DELETE BUSINESS FILES, RESTORE .gitkeep ──────────────────────────

echo "[4/11] Deleting business files and restoring .gitkeep placeholders..."

API_DIR="${STAGE}/QuraEx.${NAME}.Api"
CONTRACTS_DIR="${STAGE}/QuraEx.${NAME}.Contracts"
DOMAIN_DIR="${STAGE}/QuraEx.${NAME}.Domain"
INFRA_DIR="${STAGE}/QuraEx.${NAME}.Infrastructure"
TESTS_DIR="${STAGE}/QuraEx.${NAME}.Tests"

# API: Features/UserStories/* → keep Features/.gitkeep
rm -rf "${API_DIR}/Features/UserStories"
find "${API_DIR}/Features" -type f ! -name ".gitkeep" -delete 2>/dev/null || true
touch "${API_DIR}/Features/.gitkeep"

# Contracts: delete business contracts → .gitkeep
rm -f "${CONTRACTS_DIR}/MembershipChanged.cs" \
      "${CONTRACTS_DIR}/StoryChanged.cs" \
      "${CONTRACTS_DIR}/StoryChangedEvent.cs" 2>/dev/null || true
# Remove any remaining .cs files in contracts root that are business-specific
find "${CONTRACTS_DIR}" -maxdepth 1 -name "*.cs" -delete 2>/dev/null || true
touch "${CONTRACTS_DIR}/.gitkeep"

# Domain: Entities/* and DomainEvents/*
rm -rf "${DOMAIN_DIR}/Entities"
mkdir -p "${DOMAIN_DIR}/Entities"
touch "${DOMAIN_DIR}/Entities/.gitkeep"

rm -rf "${DOMAIN_DIR}/DomainEvents"
mkdir -p "${DOMAIN_DIR}/DomainEvents"
touch "${DOMAIN_DIR}/DomainEvents/.gitkeep"

# Infrastructure: Consumers/*, DomainEventHandlers/*, MembershipSnapshot.cs
rm -rf "${INFRA_DIR}/Consumers"
mkdir -p "${INFRA_DIR}/Consumers"
touch "${INFRA_DIR}/Consumers/.gitkeep"

rm -rf "${INFRA_DIR}/DomainEventHandlers"
mkdir -p "${INFRA_DIR}/DomainEventHandlers"
touch "${INFRA_DIR}/DomainEventHandlers/.gitkeep"

rm -f "${INFRA_DIR}/MembershipSnapshot.cs" 2>/dev/null || true

# Infrastructure: business EF configurations
rm -f "${INFRA_DIR}/EntityConfigurations/UserStoryConfiguration.cs" \
      "${INFRA_DIR}/EntityConfigurations/AcceptanceCriteriaConfiguration.cs" \
      "${INFRA_DIR}/EntityConfigurations/BusinessRuleConfiguration.cs" 2>/dev/null || true

# Infrastructure: Migrations (will regenerate with EF)
rm -rf "${INFRA_DIR}/Migrations"
mkdir -p "${INFRA_DIR}/Migrations"
# No .gitkeep here — EF will populate it

# Tests: delete UserStoryApiTests.cs (will write HealthEndpointTests.cs below)
rm -f "${TESTS_DIR}/Integration/UserStoryApiTests.cs" 2>/dev/null || true

echo "       Business files removed."

# ── STEP 5: WRITE BAKED STUB TEMPLATES ───────────────────────────────────────

echo "[5/11] Writing baked stub templates (Program.cs, AppDbContext.cs, etc.)..."

# ── 5a: Program.cs ────────────────────────────────────────────────────────────
cat > "${API_DIR}/Program.cs" << PROGRAM_CS
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using QuraEx.${NAME}.Infrastructure;
using QuraEx.BuildingBlocks;
using QuraEx.BuildingBlocks.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.Add${NAME}Infrastructure(builder.Configuration, builder.Environment);
builder.Services.AddBuildingBlocks(typeof(Program), typeof(QuraEx.${NAME}.Infrastructure.Extensions));

// JWT Bearer — same RS256 public key as gateway; services validate independently
// (never trust a gateway-injected identity header — service-side validation)
var jwtSection = builder.Configuration.GetSection("Jwt");
var publicKeyPem = jwtSection["PublicKeyPem"]
    ?? throw new InvalidOperationException(
        "Jwt:PublicKeyPem required. Set via user-secrets or ${ENV_PREFIX}__JWT__PUBLICKEYPEM.");

using var rsa = RSA.Create();
rsa.ImportFromPem(publicKeyPem);
var rsaKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = rsaKey,
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseQuraExExceptionHandling();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();

await app.RunAsync();

// Makes the compiler-generated Program class visible to the test project (required by WebApplicationFactory<Program>)
#pragma warning disable S1118
public partial class Program { }
#pragma warning restore S1118
PROGRAM_CS

# ── 5b: AppDbContext.cs ───────────────────────────────────────────────────────
cat > "${INFRA_DIR}/AppDbContext.cs" << DBCONTEXT_CS
using Microsoft.EntityFrameworkCore;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.${NAME}.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // DomainEvent is in-process only — never persisted; prevent EF from treating it as an entity
        modelBuilder.Ignore<QuraEx.BuildingBlocks.DomainEvent>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyQuraExConventions();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.ApplyQuraExConventions();
    }
}
DBCONTEXT_CS

# ── 5c: InfraEntityConfigurations.cs ─────────────────────────────────────────
cat > "${INFRA_DIR}/EntityConfigurations/InfraEntityConfigurations.cs" << INFRA_CONF_CS
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.${NAME}.Infrastructure.EntityConfigurations;

// Infra tables: no ISoftDeletable, no audit columns, no soft-delete filter.

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("${SLUG_US}_outbox_message");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Seq).UseIdentityByDefaultColumn();
        builder.Property(x => x.Type).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.HasIndex(x => x.ProcessedAt);
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("${SLUG_US}_processed_message");
        builder.HasKey(x => x.MessageId);
        builder.HasIndex(x => x.ProcessedAt);
    }
}
INFRA_CONF_CS

# ── 5d: <Name>ApiFactory.cs (replaces AuthoringApiFactory.cs) ────────────────
rm -f "${TESTS_DIR}/Integration/AuthoringApiFactory.cs" 2>/dev/null || true

cat > "${TESTS_DIR}/Integration/${NAME}ApiFactory.cs" << FACTORY_CS
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuraEx.${NAME}.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace QuraEx.${NAME}.Tests.Integration;

/// <summary>
/// Test factory: real Postgres via Testcontainers, in-memory MassTransit bus, test RS256 key pair.
/// </summary>
public sealed class ${NAME}ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("${SLUG_US}_test")
        .WithUsername("quraex")
        .WithPassword("quraex_pw")
        .Build();

    private RSA _signingKey = null!;

    /// <summary>Creates a signed JWT that passes this factory's RS256 validation.</summary>
    public string CreateTestJwt(Guid userId)
    {
        var credentials = new SigningCredentials(
            new RsaSecurityKey(_signingKey),
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "quraex-test",
            audience: "quraex-api",
            claims: [new Claim("sub", userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _signingKey = RSA.Create(2048);
        var pubKeyPem = _signingKey.ExportSubjectPublicKeyInfoPem();

        builder.UseEnvironment("Development");

        // Inject test config before services are built
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PublicKeyPem"] = pubKeyPem,
                ["Jwt:Issuer"] = "quraex-test",
                ["Jwt:Audience"] = "quraex-api",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(_signingKey.ExportParameters(includePrivateParameters: false)),
                    ValidateIssuer = true,
                    ValidIssuer = "quraex-test",
                    ValidateAudience = true,
                    ValidAudience = "quraex-api",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

            // ── Replace AppDbContext → test Postgres ──────────────────────────
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString())
                    .UseSnakeCaseNamingConvention());
            services.RemoveAll<DbContext>();
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

            // ── Replace RabbitMQ bus → in-memory (no broker needed in tests) ──
            // Remove all production MassTransit registrations first; partial removal leaves
            // duplicate "masstransit-bus" health checks in the test service provider.
            var masstransitDescriptors = services
                .Where(IsMassTransitDescriptor)
                .ToList();
            masstransitDescriptors.ForEach(d => services.Remove(d));

            services.RemoveAll<IConfigureOptions<HealthCheckServiceOptions>>();
            services.RemoveAll<IPostConfigureOptions<HealthCheckServiceOptions>>();
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            // Remove bus-level singletons so AddMassTransit can register fresh in-memory ones
            foreach (var busType in new[] { typeof(IBus), typeof(IBusControl), typeof(ISendEndpointProvider), typeof(IPublishEndpoint) })
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == busType);
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
            }

            // Register test harness over an in-memory bus so tests can assert published events.
            services.AddMassTransitTestHarness(x => x.UsingInMemory());
        });
    }

    private static bool IsMassTransitDescriptor(ServiceDescriptor descriptor)
    {
        return IsMassTransitType(descriptor.ServiceType)
            || IsMassTransitType(descriptor.ImplementationType)
            || IsMassTransitType(descriptor.ImplementationInstance?.GetType());
    }

    private static bool IsMassTransitType(Type? type)
    {
        return type?.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true
            || type?.Assembly.GetName().Name?.StartsWith("MassTransit", StringComparison.Ordinal) == true;
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Apply EF migrations before tests hit the database (exercises InitialCreate migration)
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        _signingKey.Dispose();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
FACTORY_CS

# ── 5e: HealthEndpointTests.cs ───────────────────────────────────────────────
cat > "${TESTS_DIR}/Integration/HealthEndpointTests.cs" << HEALTH_TESTS_CS
using System.Net;
using FluentAssertions;
using Xunit;

namespace QuraEx.${NAME}.Tests.Integration;

public sealed class HealthEndpointTests(${NAME}ApiFactory factory)
    : IClassFixture<${NAME}ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
HEALTH_TESTS_CS

echo "       Stub templates written."

# ── STEP 6: REWRITE appsettings.Development.json (shared dev public key) ──────

echo "[6/11] Writing appsettings.Development.json with the shared dev public key..."

DEV_SETTINGS="${API_DIR}/appsettings.Development.json"

# System-wide dev keypair: the gateway signs tokens (private key in user-secrets/env),
# every service validates with the SAME RS256 PUBLIC key. authoring + gateway both commit
# this public key in their appsettings.Development.json — services do the same so a dev
# token from the gateway validates everywhere. Public-only (cannot sign) → safe in git;
# the private key is never committed. Program.cs reads Jwt:PublicKeyPem at startup, so a
# valid PEM must be present for `dotnet run` and integration tests to boot.
# Extract it from authoring at scaffold time (stays in sync if the dev key is rotated).
AUTHORING_DEV_SETTINGS="${REPO_ROOT}/services/authoring/QuraEx.Authoring.Api/appsettings.Development.json"
PUBKEY_JSON=$(grep -o '"PublicKeyPem": "[^"]*"' "${AUTHORING_DEV_SETTINGS}" | sed 's/"PublicKeyPem": //')
if [[ -z "${PUBKEY_JSON}" ]]; then
    echo "ERROR: could not extract shared dev PublicKeyPem from ${AUTHORING_DEV_SETTINGS}." >&2
    exit 1
fi

cat > "${DEV_SETTINGS}" << DEV_SETTINGS_JSON
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information",
      "MassTransit": "Information"
    }
  },
  "ConnectionStrings": {
    "postgres-${SLUG}": "Host=localhost;Port=5432;Database=${SLUG_US};Username=postgres;Password=postgres"
  },
  "Jwt": {
    "PublicKeyPem": ${PUBKEY_JSON}
  }
}
DEV_SETTINGS_JSON

echo "       appsettings.Development.json: shared dev public key written (validation only)."
echo "       Override per-service via user-secrets or ${ENV_PREFIX}__JWT__PUBLICKEYPEM if needed."

# ── STEP 7: ASSERT csproj contains correct UserSecretsId ─────────────────────

echo "[7/11] Asserting csproj secrets ID..."

API_CSPROJ="${API_DIR}/QuraEx.${NAME}.Api.csproj"
if grep -q "quraex-authoring-secrets" "${API_CSPROJ}" 2>/dev/null; then
    echo "ERROR: ${API_CSPROJ} still contains 'quraex-authoring-secrets' after sed pass." >&2
    exit 1
fi
if ! grep -q "quraex-${SLUG}-secrets" "${API_CSPROJ}" 2>/dev/null; then
    echo "ERROR: ${API_CSPROJ} does not contain 'quraex-${SLUG}-secrets'." >&2
    exit 1
fi
echo "       csproj UserSecretsId: quraex-${SLUG}-secrets — OK."

# ── STEP 8: BUILD (validate compile) ─────────────────────────────────────────

echo "[8/11] Building staged service (compile validation)..."

TESTS_CSPROJ="${TESTS_DIR}/QuraEx.${NAME}.Tests.csproj"

# Restore tool manifest once (idempotent)
echo "       Restoring dotnet tools..."
(cd "${REPO_ROOT}" && dotnet tool restore --verbosity quiet)

echo "       Running dotnet build..."
if ! dotnet build "${TESTS_CSPROJ}" \
        --verbosity quiet \
        -p:TreatWarningsAsErrors=true \
        2>&1; then
    echo "ERROR: Build failed. Fix errors above before retrying." >&2
    exit 1
fi
echo "       Build: PASSED."

# ── STEP 9: EF MIGRATION (skip for mongo) ────────────────────────────────────

if [[ "${DB_MODE}" == "postgres" ]]; then
    echo "[9/11] Generating EF InitialCreate migration..."

    INFRA_CSPROJ="${INFRA_DIR}/QuraEx.${NAME}.Infrastructure.csproj"
    API_CSPROJ_FULL="${API_DIR}/QuraEx.${NAME}.Api.csproj"

    (cd "${REPO_ROOT}" && dotnet ef migrations add InitialCreate \
        --project "${INFRA_CSPROJ}" \
        --startup-project "${API_CSPROJ_FULL}" \
        --verbose \
        2>&1)

    # Assert the generated migration contains the infra table names
    MIGRATION_FILE="$(find "${INFRA_DIR}/Migrations" -name "*_InitialCreate.cs" | head -1)"
    if [[ -z "${MIGRATION_FILE}" ]]; then
        echo "ERROR: InitialCreate migration file not found after 'dotnet ef migrations add'." >&2
        exit 1
    fi

    if ! grep -q "${SLUG_US}_outbox_message" "${MIGRATION_FILE}"; then
        echo "ERROR: Migration does not contain '${SLUG_US}_outbox_message'. Check InfraEntityConfigurations.cs." >&2
        exit 1
    fi
    if ! grep -q "${SLUG_US}_processed_message" "${MIGRATION_FILE}"; then
        echo "ERROR: Migration does not contain '${SLUG_US}_processed_message'. Check InfraEntityConfigurations.cs." >&2
        exit 1
    fi

    echo "       EF migration: InitialCreate — PASSED."
    echo "       Tables verified: ${SLUG_US}_outbox_message, ${SLUG_US}_processed_message."
else
    echo "[9/11] Skipping EF migration (--db mongo)."
fi

# ── STEP 10: GUARDS (no residual authoring/business tokens) ──────────────────

echo "[10/11] Running residual-token guards..."

GUARD_FAILED=0

# Guard: no authoring/UserStory/MembershipSnapshot (case-insensitive).
# Exclude EF-generated Designer/snapshot files: they may legitimately reference
# BuildingBlocks identifiers that share tokens with authoring column names.
mapfile -d '' residual_files < <(find "${STAGE}" -type f \
    ! -path "*/bin/*" ! -path "*/obj/*" ! -name "*.tmp" \
    ! -name "*.Designer.cs" ! -name "AppDbContextModelSnapshot.cs" \
    -print0)
if [[ ${#residual_files[@]} -gt 0 ]]; then
    residual_tokens=$(grep -iln "authoring\|userstory\|membershipsnapshot" \
        "${residual_files[@]}" 2>/dev/null || true)
else
    residual_tokens=""
fi

if [[ -n "${residual_tokens}" ]]; then
    echo "ERROR: Residual 'authoring'/'UserStory'/'MembershipSnapshot' tokens found:" >&2
    echo "${residual_tokens}" >&2
    GUARD_FAILED=1
fi

# Guard: no PRIVATE key material in any staged source file.
# A committed dev PUBLIC key is the repo convention (gateway signs, services validate with
# the shared public key) and is allowed. A PRIVATE key must NEVER be committed — catch it
# everywhere (covers BEGIN PRIVATE KEY / BEGIN RSA PRIVATE KEY / BEGIN EC PRIVATE KEY).
mapfile -d '' key_files < <(find "${STAGE}" -type f \
    ! -path "*/bin/*" ! -path "*/obj/*" ! -name "*.tmp" \
    -print0)
if [[ ${#key_files[@]} -gt 0 ]]; then
    key_leaks=$(grep -In "BEGIN [A-Z ]*PRIVATE KEY" "${key_files[@]}" 2>/dev/null || true)
else
    key_leaks=""
fi

if [[ -n "${key_leaks}" ]]; then
    echo "ERROR: PRIVATE key material found in staged files (never commit private keys):" >&2
    echo "${key_leaks}" >&2
    GUARD_FAILED=1
fi

# Guard: no quraex-authoring-secrets
mapfile -d '' secret_files < <(find "${STAGE}" -type f \
    ! -path "*/bin/*" ! -path "*/obj/*" ! -name "*.tmp" \
    -print0)
if [[ ${#secret_files[@]} -gt 0 ]]; then
    secret_leaks=$(grep -In "quraex-authoring-secrets" "${secret_files[@]}" 2>/dev/null || true)
else
    secret_leaks=""
fi

if [[ -n "${secret_leaks}" ]]; then
    echo "ERROR: Residual 'quraex-authoring-secrets' found:" >&2
    echo "${secret_leaks}" >&2
    GUARD_FAILED=1
fi

if [[ "${GUARD_FAILED}" -eq 1 ]]; then
    exit 1
fi

echo "       All guards passed — no residual tokens, no key material, no leaked secrets IDs."

# ── STEP 11: ATOMIC COMMIT ───────────────────────────────────────────────────

echo "[11/11] Atomic commit: snapshot slnx → replace services/<slug>/ → sln add..."

# Snapshot slnx before any mutation
cp "${SLN}" "${SLN}.bak"

# Remove the gitkeep-only skeleton (or recreate if it was absent)
rm -rf "${TARGET_DIR}"

# Move staged tree into place
mv "${STAGE}" "${TARGET_DIR}"

# Register all 5 projects in the solution
SOLUTION_FOLDER="/services/${SLUG}/"

dotnet sln "${SLN}" add \
    --solution-folder "${SOLUTION_FOLDER}" \
    "${TARGET_DIR}/QuraEx.${NAME}.Api/QuraEx.${NAME}.Api.csproj" \
    "${TARGET_DIR}/QuraEx.${NAME}.Contracts/QuraEx.${NAME}.Contracts.csproj" \
    "${TARGET_DIR}/QuraEx.${NAME}.Domain/QuraEx.${NAME}.Domain.csproj" \
    "${TARGET_DIR}/QuraEx.${NAME}.Infrastructure/QuraEx.${NAME}.Infrastructure.csproj" \
    "${TARGET_DIR}/QuraEx.${NAME}.Tests/QuraEx.${NAME}.Tests.csproj"

# Sanity build in final location
echo "       Sanity build from final location..."
if ! dotnet build "${TARGET_DIR}/QuraEx.${NAME}.Api/QuraEx.${NAME}.Api.csproj" \
        --verbosity quiet \
        -p:TreatWarningsAsErrors=true \
        2>&1; then
    echo "ERROR: Sanity build from final location failed." >&2
    exit 1
fi

# Clean up slnx backup — we're done, no rollback needed
rm -f "${SLN}.bak"

# Disarm the trap (success path)
trap - ERR INT TERM

# ── NEXT STEPS ───────────────────────────────────────────────────────────────

# Compute Aspire mangled project type: dots → underscores
ASPIRE_TYPE="Projects.QuraEx_${NAME}_Api"
# The NAME variable may contain dots if multi-part (not typical, but safe)
ASPIRE_TYPE="${ASPIRE_TYPE//./_}"

echo ""
echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║  QuraEx.${NAME} scaffolded successfully!"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo ""
echo "── Next Steps ──────────────────────────────────────────────────────────"
echo ""
echo "1. AppHost wiring (aspire/QuraEx.AppHost/Program.cs):"
echo ""
echo "   // Add Postgres resource for ${NAME}"
echo "   var postgres${NAME} = builder"
echo "       .AddPostgres(\"postgres-${SLUG}\")"
echo "       .WithPgAdmin();"
echo ""
echo "   var ${SLUG//-/}Api = builder"
echo "       .AddProject<${ASPIRE_TYPE}>(\"${SLUG}\")"
echo "       .WithReference(postgres${NAME}).WaitFor(postgres${NAME})"
echo "       .WithReference(rabbitmq).WaitFor(rabbitmq)"
echo "       .WithReference(redis).WaitFor(redis);"
echo ""
echo "   // Wire gateway → ${NAME}"
echo "   gateway.WithReference(${SLUG//-/}Api).WaitFor(${SLUG//-/}Api);"
echo ""
echo "2. Gateway route (gateway/QuraEx.Gateway/appsettings.json):"
echo "   Add a route cluster for ${NAME} with path /api/${SLUG}/{**catch-all}."
echo "   Use AuthorizationPolicy: \"default\" (or \"anonymous\" for auth-server services)."
echo ""
echo "3. Gateway YARP config snippet:"
echo "   \"${SLUG}-cluster\": {"
echo "     \"Destinations\": { \"${SLUG}\": { \"Address\": \"{${NAME}_BASE_URL}\" } }"
echo "   }"
echo "   Route: { \"RouteId\": \"${SLUG}\", \"ClusterId\": \"${SLUG}-cluster\","
echo "     \"Match\": { \"Path\": \"/api/${SLUG}/{**catch-all}\" },"
echo "     \"AuthorizationPolicy\": \"default\" }"
echo ""
echo "4. ci.yml: Add '${NAME}' to the service matrix and path filter for"
echo "   services/${SLUG}/."
echo ""
echo "5. Golden DB flow: After wiring AppHost, run:"
echo "   dotnet ef migrations has-pending-model-changes \\"
echo "     --project services/${SLUG}/QuraEx.${NAME}.Infrastructure \\"
echo "     --startup-project services/${SLUG}/QuraEx.${NAME}.Api \\"
echo "     --no-build"
echo "   Expected: no pending model changes."
echo ""
echo "6. CODEOWNERS: /services/${SLUG}/ is already covered by the wildcard rule."
echo ""
echo "── Service Location ────────────────────────────────────────────────────"
echo "   services/${SLUG}/"
echo "     QuraEx.${NAME}.Api/"
echo "     QuraEx.${NAME}.Contracts/"
echo "     QuraEx.${NAME}.Domain/"
echo "     QuraEx.${NAME}.Infrastructure/"
echo "     QuraEx.${NAME}.Tests/"
echo ""
echo "Done. services/${SLUG}/ is ready for business logic."
