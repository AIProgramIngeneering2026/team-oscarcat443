namespace Oscar.Sla.Core.Tools;

/// <summary>
/// Thrown by <see cref="ToolRegistry"/> when no tool is registered under the
/// requested operation name. Carries the <c>unknown_operation</c> error code.
/// </summary>
public sealed class UnknownOperationException : Exception
{
    public const string ErrorCode = "unknown_operation";

    public UnknownOperationException(string operation)
        : base($"Unknown operation: {operation}")
    {
        Operation = operation;
    }

    public string Operation { get; }
}
