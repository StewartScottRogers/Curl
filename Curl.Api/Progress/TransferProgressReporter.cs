using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Curl.Api.Info;

namespace Curl.Api;

/// <summary>
/// The transfer-info callback that turns each <c>CURLOPT_XFERINFOFUNCTION</c> call into a
/// <see cref="CurlTransferProgress"/> for the caller's <see cref="IProgress{T}"/> (ADR-0643), then
/// answers what the next callback (the transfer's cancellation) answers. A report that throws aborts
/// the transfer; once <see cref="End"/> is called no more reports are made.
/// </summary>
/// <param name="easy">The handle whose speeds each report reads.</param>
/// <param name="progress">Where the reports go.</param>
/// <param name="next">The transfer-info callback asked after each report.</param>
internal sealed class TransferProgressReporter(IEasyTransfer easy, IProgress<CurlTransferProgress> progress, TransferInfoCallback next)
{
    /// <summary>Set by <see cref="End"/>; read on the engine thread, written from the perform task.</summary>
    private volatile bool _ended;
    /// <summary>Set once a report threw; every later call then answers abort.</summary>
    private volatile bool _failed;

    /// <summary>The <c>CURLOPT_XFERINFOFUNCTION</c>: reports, then answers <see cref="TransferCancellation.Abort"/> if a report threw, else what the next callback answers.</summary>
    /// <param name="downloadTotal">The response body bytes expected, 0 when not known.</param>
    /// <param name="downloadNow">The response body bytes received.</param>
    /// <param name="uploadTotal">The request body bytes to send, 0 when not known.</param>
    /// <param name="uploadNow">The request body bytes sent.</param>
    /// <returns>0 to carry on; any other value aborts the transfer.</returns>
    public int Report(long downloadTotal, long downloadNow, long uploadTotal, long uploadNow)
    {
        if (!_ended && !TryReport(downloadTotal, downloadNow, uploadTotal, uploadNow))
        {
            _failed = true;
        }

        return _failed ? TransferCancellation.Abort : next(downloadTotal, downloadNow, uploadTotal, uploadNow);
    }

    /// <summary>Marks the transfer as ended: later calls make no report.</summary>
    public void End() => _ended = true;

    /// <summary>The phase the byte counts show.</summary>
    /// <param name="downloadTotal">The response body bytes expected.</param>
    /// <param name="downloadNow">The response body bytes received.</param>
    /// <param name="uploadNow">The request body bytes sent.</param>
    /// <returns>Downloading, uploading or connecting, in that order of precedence.</returns>
    internal static CurlTransferPhase PhaseOf(long downloadTotal, long downloadNow, long uploadNow) =>
        downloadTotal > 0 || downloadNow > 0 ? CurlTransferPhase.Downloading
        : uploadNow > 0 ? CurlTransferPhase.Uploading
        : CurlTransferPhase.Connecting;

    /// <summary>Gives the progress sink one report; a sink that throws must not unwind through the engine, so the failure is returned instead.</summary>
    /// <returns><see langword="true"/> when the report was delivered; <see langword="false"/> when the sink threw.</returns>
    private bool TryReport(long downloadTotal, long downloadNow, long uploadTotal, long uploadNow)
    {
        try
        {
            TransferInfo info = easy.Info;
            progress.Report(new CurlTransferProgress
            {
                DownloadTotal = downloadTotal,
                DownloadNow = downloadNow,
                UploadTotal = uploadTotal,
                UploadNow = uploadNow,
                DownloadSpeed = info.SpeedDownload,
                UploadSpeed = info.SpeedUpload,
                Phase = PhaseOf(downloadTotal, downloadNow, uploadNow),
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
