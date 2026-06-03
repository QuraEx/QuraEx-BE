using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace QuraEx.BuildingBlocks.Auth;

/// <summary>Abstraction over the authenticated user in the current request scope.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
}

/// <summary>Default implementation reading from the ASP.NET Core HttpContext claims principal.</summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? Principal?.FindFirstValue("sub");
            return sub is not null && Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
                         ?? Principal?.FindFirstValue("email");

    public IEnumerable<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];
}
