namespace Oscar.Sla.Core;

/// <summary>
/// An incident as a unit of work. Owns the allowed-transitions rule from
/// docs/job-states.md: an invalid transition is rejected with an explicit
/// <see cref="InvalidTransitionException"/> and never silently ignored.
/// </summary>
public sealed class Job
{
    private static readonly IReadOnlyDictionary<JobState, IReadOnlySet<JobState>> AllowedTransitions =
        new Dictionary<JobState, IReadOnlySet<JobState>>
        {
            [JobState.Open] = new HashSet<JobState> { JobState.InProgress, JobState.Escalated, JobState.Cancelled },
            [JobState.InProgress] = new HashSet<JobState> { JobState.Escalated, JobState.Resolved, JobState.Cancelled },
            [JobState.Escalated] = new HashSet<JobState> { JobState.InProgress, JobState.Resolved, JobState.Cancelled },
            [JobState.Resolved] = new HashSet<JobState> { JobState.Closed, JobState.InProgress },
            [JobState.Closed] = new HashSet<JobState>(),
            [JobState.Cancelled] = new HashSet<JobState>(),
        };

    private readonly List<JobTransition> _history = new();

    public Job(Guid id, JobState initialState = JobState.Open)
    {
        Id = id;
        State = initialState;
    }

    public Guid Id { get; }

    public JobState State { get; private set; }

    /// <summary>
    /// Immutable history of applied transitions. Unsuccessful attempts are not recorded.
    /// </summary>
    public IReadOnlyList<JobTransition> History => _history.AsReadOnly();

    /// <summary>
    /// Applies <paramref name="target"/> if the transition is allowed by the state diagram.
    /// Throws <see cref="InvalidTransitionException"/> otherwise and leaves the state unchanged.
    /// </summary>
    public void TransitionTo(JobState target)
    {
        if (State == target)
        {
            throw new InvalidTransitionException(State, target);
        }

        if (!AllowedTransitions[State].Contains(target))
        {
            throw new InvalidTransitionException(State, target);
        }

        var from = State;
        State = target;
        _history.Add(new JobTransition(from, target));
    }
}

/// <summary>A successfully applied state change, recorded in <see cref="Job.History"/>.</summary>
public sealed record JobTransition(JobState From, JobState To);
