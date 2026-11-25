using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Pop3.Fakes;

/// <summary>Takes the timings the handler stamps (BL-5473) off a result so a test can compare the rest of it.</summary>
internal static class StampedTimings
{
    /// <summary>
    /// Returns <paramref name="result" /> without its timings, and without a report when the timings
    /// were all the report held besides the download size.
    /// </summary>
    internal static TransferResult Without(TransferResult result)
    {
        if (result.Report is not { Timings: not null } report)
        {
            return result;
        }

        TransferReport rest = report with { Timings = null };
        return result with { Report = rest == new TransferReport { DownloadSize = result.BytesTransferred } ? null : rest };
    }
}
