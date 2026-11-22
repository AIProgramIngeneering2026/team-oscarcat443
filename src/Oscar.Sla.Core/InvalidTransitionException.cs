namespace Oscar.Sla.Core;

/// <summary>
/// Error thrown by <see cref="Job"/> when a transition is not allowed by the
/// state diagram. The transition is rejected with an explicit error and the
/// Job state is left unchanged.
/// </summary>
public sealed class InvalidTransitionException : Exception
{
    public const string ErrorCode = "invalid_transition";

    public InvalidTransitionException(JobState from, JobState to)
        : base("Transition is not allowed")
    {
        From = from;
        To = to;
    }

    public JobState From { get; }

    public JobState To { get; }
}
