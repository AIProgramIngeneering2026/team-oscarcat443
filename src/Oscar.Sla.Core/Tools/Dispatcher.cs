namespace Oscar.Sla.Core.Tools;

/// <summary>
/// Takes a tool from the <see cref="ToolRegistry"/> by operation name, executes
/// it and drives the <see cref="Job"/> through its states. It knows no concrete
/// tool: an operation is dispatched solely through <see cref="ITool"/> and
/// <see cref="ToolRegistry"/>, so adding a tool requires no change here.
///
/// State changes follow docs/tool-contract.md: the Job state changes only when a
/// tool returns <see cref="ToolResult.Success"/> and reports the resulting state
/// in <see cref="StateDataKey"/>. A tool's fallback (thrown exception) or a
/// failed <see cref="ToolResult"/> leaves the Job state unchanged and surfaces an
/// explicit error to the caller.
/// </summary>
public sealed class Dispatcher
{
    /// <summary>Key under which a successful tool reports the resulting <see cref="JobState"/>.</summary>
    public const string StateDataKey = "state";

    /// <summary>Error code used when a tool throws and the Job must not change.</summary>
    public const string ToolFailedErrorCode = "tool_failed";

    /// <summary>Error code used when a successful tool reports no or an unknown resulting state.</summary>
    public const string ToolContractErrorCode = "tool_contract";

    private readonly ToolRegistry _registry;

    public Dispatcher(ToolRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Dispatches <paramref name="operation"/> to the registered tool and drives
    /// <paramref name="job"/> to the resulting state on success.
    ///
    /// Throws <see cref="UnknownOperationException"/> when the operation is not
    /// registered. Throws <see cref="InvalidTransitionException"/> when a
    /// successful tool reports a state that <paramref name="job"/> rejects; the
    /// Job's transition rule always wins and is never silently ignored.
    /// </summary>
    public ToolResult Dispatch(Job job, string operation, IReadOnlyDictionary<string, object?> parameters)
    {
        var tool = _registry.Get(operation);

        var request = new ToolRequest
        {
            JobId = job.Id,
            Operation = operation,
            Params = parameters,
        };

        ToolResult result;
        try
        {
            result = tool.Execute(request);
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                ErrorCode = ToolFailedErrorCode,
                ErrorMessage = ex.Message,
            };
        }

        if (!result.Success)
        {
            return result;
        }

        if (TryReadState(result.Data, out var target))
        {
            job.TransitionTo(target);
            return result;
        }

        return new ToolResult
        {
            Success = false,
            ErrorCode = ToolContractErrorCode,
            ErrorMessage = $"Tool '{tool.Name}' did not report its resulting state",
        };
    }

    private static bool TryReadState(IReadOnlyDictionary<string, object?>? data, out JobState state)
    {
        state = default;
        if (data is null || !data.TryGetValue(StateDataKey, out var value))
        {
            return false;
        }

        if (value is JobState enumState)
        {
            state = enumState;
            return true;
        }

        if (value is string name && Enum.TryParse<JobState>(name, ignoreCase: true, out var parsed))
        {
            state = parsed;
            return true;
        }

        return false;
    }
}
