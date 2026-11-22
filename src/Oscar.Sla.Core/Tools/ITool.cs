namespace Oscar.Sla.Core.Tools;

/// <summary>
/// A single operation executable by the Dispatcher. The contract is fixed in
/// docs/tool-contract.md: the Dispatcher depends only on <see cref="ITool"/>
/// and <see cref="ToolRegistry"/>, never on a concrete tool. Adding a new tool
/// requires no change to the Dispatcher.
/// </summary>
public interface ITool
{
    string Name { get; }

    ToolResult Execute(ToolRequest request);
}

/// <summary>Input for <see cref="ITool.Execute"/>.</summary>
public sealed class ToolRequest
{
    public required Guid JobId { get; init; }

    public required string Operation { get; init; }

    public required IReadOnlyDictionary<string, object?> Params { get; init; }
}

/// <summary>
/// Uniform outcome of a tool. The Job state changes only when
/// <see cref="Success"/> is <c>true</c>.
/// </summary>
public sealed class ToolResult
{
    public required bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? Field { get; init; }

    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
