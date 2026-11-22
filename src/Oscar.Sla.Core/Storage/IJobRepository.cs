namespace Oscar.Sla.Core.Storage;

/// <summary>
/// Loads and persists jobs. The core depends only on this interface; the
/// current in-memory implementation keeps the domain free of a database driver.
/// </summary>
public interface IJobRepository
{
    Job? Find(Guid id);

    void Save(Job job);
}
