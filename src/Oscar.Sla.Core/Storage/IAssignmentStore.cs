namespace Oscar.Sla.Core.Storage;

/// <summary>
/// Persists the result of an assignment. Keeps the assignee of a job separate
/// from the state machine so that concurrent assigns can detect a conflict.
/// </summary>
public interface IAssignmentStore
{
    void Save(Guid jobId, string assigneeId);

    string? Get(Guid jobId);
}
