using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ssh.Fakes;

/// <summary>
/// Builds the result an <c>sftp://</c> transfer against <see cref="InMemorySshServer" /> ends with:
/// the plain result plus the home directory as <see cref="TransferReport.FtpEntryPath" /> (CGR-267).
/// </summary>
internal static class HomeReportingResult
{
    /// <summary>Gets <paramref name="result" /> with the fake server's home directory reported.</summary>
    /// <param name="result">The result without a report.</param>
    /// <returns>The result as the SFTP handlers return it.</returns>
    internal static TransferResult Of(TransferResult result) =>
        result with { Report = new TransferReport { DownloadSize = result.BytesTransferred, FtpEntryPath = InMemorySshServer.DefaultHomeDirectory } };
}
