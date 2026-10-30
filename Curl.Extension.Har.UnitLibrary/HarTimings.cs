using System.Text.Json;

namespace Curl.Extension.Har;

/// <summary>
/// An entry's <c>timings</c> object (HAR 1.2, "timings"), in milliseconds, read from the transfer's
/// <see cref="TransferTimings" />. <c>blocked</c>, <c>dns</c>, <c>connect</c> and <c>ssl</c> are -1 when the
/// transfer did not take that step; <c>send</c>, <c>wait</c> and <c>receive</c>, which the specification
/// requires, are 0. <c>connect</c> includes <c>ssl</c>, as the specification says, and <see cref="Total" />,
/// the entry's <c>time</c>, is the sum of every phase but <c>ssl</c> that is not -1.
/// </summary>
/// <param name="Blocked">From the handler's start to the connector's.</param>
/// <param name="Dns">Resolving the host name.</param>
/// <param name="Connect">Opening the connection, the TLS handshake included.</param>
/// <param name="Ssl">The TLS handshake.</param>
/// <param name="Send">Sending the request.</param>
/// <param name="Wait">Waiting for the first response byte.</param>
/// <param name="Receive">Reading the response.</param>
internal sealed record HarTimings(double Blocked, double Dns, double Connect, double Ssl, double Send, double Wait, double Receive)
{
    private const double NotTaken = -1;

    /// <summary>Gets the entry's <c>time</c>: the sum of every phase but <c>ssl</c> that was taken.</summary>
    internal double Total => new[] { Blocked, Dns, Connect, Send, Wait, Receive }.Where(phase => phase >= 0).Sum();

    /// <summary>Reads the phases from <paramref name="timings" />.</summary>
    /// <param name="timings">The transfer's points in time, or <see langword="null" /> when it recorded none.</param>
    /// <param name="frequency">The timestamps' frequency, in ticks per second.</param>
    /// <returns>The phases.</returns>
    internal static HarTimings Of(TransferTimings? timings, long frequency)
    {
        if (timings is null)
        {
            return new(NotTaken, NotTaken, NotTaken, NotTaken, 0, 0, 0);
        }

        var (blocked, dns, connect, ssl) = ConnectPhasesOf(timings, frequency);
        return new(
            blocked,
            dns,
            connect,
            ssl,
            Taken(Between(timings.RequestReady ?? RequestFromOf(timings), timings.RequestSent, frequency)),
            Taken(Between(timings.RequestSent, timings.FirstByteReceived, frequency)),
            Taken(Between(timings.FirstByteReceived, timings.Completed, frequency)));
    }

    /// <summary>Writes the <c>timings</c> object.</summary>
    /// <param name="json">The writer, inside the entry's object.</param>
    internal void Write(Utf8JsonWriter json)
    {
        json.WriteStartObject("timings");
        json.WriteNumber("blocked", Blocked);
        json.WriteNumber("dns", Dns);
        json.WriteNumber("connect", Connect);
        json.WriteNumber("ssl", Ssl);
        json.WriteNumber("send", Send);
        json.WriteNumber("wait", Wait);
        json.WriteNumber("receive", Receive);
        json.WriteEndObject();
    }

    private static double Between(long? from, long? to, long frequency) =>
        from is { } start && to is { } end ? Math.Round((end - start) * 1000.0 / frequency, 3) : NotTaken;

    private static double Taken(double phase) => Math.Max(phase, 0);

    private static (double Blocked, double Dns, double Connect, double Ssl) ConnectPhasesOf(TransferTimings timings, long frequency) =>
        timings.Connect is { } connect
            ? (Between(timings.Started, connect.Started, frequency),
               Between(connect.Started, connect.NameResolved, frequency),
               Between(connect.NameResolved ?? connect.Started, connect.TlsHandshakeCompleted ?? connect.Connected, frequency),
               Between(connect.Connected, connect.TlsHandshakeCompleted, frequency))
            : (NotTaken, NotTaken, NotTaken, NotTaken);

    private static long RequestFromOf(TransferTimings timings) =>
        timings.Connect?.TlsHandshakeCompleted ?? timings.Connect?.Connected ?? timings.Started;
}
