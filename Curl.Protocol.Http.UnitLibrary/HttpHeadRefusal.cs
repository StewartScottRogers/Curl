namespace Curl.Protocol.Http;

/// <summary>
/// A response header curl 8.21.0 refuses while it reads the head, and the failure it reports
/// (<see cref="HttpResponseBodyReader.FindHeadRefusal" />).
/// </summary>
/// <param name="HeaderIndex">The refused header's index in the head's headers.</param>
/// <param name="Failure">The failure curl reports for it.</param>
internal sealed record HttpHeadRefusal(int HeaderIndex, HttpTransferException Failure)
{
    /// <summary>
    /// Gets a value indicating whether the refused header's own lines are still reported as
    /// <c>-v</c> <c>&lt;</c> lines, though not written to the header output, as curl 8.21.0
    /// writes the header past <see cref="HttpResponseHeadReader.MaximumHeaderCount" /> (measured,
    /// BL-1431 Notes). Otherwise neither it nor any line after it is reported.
    /// </summary>
    internal bool ReportsRefusedHeaderLines { get; init; }
}
