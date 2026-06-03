using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using QuraEx.Authoring.Api.Features.UserStories;
using QuraEx.Authoring.Infrastructure;
using QuraEx.BuildingBlocks;
using QuraEx.BuildingBlocks.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddAuthoringInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddBuildingBlocks(typeof(Program));

// JWT Bearer — same RS256 public key as gateway; services validate independently
// (never trust a gateway-injected identity header — service-side validation)
var jwtSection = builder.Configuration.GetSection("Jwt");
var publicKeyPem = jwtSection["PublicKeyPem"]
    ?? throw new InvalidOperationException(
        "Jwt:PublicKeyPem required. Set via user-secrets or AUTHORING__JWT__PUBLICKEYPEM.");

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

app.MapUserStoryEndpoints();

await app.RunAsync();

// Makes the compiler-generated Program class visible to the test project (required by WebApplicationFactory<Program>)
#pragma warning disable S1118
public partial class Program { }
#pragma warning restore S1118
