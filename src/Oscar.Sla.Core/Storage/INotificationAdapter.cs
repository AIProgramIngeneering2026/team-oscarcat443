namespace Oscar.Sla.Core.Storage;

/// <summary>Outcome of a notification delivery attempt.</summary>
public sealed record NotificationResult(bool Success, string? Error);

/// <summary>
/// Sends notifications to an external service. The tool depends only on this
/// interface; a delivery failure must not undo an already saved escalation
/// (docs/tool-contract.md, ADR 0002).
/// </summary>
public interface INotificationAdapter
{
    NotificationResult Send(Guid jobId, string kind, string payload);
}
