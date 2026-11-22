namespace Oscar.Sla.Core.Tests;

using Oscar.Sla.Core.Storage;
using Oscar.Sla.Core.Tools;

public class ToolTests
{
    private sealed class Fixture
    {
        public Fixture(params (string Id, bool Active)[] assignees)
        {
            Jobs = new InMemoryJobRepository();
            Assignees = new InMemoryAssigneeDirectory(assignees.Select(a => new Assignee(a.Id, a.Active)));
            Assignments = new InMemoryAssignmentStore();
            Escalations = new InMemoryEscalationStore();
            Notifications = new RecordingNotificationAdapter();
        }

        public InMemoryJobRepository Jobs { get; }

        public InMemoryAssigneeDirectory Assignees { get; }

        public InMemoryAssignmentStore Assignments { get; }

        public InMemoryEscalationStore Escalations { get; }

        public RecordingNotificationAdapter Notifications { get; }

        public RoutingTool RoutingTool => new(Jobs, Assignees, Assignments);

        public EscalationTool EscalationTool => new(Jobs, Escalations, Notifications);

        public ToolRegistry Registry => new(new ITool[] { RoutingTool, EscalationTool });

        public Dispatcher Dispatcher => new(Registry);
    }

    private sealed class RecordingNotificationAdapter : INotificationAdapter
    {
        public bool FailNext { get; set; }

        public int Calls { get; private set; }

        public NotificationResult Send(Guid jobId, string kind, string payload)
        {
            Calls++;
            return FailNext
                ? new NotificationResult(false, "timeout")
                : new NotificationResult(true, null);
        }
    }

    private static ToolRequest Req(Guid jobId, string operation, params (string K, object? V)[] p) => new()
    {
        JobId = jobId,
        Operation = operation,
        Params = p.ToDictionary(x => x.K, x => x.V),
    };

    private static Job OpenJob(Guid id) => new(id, JobState.Open);

    // ---- RoutingTool: обычный путь через Dispatcher, assign открытого Job ----

    [Fact]
    public void Assign_Moves_Open_Job_To_InProgress_Through_Dispatcher()
    {
        var fx = new Fixture(("u-1", true));
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        var result = fx.Dispatcher.Dispatch(job, "assign", new Dictionary<string, object?> { ["assignee_id"] = "u-1" });

        Assert.True(result.Success);
        Assert.Equal(JobState.InProgress, job.State);
        Assert.Equal("u-1", (string?)result.Data!["assignee_id"]);
        Assert.Equal("u-1", fx.Assignments.Get(job.Id));
    }

    [Fact]
    public void Assign_From_Escalated_Moves_To_InProgress()
    {
        var fx = new Fixture(("u-1", true));
        var job = new Job(Guid.NewGuid(), JobState.Escalated);
        fx.Jobs.Save(job);

        var result = fx.RoutingTool.Execute(Req(job.Id, "assign", ("assignee_id", "u-1")));

        Assert.True(result.Success);
        Assert.Equal(JobState.InProgress, result.Data![Dispatcher.StateDataKey]);
    }

    // Неверные параметры: неизвестный исполнитель — invalid_params с полем assignee_id.
    [Fact]
    public void Assign_Unknown_Assignee_Is_InvalidParams_With_Field()
    {
        var fx = new Fixture();
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        var result = fx.RoutingTool.Execute(Req(job.Id, "assign", ("assignee_id", "ghost")));

        Assert.False(result.Success);
        Assert.Equal("invalid_params", result.ErrorCode);
        Assert.Equal("assignee_id", result.Field);
        Assert.Equal(JobState.Open, job.State);
    }

    [Fact]
    public void Assign_Inactive_Assignee_Is_InvalidParams_With_Field()
    {
        var fx = new Fixture(("u-1", false));
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        var result = fx.RoutingTool.Execute(Req(job.Id, "assign", ("assignee_id", "u-1")));

        Assert.False(result.Success);
        Assert.Equal("invalid_params", result.ErrorCode);
        Assert.Equal("assignee_id", result.Field);
    }

    // Недопустимый переход: назначение из IN_PROGRESS запрещено.
    [Fact]
    public void Assign_From_InProgress_Is_InvalidTransition()
    {
        var fx = new Fixture(("u-1", true));
        var job = new Job(Guid.NewGuid(), JobState.InProgress);
        fx.Jobs.Save(job);

        var result = fx.RoutingTool.Execute(Req(job.Id, "assign", ("assignee_id", "u-1")));

        Assert.False(result.Success);
        Assert.Equal("invalid_transition", result.ErrorCode);
    }

