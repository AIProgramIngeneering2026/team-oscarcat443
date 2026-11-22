namespace Oscar.Sla.Core.Storage;

/// <summary>
/// In-memory <see cref="IAssignmentStore"/> for the domain core.
/// </summary>
public sealed class InMemoryAssignmentStore : IAssignmentStore
{
    private readonly Dictionary<Guid, string> _assignments = new();

    public void Save(Guid jobId, string assigneeId) => _assignments[jobId] = assigneeId;

    public string? Get(Guid jobId) => _assignments.TryGetValue(jobId, out var assigneeId) ? assigneeId : null;
}
