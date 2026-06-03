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
using QuraEx.Authoring.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace QuraEx.Authoring.Tests.Integration;

/// <summary>
/// Test factory: real Postgres via Testcontainers, in-memory MassTransit bus, test RS256 key pair.
/// </summary>
public sealed class AuthoringApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("authoring_test")
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

        // Apply EF migrations before tests hit the database
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
