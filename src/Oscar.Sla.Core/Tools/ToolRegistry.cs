namespace Oscar.Sla.Core.Tools;

/// <summary>
/// Stores tools by operation name and hands them out as <see cref="ITool"/>.
/// Hides from the Dispatcher how a tool is created or registered. An unknown
/// operation throws <see cref="UnknownOperationException"/> instead of being
/// silently ignored.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name);
    }

    /// <summary>
    /// Returns the tool registered under <paramref name="operation"/>.
    /// Throws <see cref="UnknownOperationException"/> if none is registered.
    /// </summary>
    public ITool Get(string operation)
    {
        if (!_tools.TryGetValue(operation, out var tool))
        {
            throw new UnknownOperationException(operation);
        }

        return tool;
    }
}
