namespace Oscar.Sla.Core.Tests;

using Oscar.Sla.Core.Tools;

public class DispatcherTests
{
    private static ToolRegistry RegistryWith(params ITool[] tools) => new(tools);

    private static IReadOnlyDictionary<string, object?> Params(params (string Key, object? Value)[] items) =>
        items.ToDictionary(i => i.Key, i => i.Value);

    private static ToolRequest Request(Guid jobId, string operation, IReadOnlyDictionary<string, object?>? parameters = null) =>
        new()
        {
            JobId = jobId,
            Operation = operation,
            Params = parameters ?? new Dictionary<string, object?>(),
        };

    // Stub tools for exercising the Dispatcher. Their concrete siblings
    // (RoutingTool, EscalationTool) belong to other team members' items.
    private sealed class StubTool : ITool
    {
        public StubTool(string name, Func<ToolRequest, ToolResult> handler)
        {
            Name = name;
            _handler = handler;
        }

        public string Name { get; }

        private readonly Func<ToolRequest, ToolResult> _handler;

        public ToolResult Execute(ToolRequest request) => _handler(request);
    }

    private static ToolResult SuccessWithState(JobState state, string assigneeId) => new()
    {
        Success = true,
        Data = new Dictionary<string, object?>
        {
            [Dispatcher.StateDataKey] = state,
            ["assignee_id"] = assigneeId,
        },
    };

    // Обычный путь Job через Dispatcher — от OPEN до успешного завершения.
    [Fact]
    public void Dispatcher_Drives_Job_From_Open_To_Closed()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(
            new StubTool("assign", r => SuccessWithState(JobState.InProgress, "u-1")),
            new StubTool("resolve", r => SuccessWithState(JobState.Resolved, "u-1")),
            new StubTool("resolve_to_closed", r => SuccessWithState(JobState.Closed, "u-1"))));

        var r1 = dispatcher.Dispatch(job, "assign", Params(("assignee_id", "u-1")));
        Assert.True(r1.Success);
        Assert.Equal(JobState.InProgress, job.State);

        var r2 = dispatcher.Dispatch(job, "resolve", Params(("resolution", "fixed")));
        Assert.True(r2.Success);
        Assert.Equal(JobState.Resolved, job.State);

        var r3 = dispatcher.Dispatch(job, "resolve_to_closed", Params());
        Assert.True(r3.Success);
        Assert.Equal(JobState.Closed, job.State);
    }

    // Dispatcher не знает конкретных Tool: новая операция добавляется в Registry
    // без правки Dispatcher.
    [Fact]
    public void New_Tool_Is_Dispatched_Without_Changing_Dispatcher()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(
            new StubTool("assign", r => SuccessWithState(JobState.InProgress, "u-1")),
            new StubTool("escalate", r => SuccessWithState(JobState.Escalated, "esc-1"))));

        dispatcher.Dispatch(job, "assign", Params(("assignee_id", "u-1")));
        var result = dispatcher.Dispatch(job, "escalate", Params(("reason", "resolution_sla_breached")));

        Assert.True(result.Success);
        Assert.Equal(JobState.Escalated, job.State);
    }

    // Неизвестная операция — явная ошибка unknown_operation, Job не меняется.
    [Fact]
    public void Unknown_Operation_Throws_And_Does_Not_Change_Job()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(new StubTool("assign", r => SuccessWithState(JobState.InProgress, "u-1"))));

        var ex = Assert.Throws<UnknownOperationException>(
            () => dispatcher.Dispatch(job, "no_such_operation", Params()));

        Assert.Equal("no_such_operation", ex.Operation);
        Assert.Equal(UnknownOperationException.ErrorCode, "unknown_operation");
        Assert.Equal(JobState.Open, job.State);
    }

    // Падение Tool не меняет допустимое состояние Job и возвращает явную ошибку.
    [Fact]
    public void Tool_Failure_Leaves_Job_Unchanged_And_Returns_Error()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(
            new StubTool("assign", r => new ToolResult
            {
                Success = false,
                ErrorCode = "invalid_params",
                ErrorMessage = "Assignee is inactive",
                Field = "assignee_id",
            })));

        var result = dispatcher.Dispatch(job, "assign", Params(("assignee_id", "u-1")));

        Assert.False(result.Success);
        Assert.Equal("invalid_params", result.ErrorCode);
        Assert.Equal("assignee_id", result.Field);
        Assert.Equal(JobState.Open, job.State);
    }

    // Падение Tool (исключение) переводит операцию в ошибку, состояние Job не меняется.
    [Fact]
    public void Tool_Exception_Leaves_Job_Unchanged_And_Returns_ToolFailed()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(
            new StubTool("assign", r => throw new InvalidOperationException("boom"))));

        var result = dispatcher.Dispatch(job, "assign", Params(("assignee_id", "u-1")));

        Assert.False(result.Success);
        Assert.Equal(Dispatcher.ToolFailedErrorCode, result.ErrorCode);
        Assert.Equal(JobState.Open, job.State);
    }

    // Успех Tool без указания следующего состояния — ошибка контракта, Job не меняется.
    [Fact]
    public void Success_Without_Reported_State_Is_Contract_Error()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(
            new StubTool("assign", r => new ToolResult { Success = true, Data = new Dictionary<string, object?> { ["assignee_id"] = "u-1" } })));

        var result = dispatcher.Dispatch(job, "assign", Params(("assignee_id", "u-1")));

        Assert.False(result.Success);
        Assert.Equal(Dispatcher.ToolContractErrorCode, result.ErrorCode);
        Assert.Equal(JobState.Open, job.State);
    }

    // Tool, вернувший переход, который Job отклоняет, — Job отклоняет его явной ошибкой.
    [Fact]
    public void Tool_Reporting_Invalid_Transition_Is_Rejected_By_Job()
    {
        var job = new Job(Guid.NewGuid(), JobState.Open);
        var dispatcher = new Dispatcher(RegistryWith(
            new StubTool("assign", r => SuccessWithState(JobState.InProgress, "u-1")),
            new StubTool("jump_to_closed", r => SuccessWithState(JobState.Closed, "u-1"))));

        dispatcher.Dispatch(job, "assign", Params(("assignee_id", "u-1")));

        var ex = Assert.Throws<InvalidTransitionException>(
            () => dispatcher.Dispatch(job, "jump_to_closed", Params()));

        Assert.Equal(JobState.InProgress, ex.From);
        Assert.Equal(JobState.Closed, ex.To);
        Assert.Equal(JobState.InProgress, job.State);
    }

    // Registry: выдача Tool по имени операции.
    [Fact]
    public void Registry_Returns_Tool_By_Name()
    {
        var tool = new StubTool("assign", r => SuccessWithState(JobState.InProgress, "u-1"));
        var registry = RegistryWith(tool);

        Assert.Same(tool, registry.Get("assign"));
    }

    // Registry: неизвестная операция — явная ошибка unknown_operation.
    [Fact]
    public void Registry_Unknown_Operation_Throws_UnknownOperation()
    {
        var registry = RegistryWith(new StubTool("assign", r => SuccessWithState(JobState.InProgress, "u-1")));

        var ex = Assert.Throws<UnknownOperationException>(() => registry.Get("phantom"));

        Assert.Equal("phantom", ex.Operation);
    }
}