    // assignment_conflict: повторное назначение одному Job запрещено.
    [Fact]
    public void Assign_Second_Time_Is_AssignmentConflict()
    {
        var fx = new Fixture(("u-1", true));
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        fx.RoutingTool.Execute(Req(job.Id, "assign", ("assignee_id", "u-1")));
        var second = fx.RoutingTool.Execute(Req(job.Id, "assign", ("assignee_id", "u-1")));

        Assert.False(second.Success);
        Assert.Equal("assignment_conflict", second.ErrorCode);
    }

    // ---- EscalationTool ----

    [Fact]
    public void Escalate_Moves_Job_To_Escalated_Through_Dispatcher()
    {
        var fx = new Fixture();
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        var result = fx.Dispatcher.Dispatch(job, "escalate", new Dictionary<string, object?> { ["reason"] = "reaction_sla_breached" });

        Assert.True(result.Success);
        Assert.Equal(JobState.Escalated, job.State);
        Assert.False(result.Data!["repeat"] is true);
        Assert.Equal(1, fx.Notifications.Calls);
        Assert.Equal(1, fx.Escalations.Count);
    }

    // Неверные параметры: неизвестная причина — invalid_params с полем reason.
    [Fact]
    public void Escalate_Unknown_Reason_Is_InvalidParams_With_Field()
    {
        var fx = new Fixture();
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        var result = fx.EscalationTool.Execute(Req(job.Id, "escalate", ("reason", "nonsense")));

        Assert.False(result.Success);
        Assert.Equal("invalid_params", result.ErrorCode);
        Assert.Equal("reason", result.Field);
    }

    // Недопустимый переход: эскалация из CLOSED запрещена.
    [Fact]
    public void Escalate_From_Closed_Is_InvalidTransition()
    {
        var fx = new Fixture();
        var job = new Job(Guid.NewGuid(), JobState.Closed);
        fx.Jobs.Save(job);

        var result = fx.EscalationTool.Execute(Req(job.Id, "escalate", ("reason", "reaction_sla_breached")));

        Assert.False(result.Success);
        Assert.Equal("invalid_transition", result.ErrorCode);
    }

    // Повтор той же причины: прежняя эскалация, repeat=true, вторая запись не создаётся.
    [Fact]
    public void Escalate_Same_Reason_Returns_Previous_Record_And_No_Second()
    {
        var fx = new Fixture();
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);

        var first = fx.EscalationTool.Execute(Req(job.Id, "escalate", ("reason", "resolution_sla_breached")));
        var second = fx.EscalationTool.Execute(Req(job.Id, "escalate", ("reason", "resolution_sla_breached")));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(second.Data!["repeat"] is true);
        Assert.Equal(first.Data!["escalation_id"], second.Data!["escalation_id"]);
        Assert.Equal(1, fx.Escalations.Count);
        Assert.Equal(1, fx.Notifications.Calls);
    }

    // Ошибка доставки уведомления не откатывает сохранённую эскалацию.
    [Fact]
    public void Escalate_Delivery_Failure_Does_Not_Roll_Back_Escalation()
    {
        var fx = new Fixture();
        var job = OpenJob(Guid.NewGuid());
        fx.Jobs.Save(job);
        fx.Notifications.FailNext = true;

        var result = fx.EscalationTool.Execute(Req(job.Id, "escalate", ("reason", "reaction_sla_breached")));

        Assert.True(result.Success);
        Assert.Equal(1, fx.Escalations.Count);
        Assert.NotNull(result.Data!["delivery_error"]);
    }

    // ---- Registry ----

    // Registry выдаёт Tool по имени операции.
    [Fact]
    public void Registry_Returns_Tools_By_Name()
    {
        var fx = new Fixture(("u-1", true));

        Assert.IsAssignableFrom<RoutingTool>(fx.Registry.Get("assign"));
        Assert.IsAssignableFrom<EscalationTool>(fx.Registry.Get("escalate"));
    }

    // Неизвестная операция — ошибка unknown_operation.
    [Fact]
    public void Registry_Unknown_Operation_Throws()
    {
        var fx = new Fixture(("u-1", true));

        var ex = Assert.Throws<UnknownOperationException>(() => fx.Registry.Get("phantom"));
        Assert.Equal("phantom", ex.Operation);
    }
}
