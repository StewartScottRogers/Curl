namespace Curl.Protocol.Abstractions;

/// <summary>
/// An <see cref="ITransferProgress" /> that is not <see cref="NoTransferProgress.Instance" />,
/// for tests that pin a sink is passed through as given. Its own members are never called;
/// it keeps <see cref="ITransferProgress.ReportTransferDone" />'s default body, which one test calls.
/// </summary>
internal sealed class StubTransferProgress : ITransferProgress
{
    public void ReportTransferStarted() => throw new NotSupportedException();

    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => throw new NotSupportedException();

    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => throw new NotSupportedException();
}
