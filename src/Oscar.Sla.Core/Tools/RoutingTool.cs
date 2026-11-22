namespace Oscar.Sla.Core.Tools;

using Oscar.Sla.Core.Storage;

/// <summary>
/// Operation <c>assign</c>. Assigns an active assignee to a job and reports the
/// resulting state <see cref="JobState.InProgress"/>. Follows docs/tool-contract.md:
///
/// - refuses with <c>invalid_params</c>/field <c>assignee_id</c> when the assignee
///   is missing, does not exist or is inactive;
/// - refuses with <c>assignment_conflict</c> when an assignment is already saved;
/// - refuses with <c>invalid_transition</c> when the current state does not allow
///   assignment.
///
/// Hidden from callers are the competence and load rules of the routing decision.
/// </summary>
public sealed class RoutingTool : ITool
{
    public const string Name = "assign";

    private readonly IJobRepository _jobs;
    private readonly IAssigneeDirectory _assignees;
    private readonly IAssignmentStore _assignments;

    public RoutingTool(IJobRepository jobs, IAssigneeDirectory assignees, IAssignmentStore assignments)
    {
        _jobs = jobs;
        _assignees = assignees;
        _assignments = assignments;
    }

    string ITool.Name => Name;

    public ToolResult Execute(ToolRequest request)
    {
        var assigneeId = request.Params.TryGetValue("assignee_id", out var raw) ? raw as string : null;
        if (string.IsNullOrWhiteSpace(assigneeId))
        {
            return Error("invalid_params", "Assignee id is required", "assignee_id");
        }

        var job = _jobs.Find(request.JobId);
        if (job is null)
        {
            return Error("job_not_found", "Job not found", null);
        }

        if (job.State is not (JobState.Open or JobState.Escalated))
        {
            return Error("invalid_transition", $"Assignment is not allowed from {job.State}", null);
        }

        if (_assignments.Get(job.Id) is not null)
        {
            return Error("assignment_conflict", "Assignment is already saved for this job", null);
        }

        var assignee = _assignees.Find(assigneeId);
        if (assignee is null || !assignee.IsActive)
        {
            return Error("invalid_params", "Assignee is inactive or does not exist", "assignee_id");
        }

        _assignments.Save(job.Id, assigneeId);

        return new ToolResult
        {
            Success = true,
            Data = new Dictionary<string, object?>
            {
                [Dispatcher.StateDataKey] = JobState.InProgress,
                ["assignee_id"] = assigneeId,
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
