namespace Curl.Protocol.Http;

/// <summary>
/// A response header curl 8.21.0 refuses while it reads the head, and the failure it reports
/// (<see cref="HttpResponseBodyReader.FindHeadRefusal" />).
/// </summary>
/// <param name="HeaderIndex">The refused header's index in the head's headers.</param>
/// <param name="Failure">The failure curl reports for it.</param>
internal sealed record HttpHeadRefusal(int HeaderIndex, HttpTransferException Failure);
