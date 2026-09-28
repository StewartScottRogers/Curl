using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Remembers the end points of the first connection a transfer opens, as the recording
/// connectors report them, and puts them on the transfer's report when its handler left them
/// out, the source of <c>%{local_ip}</c>, <c>%{local_port}</c>, <c>%{remote_ip}</c> and
/// <c>%{remote_port}</c> for every scheme (ADR-0119).
/// </summary>
/// <remarks>
/// The first connection is the one curl 8.21.0 reports: for FTP the control connection, not
/// the data connection opened after it (measured, BL-515 Notes). One recorder serves the
/// run's transfers one at a time; <see cref="EndPointReportingProtocolHandler" /> clears it
/// before each.
/// </remarks>
internal sealed class ConnectionEndPointRecorder
{
    /// <summary>The local end of the first connection, or <see langword="null" /> when unknown.</summary>
    private IPEndPoint? localEndPoint;

    /// <summary>The remote end of the first connection, or <see langword="null" /> before one opened.</summary>
    private IPEndPoint? remoteEndPoint;

    /// <summary>Forgets what the previous transfer recorded.</summary>
    internal void Clear()
    {
        localEndPoint = null;
        remoteEndPoint = null;
    }

    /// <summary>
    /// Records a connection the transfer opened, unless it already opened one.
    /// </summary>
    /// <param name="local">
    /// The local address and port, or <see langword="null" /> when the connection has none to
    /// report, as a UDP channel curl never connects has none.
    /// </param>
    /// <param name="remote">
    /// The peer's address and port; <see langword="null" /> records nothing, as a connection
    /// with no known peer has nothing to report.
    /// </param>
    internal void Record(IPEndPoint? local, IPEndPoint? remote)
    {
        if (remote is null || remoteEndPoint is not null)
        {
            return;
        }

        localEndPoint = local;
        remoteEndPoint = remote;
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
        if (remoteEndPoint is null)
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        if (report.LocalEndPoint is not null || report.RemoteEndPoint is not null)
        {
            return result;
        }

        return result with { Report = report with { LocalEndPoint = localEndPoint, RemoteEndPoint = remoteEndPoint } };
    }
}
