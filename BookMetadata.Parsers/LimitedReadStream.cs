namespace BookMetadata.Parsers;

/// <summary>
/// Read-only stream wrapper that fails once a byte budget is exhausted. Zip entry headers
/// can lie about the expanded size, so archive content must be capped while it is read
/// rather than only by inspecting declared lengths.
/// </summary>
public sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
{
    private long _read;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var allowed = GetAllowedCount(count);
        if (allowed == 0)
        {
            return 0;
        }

        var read = inner.Read(buffer, offset, (int)allowed);
        _read += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var allowed = GetAllowedCount(buffer.Length);
        if (allowed == 0)
        {
            return 0;
        }

        var read = await inner.ReadAsync(buffer[..(int)allowed], cancellationToken);
        _read += read;
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private long GetAllowedCount(int requested)
    {
        var remaining = maxBytes - _read;
        if (remaining <= 0)
        {
            throw new InvalidDataException($"Stream exceeded the limit of {maxBytes} bytes.");
        }

        return Math.Min(requested, remaining);
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
