using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuraEx.Authoring.Contracts;

namespace QuraEx.Authoring.Infrastructure.Consumers;

/// <summary>Idempotent consumer — dedup handled by IdempotentConsumerFilter upstream.
/// Fail-closed authz: if snapshot is missing, writes fail (403). Log + metric for lag observability.</summary>
public sealed class MembershipChangedConsumer(
    AppDbContext db,
    ILogger<MembershipChangedConsumer> logger)
    : IConsumer<MembershipChangedEvent>
{
    public async Task Consume(ConsumeContext<MembershipChangedEvent> context)
    {
        var msg = context.Message;

        var existing = await db.MembershipSnapshots
            .FirstOrDefaultAsync(
                m => m.ProjectId == msg.ProjectId && m.UserId == msg.UserId,
                context.CancellationToken);

        if (msg.ChangeType == "REMOVED")
        {
            if (existing is not null)
            {
                db.MembershipSnapshots.Remove(existing);
            }
        }
        else
        {
            if (existing is null)
            {
                db.MembershipSnapshots.Add(new MembershipSnapshot
                {
                    ProjectId = msg.ProjectId,
                    UserId = msg.UserId,
                    Role = msg.Role,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Role = msg.Role;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "MembershipSnapshot updated: project={ProjectId} user={UserId} change={ChangeType}",
            msg.ProjectId, msg.UserId, msg.ChangeType);
    }
}
