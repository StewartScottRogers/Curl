using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>Records every progress report, in order, as text such as <c>down 3/10</c>.</summary>
public sealed class RecordingTransferProgress : ITransferProgress
{
    public List<string> Reports { get; } = [];

    public void ReportTransferStarted() => Reports.Add("started");

    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"down {bytesSoFar}/{expectedTotal}");

    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"up {bytesSoFar}/{expectedTotal}");
}
