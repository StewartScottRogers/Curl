using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>
    /// The info with the operating system error number (<c>CURLINFO_OS_ERRNO</c>) the report holds.
    /// </summary>
    /// <param name="info">The info so far.</param>
    /// <param name="report">The finished transfer's report.</param>
    /// <returns>The info with the OS error number set.</returns>
    private static TransferInfo WithOsErrno(TransferInfo info, TransferReport report) => info with
    {
        OsErrno = report.OsErrno,
    };
}
