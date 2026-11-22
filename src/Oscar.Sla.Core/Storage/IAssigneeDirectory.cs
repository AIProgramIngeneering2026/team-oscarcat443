namespace Oscar.Sla.Core.Storage;

/// <summary>An assignee that a routing tool can be asked to assign.</summary>
public sealed record Assignee(string Id, bool IsActive);

/// <summary>
/// Lookup of assignees for domain checks (exists, active). The tool sees no
/// competence and load rules, in line with docs/tool-contract.md.
/// </summary>
public interface IAssigneeDirectory
{
    Assignee? Find(string id);
}
