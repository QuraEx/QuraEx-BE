using QuraEx.Authoring.Domain.DomainEvents;
using QuraEx.BuildingBlocks;

namespace QuraEx.Authoring.Domain.Entities;

public enum AuthoringStatus
{
    DRAFT,
    READY,
    APPROVED,
}

/// <summary>Aggregate root. Maps to user_story table (DBML §3).</summary>
public sealed class UserStory : SoftDeletableAggregate
{
    private readonly List<AcceptanceCriteria> _acceptanceCriteria = [];
    private readonly List<BusinessRule> _businessRules = [];

    private UserStory() { }

    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? AsA { get; private set; }
    public string? IWantTo { get; private set; }
    public string? SoThat { get; private set; }
    public string? Description { get; private set; }
    public AuthoringStatus Status { get; private set; } = AuthoringStatus.DRAFT;
    public string? ExternalRef { get; private set; }

    public IReadOnlyList<AcceptanceCriteria> AcceptanceCriteria => _acceptanceCriteria.AsReadOnly();
    public IReadOnlyList<BusinessRule> BusinessRules => _businessRules.AsReadOnly();

    public static UserStory Create(
        Guid projectId,
        string title,
        Guid createdBy,
        string? asA = null,
        string? iWantTo = null,
        string? soThat = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var story = new UserStory
        {
            ProjectId = projectId,
            Title = title.Trim(),
            AsA = asA,
            IWantTo = iWantTo,
            SoThat = soThat,
            Description = description,
            Status = AuthoringStatus.DRAFT,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        };

        story.AddDomainEvent(new UserStoryCreatedEvent(story.Id, projectId));
        return story;
    }

    public void Update(
        string title,
        Guid updatedBy,
        string? asA = null,
        string? iWantTo = null,
        string? soThat = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        AsA = asA;
        IWantTo = iWantTo;
        SoThat = soThat;
        Description = description;
        UpdatedBy = updatedBy;
        SetUpdated();
    }

    public void Transition(AuthoringStatus newStatus, Guid updatedBy)
    {
        Status = newStatus;
        UpdatedBy = updatedBy;
        SetUpdated();
    }

    public void SetExternalRef(string externalRef)
    {
        ExternalRef = externalRef;
        SetUpdated();
    }
}
