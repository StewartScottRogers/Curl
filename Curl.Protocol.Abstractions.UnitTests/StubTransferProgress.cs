namespace Curl.Protocol.Abstractions;

/// <summary>
/// An <see cref="ITransferProgress" /> that is not <see cref="NoTransferProgress.Instance" />,
/// for tests that pin a sink is passed through as given. Its own members throw;
/// it keeps the default bodies of <see cref="ITransferProgress.ReportTransferDone" /> and
/// <see cref="ITransferProgress.ReportRequestRetried" />, which one test each calls; the second reaches
/// <see cref="ReportTransferStarted" />, which throws.
/// </summary>
internal sealed class StubTransferProgress : ITransferProgress
{
    public void ReportTransferStarted() => throw new NotSupportedException();

    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => throw new NotSupportedException();

    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => throw new NotSupportedException();
}
