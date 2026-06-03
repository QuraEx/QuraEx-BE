using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.ServiceDiscovery;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── YARP Reverse Proxy ─────────────────────────────────────────────────────
// AddServiceDiscoveryDestinationResolver integrates Aspire service discovery
// with YARP so that http://authoring in cluster config resolves to the real address.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// ── JWT Bearer (RS256 — PUBLIC KEY ONLY in config) ─────────────────────────
// Private key MUST stay in user-secrets or env, NEVER in any appsettings file.
// Asymmetric key: public repo holds only public key; a leaked public key cannot forge tokens.
// Swap Jwt:Issuer to OpenIddict authority when Identity service is built.
var jwtSection = builder.Configuration.GetSection("Jwt");
var publicKeyPem = jwtSection["PublicKeyPem"]
    ?? throw new InvalidOperationException(
        "Jwt:PublicKeyPem is required. Set via user-secrets (key: Jwt:PublicKeyPem) " +
        "or GATEWAY__JWT__PUBLICKEYPEM env var. Must be RSA public key PEM only.");

using var rsa = RSA.Create();
rsa.ImportFromPem(publicKeyPem);
var rsaKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
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

// ── Rate Limiting (fixed-window, configurable) ─────────────────────────────
var rlSection = builder.Configuration.GetSection("RateLimiting");
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.Window = TimeSpan.FromSeconds(rlSection.GetValue("WindowSeconds", 60));
        limiter.PermitLimit = rlSection.GetValue("PermitLimit", 100);
        limiter.QueueLimit = rlSection.GetValue("QueueLimit", 0);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapReverseProxy();

await app.RunAsync();
