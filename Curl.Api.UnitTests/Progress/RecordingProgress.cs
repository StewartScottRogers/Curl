namespace Curl.Api.Progress;

/// <summary>An <see cref="IProgress{T}"/> that keeps every report on the calling thread, or throws <see cref="Failure"/> instead when it is set.</summary>
internal sealed class RecordingProgress : IProgress<CurlTransferProgress>
{
    public List<CurlTransferProgress> Reports { get; } = [];

    public Exception? Failure { get; init; }

    public void Report(CurlTransferProgress value)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Reports.Add(value);
    }
}
