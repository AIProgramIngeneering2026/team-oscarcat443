namespace Oscar.Sla.Core.Storage;

/// <summary>A recorded escalation, unique for a job and a reason.</summary>
public sealed record EscalationRecord(Guid JobId, string Reason, string EscalationId);

/// <summary>
/// Persists escalations. At most one record per (job, reason) pair is created,
/// guarding against repeated SLA breach events (docs/contract-check.md K9/K10
/// and ADR 0002).
/// </summary>
public interface IEscalationStore
{
    EscalationRecord? Find(Guid jobId, string reason);

    EscalationRecord Save(Guid jobId, string reason);
}
