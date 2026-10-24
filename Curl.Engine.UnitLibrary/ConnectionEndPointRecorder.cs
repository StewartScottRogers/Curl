using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Remembers the end points of the first connection a transfer opens, as the recording
/// connectors report them, and puts them on the transfer's report when its handler left them
/// out, the source of <c>%{local_ip}</c>, <c>%{local_port}</c>, <c>%{remote_ip}</c> and
/// <c>%{remote_port}</c> for every scheme (ADR-0119).
/// </summary>
/// <remarks>
/// The first connection is the one curl 8.21.0 reports: for FTP the control connection, not
/// the data connection opened after it (measured, BL-515 Notes). A connection through a Unix
/// domain socket has no address or ports, so it is reported as
/// <see cref="TransferReport.UnixSocketRemoteIp" /> alone (measured, BL-793 Notes). One
/// recorder serves the run's transfers one at a time; <see cref="EndPointReportingProtocolHandler" />
/// clears it before each. Its units live in one file each: the first connection's end points
/// (<c>ConnectionEndPointRecorder.EndPoints.cs</c>), the failed connect's error numbers
/// (<c>.OsErrno.cs</c>) and the earlier redirect hop (<c>.EarlierHop.cs</c>).
/// </remarks>
internal sealed partial class ConnectionEndPointRecorder
{
    /// <summary>
    /// Forgets what the previous transfer recorded, or - for a redirect's next hop - sets it aside as the earlier hop's,
    /// which a hop that fails before it connects reports in its place as libcurl 8.22.0 keeps them (BL-3314).
    /// </summary>
    /// <param name="followingRedirect">Whether the transfer about to run is a redirect's next hop.</param>
    internal void Clear(bool followingRedirect = false)
    {
        SetAsideEarlierHop(followingRedirect);
        ClearEndPoints();
        ClearConnectFailure();
    }

    /// <summary>
    /// Puts the recorded end points on <paramref name="result" />'s report when the handler
    /// reported neither end point itself; a handler that reported them, as the HTTP handler
    /// does, keeps its own.
    /// </summary>
    /// <param name="result">The transfer's result, as its handler returned it.</param>
    /// <returns>
    /// <paramref name="result" /> with the recorded end points on its report; a result without
    /// a report gets one carrying only the end points and <see cref="TransferResult.BytesTransferred" />
    /// as its <see cref="TransferReport.DownloadSize" />, which <c>%{size_download}</c> printed
    /// without a report. <paramref name="result" /> itself when nothing was recorded or the
    /// handler reported an end point.
    /// </returns>
    internal TransferResult ReportOn(TransferResult result)
    {
        RememberResponseCode(result);
        return WithEarlierHop(WithEndPoints(WithConnectFailure(result)));
    }
}
