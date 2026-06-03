using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using QuraEx.BuildingBlocks.Auth;
using QuraEx.BuildingBlocks.Behaviors;

namespace QuraEx.BuildingBlocks;

/// <summary>Convenience extension to register all BuildingBlocks services in one call.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers MediatR pipeline behaviors (validation → logging → transaction) and ICurrentUser.
    /// Call from each service's Program.cs:
    ///   builder.Services.AddBuildingBlocks(typeof(MyHandlerAssemblyMarker)).</summary>
    /// <returns></returns>
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        params Type[] assemblyMarkers)
    {
        services.AddMediatR(cfg =>
        {
            foreach (var marker in assemblyMarkers)
            {
                cfg.RegisterServicesFromAssembly(marker.Assembly);
            }

            // Pipeline order: validation → logging → transaction → handler
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        return services;
    }
}
