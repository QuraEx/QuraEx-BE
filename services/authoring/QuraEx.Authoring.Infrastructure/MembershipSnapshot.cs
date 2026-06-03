namespace QuraEx.Authoring.Infrastructure;

/// <summary>Read-model from Workspace MembershipChanged events. Not an aggregate — no ISoftDeletable.
/// Composite PK: (project_id, user_id).</summary>
public sealed class MembershipSnapshot
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
