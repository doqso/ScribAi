namespace ScribAi.Api.Jobs;

public record ExtractionJob(Guid ExtractionId, Guid TenantId);

public interface IJobQueue
{
    Task EnqueueAsync(ExtractionJob job, CancellationToken ct);
}
