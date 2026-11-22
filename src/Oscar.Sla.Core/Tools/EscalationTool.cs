namespace Oscar.Sla.Core.Tools;

using Oscar.Sla.Core.Storage;

/// <summary>
/// Operation <c>escalate</c>. Records an escalation for an SLA breach reason and
/// reports the resulting state <see cref="JobState.Escalated"/>. Follows
/// docs/tool-contract.md:
///
/// - refuses with <c>invalid_params</c>/field <c>reason</c> when the reason is not in
///   the known list;
/// - refuses with <c>invalid_transition</c> when the current state does not allow
///   escalation;
/// - at most one record per (job, reason): a repeated reason returns the previous
///   escalation with a repeat flag and neither creates a second record nor sends a
///   second notification;
/// - a notification delivery failure does not roll back the saved escalation.
/// </summary>
public sealed class EscalationTool : ITool
{
    public const string Name = "escalate";

    private static readonly IReadOnlySet<string> KnownReasons = new HashSet<string>
    {
        "reaction_sla_breached",
        "resolution_sla_breached",
    };

    private readonly IJobRepository _jobs;
    private readonly IEscalationStore _escalations;
    private readonly INotificationAdapter _notifications;

    public EscalationTool(IJobRepository jobs, IEscalationStore escalations, INotificationAdapter notifications)
    {
        _jobs = jobs;
        _escalations = escalations;
        _notifications = notifications;
    }

    string ITool.Name => Name;

    public ToolResult Execute(ToolRequest request)
    {
        var reason = request.Params.TryGetValue("reason", out var raw) ? raw as string : null;
        if (reason is null || !KnownReasons.Contains(reason))
        {
            return Error("invalid_params", "Unknown reason", "reason");
        }

        var job = _jobs.Find(request.JobId);
        if (job is null)
        {
            return Error("job_not_found", "Job not found", null);
        }

        var existing = _escalations.Find(job.Id, reason);
        if (existing is not null)
        {
            return new ToolResult
            {
                Success = true,
                Data = new Dictionary<string, object?>
                {
                    [Dispatcher.StateDataKey] = job.State,
                    ["escalation_id"] = existing.EscalationId,
                    ["repeat"] = true,
                },
            };
        }

        if (job.State is not (JobState.Open or JobState.InProgress))
        {
            return Error("invalid_transition", $"Escalation is not allowed from {job.State}", null);
        }

        var record = _escalations.Save(job.Id, reason);

        string? deliveryError = null;
        var delivery = _notifications.Send(job.Id, "escalation", record.EscalationId);
        if (!delivery.Success)
        {
            deliveryError = delivery.Error ?? "notification delivery failed";
        }

        return new ToolResult
        {
            Success = true,
            Data = new Dictionary<string, object?>
            {
                [Dispatcher.StateDataKey] = JobState.Escalated,
                ["escalation_id"] = record.EscalationId,
                ["repeat"] = false,
                ["delivery_error"] = deliveryError,
            },
        };
    }

    private static ToolResult Error(string code, string message, string? field) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message,
        Field = field,
    };
}
