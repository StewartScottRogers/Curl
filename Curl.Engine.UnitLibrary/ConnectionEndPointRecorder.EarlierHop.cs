using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal sealed partial class ConnectionEndPointRecorder
{
    /// <summary>The local end of the redirect hop before this one, or <see langword="null" />.</summary>
    private IPEndPoint? earlierLocalEndPoint;

    /// <summary>The remote end of the redirect hop before this one, or <see langword="null" />.</summary>
    private IPEndPoint? earlierRemoteEndPoint;

    /// <summary>The status code the redirect hop before this one received, or <c>0</c>.</summary>
    private int earlierResponseCode;

    /// <summary>The status code the report of the hop being run carried, or <c>0</c>.</summary>
    private int responseCode;

    /// <summary>
    /// Sets the hop just run aside as the earlier hop's when the next transfer follows a redirect, else forgets it.
    /// Runs before the end points are cleared.
    /// </summary>
    /// <param name="followingRedirect">Whether the transfer about to run is a redirect's next hop.</param>
    private void SetAsideEarlierHop(bool followingRedirect)
    {
        earlierLocalEndPoint = followingRedirect ? localEndPoint ?? earlierLocalEndPoint : null;
        earlierRemoteEndPoint = followingRedirect ? remoteEndPoint ?? earlierRemoteEndPoint : null;
        earlierResponseCode = followingRedirect ? Math.Max(responseCode, earlierResponseCode) : 0;
        responseCode = 0;
    }

    /// <summary>Remembers the status code <paramref name="result" />'s report carries, for the next hop.</summary>
    private void RememberResponseCode(TransferResult result) => responseCode = result.Report?.ResponseCode ?? 0;

    /// <summary>
    /// Puts the earlier redirect hop's status code and end points on the report of a hop that failed before it connected.
    /// </summary>
    private TransferResult WithEarlierHop(TransferResult result)
    {
        if (connectionRecorded || result.IsSuccess || earlierRemoteEndPoint is null)
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        return result with
        {
            Report = report with { ResponseCode = earlierResponseCode, LocalEndPoint = earlierLocalEndPoint, RemoteEndPoint = earlierRemoteEndPoint },
        };
    }
}
