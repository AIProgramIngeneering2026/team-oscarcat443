namespace Oscar.Sla.Core.Storage;

/// <summary>
/// In-memory <see cref="IAssigneeDirectory"/> seeded with a known set of
/// assignees for the domain core.
/// </summary>
public sealed class InMemoryAssigneeDirectory : IAssigneeDirectory
{
    private readonly Dictionary<string, Assignee> _assignees;

    public InMemoryAssigneeDirectory(IEnumerable<Assignee> assignees)
    {
        _assignees = assignees.ToDictionary(a => a.Id);
    }

    public Assignee? Find(string id) => _assignees.TryGetValue(id, out var assignee) ? assignee : null;
}
