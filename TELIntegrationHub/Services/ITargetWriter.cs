namespace TEL.IntegrationHub.Services;

/// <summary>Phase 1 stub: target DB writers are not implemented yet.</summary>
public interface ITargetWriter
{
    string TargetSystem { get; }

    Task WriteAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
}

public sealed class NoOpTargetWriter : ITargetWriter
{
    public NoOpTargetWriter(string targetSystem) => TargetSystem = targetSystem;

    public string TargetSystem { get; }

    public Task WriteAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
