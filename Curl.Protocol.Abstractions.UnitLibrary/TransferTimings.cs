namespace Curl.Protocol.Abstractions;

/// <summary>
/// The points in time a handler records during one transfer, each a value returned by
/// <see cref="TimeProvider.GetTimestamp" /> on <see cref="ITransferContext.TimeProvider" />.
/// A member whose event did not happen is <see langword="null" />; see ADR-0015.
/// </summary>
/// <param name="Started">
/// Taken when the handler's <c>ExecuteAsync</c> begins: the origin every <c>%{time_*}</c>
/// is measured from.
/// </param>
/// <param name="Connect">
/// The connector's timings, copied from <see cref="ConnectResult.Timings" />;
/// <see langword="null" /> when no connection was opened.
/// </param>
/// <param name="RequestReady">
/// Taken when the connection is ready and the first request byte is about to be sent,
/// the source of <c>%{time_pretransfer}</c>.
/// </param>
/// <param name="RequestSent">
/// Taken when the last request byte, body included, has been sent, the source of
/// <c>%{time_posttransfer}</c>.
/// </param>
/// <param name="FirstByteReceived">
/// Taken when the first response byte is read, the source of <c>%{time_starttransfer}</c>.
/// </param>
/// <param name="Completed">
/// Taken when the handler is about to return, the source of <c>%{time_total}</c>.
/// </param>
public sealed record TransferTimings(
    long Started,
    ConnectTimings? Connect,
    long? RequestReady,
    long? RequestSent,
    long? FirstByteReceived,
    long Completed)
{
    /// <summary>
    /// Gets the time spent on every redirect hop before the last, the source of
    /// <c>%{time_redirect}</c>. Set by the redirect follower; <see cref="TimeSpan.Zero" />
    /// from a handler.
    /// </summary>
    public TimeSpan RedirectDuration { get; init; }
}
