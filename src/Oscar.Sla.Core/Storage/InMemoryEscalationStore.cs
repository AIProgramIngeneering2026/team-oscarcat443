namespace Oscar.Sla.Core.Storage;

/// <summary>
/// In-memory <see cref="IEscalationStore"/>. A second record for the same
/// (job, reason) is never created; <see cref="Find"/> returns the previous one.
/// </summary>
public sealed class InMemoryEscalationStore : IEscalationStore
{
    private readonly Dictionary<(Guid JobId, string Reason), EscalationRecord> _records = new();

    private int _nextId = 1;

    public EscalationRecord? Find(Guid jobId, string reason) =>
        _records.TryGetValue((jobId, reason), out var record) ? record : null;

    public EscalationRecord Save(Guid jobId, string reason)
    {
        if (_records.TryGetValue((jobId, reason), out var existing))
        {
            return existing;
        }

        var record = new EscalationRecord(jobId, reason, $"esc-{_nextId++}");
        _records[(jobId, reason)] = record;
        return record;
    }

    public int Count => _records.Count;
}
