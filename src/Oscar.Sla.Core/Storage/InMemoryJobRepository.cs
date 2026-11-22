namespace Oscar.Sla.Core.Storage;

/// <summary>
/// In-memory <see cref="IJobRepository"/> for the domain core. Sufficient until
/// practice 4 replaces it with a database-backed implementation.
/// </summary>
public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly Dictionary<Guid, Job> _jobs = new();

    public Job? Find(Guid id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public void Save(Job job) => _jobs[job.Id] = job;
}
