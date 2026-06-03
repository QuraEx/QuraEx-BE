using QuraEx.BuildingBlocks;

namespace QuraEx.Authoring.Domain.Entities;

/// <summary>Hierarchical acceptance criteria (nullable parent_id for nested ACs). Maps to acceptance_criteria table.</summary>
public sealed class AcceptanceCriteria : BaseEntity, ISoftDeletable
{
    private AcceptanceCriteria() { }

    public Guid UserStoryId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int OrderNo { get; private set; }
    public bool Completed { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static AcceptanceCriteria Create(
        Guid userStoryId,
        string content,
        int orderNo = 0,
        Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return new AcceptanceCriteria
        {
            UserStoryId = userStoryId,
            ParentId = parentId,
            Content = content.Trim(),
            OrderNo = orderNo,
            Completed = false,
        };
    }

    public void Complete() => Completed = true;
    public void Reopen() => Completed = false;

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;
}
