namespace Oscar.Sla.Core.Tests;

public class JobTests
{
    private static Job CreateJob(JobState initialState = JobState.Open) =>
        new(Guid.NewGuid(), initialState);

    // Обычный путь — от создания до успешного завершения.
    [Fact]
    public void Normal_Path_Walks_From_Open_To_Closed()
    {
        var job = CreateJob(JobState.Open);

        job.TransitionTo(JobState.InProgress);
        job.TransitionTo(JobState.Resolved);
        job.TransitionTo(JobState.Closed);

        Assert.Equal(JobState.Closed, job.State);
        Assert.Equal(
            new[]
            {
                new JobTransition(JobState.Open, JobState.InProgress),
                new JobTransition(JobState.InProgress, JobState.Resolved),
                new JobTransition(JobState.Resolved, JobState.Closed),
            },
            job.History);
    }

    [Fact]
    public void Normal_Path_Through_Escalation_Reaches_Closed()
    {
        var job = CreateJob(JobState.Open);

        job.TransitionTo(JobState.Escalated);
        job.TransitionTo(JobState.InProgress);
        job.TransitionTo(JobState.Resolved);
        job.TransitionTo(JobState.Closed);

        Assert.Equal(JobState.Closed, job.State);
    }

    // З1 Двойная работа: нельзя повторно запустить работу, пока она идёт.
    [Fact]
    public void З1_Second_Start_While_Working_Is_Rejected()
    {
        var job = CreateJob(JobState.Open);
        job.TransitionTo(JobState.InProgress);

        var ex = Assert.Throws<InvalidTransitionException>(
            () => job.TransitionTo(JobState.InProgress));

        Assert.Equal(JobState.InProgress, ex.From);
        Assert.Equal(JobState.InProgress, ex.To);
        Assert.Equal(JobState.InProgress, job.State);
    }

    // З3 Отменённое стало готовым: отменённый Job не может стать готовым/закрытым.
    [Fact]
    public void З3_Cancelled_Job_Cannot_Become_Done()
    {
        var job = CreateJob(JobState.Open);
        job.TransitionTo(JobState.Cancelled);

        Assert.Throws<InvalidTransitionException>(() => job.TransitionTo(JobState.Resolved));
        Assert.Throws<InvalidTransitionException>(() => job.TransitionTo(JobState.Closed));

        Assert.Equal(JobState.Cancelled, job.State);
        Assert.Single(job.History);
    }

    // З4 Два итога: из состояния ошибки (ESCALATED) нельзя сразу стать готовым (CLOSED),
    // минуя повторную работу и фиксацию решения.
    [Fact]
    public void З4_Escalated_Cannot_Jump_To_Closed()
    {
        var job = CreateJob(JobState.Open);
        job.TransitionTo(JobState.Escalated);

        Assert.Throws<InvalidTransitionException>(() => job.TransitionTo(JobState.Closed));

        Assert.Equal(JobState.Escalated, job.State);
    }

    // З5 Успех без работы: Job не может стать готовым/закрытым, не побывав в работе.
    [Theory]
    [InlineData(JobState.Resolved)]
    [InlineData(JobState.Closed)]
    public void З5_Open_Cannot_Become_Done_Without_Working(JobState target)
    {
        var job = CreateJob(JobState.Open);

        Assert.Throws<InvalidTransitionException>(() => job.TransitionTo(target));

        Assert.Equal(JobState.Open, job.State);
    }

    // З6 Повтор успешного: закрытый Job не возвращается в работу.
    [Fact]
    public void З6_Closed_Job_Cannot_Go_Back_To_Work()
    {
        var job = CreateJob(JobState.Open);
        job.TransitionTo(JobState.InProgress);
        job.TransitionTo(JobState.Resolved);
        job.TransitionTo(JobState.Closed);

        Assert.Throws<InvalidTransitionException>(() => job.TransitionTo(JobState.InProgress));

        Assert.Equal(JobState.Closed, job.State);
    }
}
