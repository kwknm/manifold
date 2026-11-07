using Shared;

namespace BookMetadata.Grpc.Services;

/// <summary>
/// Bounds how many books are parsed at once. Parsing decompresses archives and renders
/// images, so unbounded concurrency exhausts memory and CPU regardless of per-file limits.
/// </summary>
public sealed class ParserConcurrencyLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore =
        new(UploadLimits.MaxConcurrentMetadataParses, UploadLimits.MaxConcurrentMetadataParses);

    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
