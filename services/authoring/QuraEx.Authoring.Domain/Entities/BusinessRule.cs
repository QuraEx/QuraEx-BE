using QuraEx.BuildingBlocks;

namespace QuraEx.Authoring.Domain.Entities;

/// <summary>Business rule attached to a user story. Maps to business_rule table.</summary>
public sealed class BusinessRule : BaseEntity, ISoftDeletable
{
    private BusinessRule() { }

    public Guid UserStoryId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime? DeletedAt { get; private set; }

    public static BusinessRule Create(Guid userStoryId, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return new BusinessRule { UserStoryId = userStoryId, Content = content.Trim() };
    }

    public void Update(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        Content = content.Trim();
    }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;
}
