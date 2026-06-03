using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace QuraEx.BuildingBlocks.Behaviors;

/// <summary>Emits structured log entries and a timing metric for every MediatR request.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName}", requestName);
        try
        {
            var response = await next();
            sw.Stop();
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName,
                sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMs}ms",
                requestName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
