using System.Globalization;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Telnet;

/// <summary>
/// Holds, in the order they arose, the replies a read calls for and the <c>-v</c> and
/// <c>--trace</c> reports around them, so each reply is sent as its own write at the point
/// curl 8.21.0's <c>lib/telnet.c</c> sends it, and a reply the connection fails to take is
/// reported just there, in the words curl uses for that write (BL-1307, BL-1312).
/// </summary>
/// <param name="events">Where the reports go once their turn comes.</param>
/// <param name="runsOnWindows">Whether a failed read is met as curl on Windows meets it.</param>
internal sealed class TelnetOutbox(ITransferEvents events, bool runsOnWindows)
{
    private readonly Queue<Step> steps = new();

    private bool writeTried;

    private bool writeFailed;

    /// <summary>
    /// Tells whether a connection read that fails now is a receive failure. On Linux and
    /// macOS every failed read is one (BL-5918, ADR-1382). On Windows only a read that
    /// struck the option negotiation is, as the session lost what the server sent: a reply
    /// the connection did not take, or replies sent while no data has been written yet
    /// (BL-5882, ADR-1352).
    /// </summary>
    /// <param name="bytesWritten">The bytes written to the output so far.</param>
    /// <returns><see langword="true" /> when the failed read is a receive failure.</returns>
    public bool FailedReadIsReceiveError(long bytesWritten) =>
        !runsOnWindows || writeFailed || (writeTried && bytesWritten == 0);

    /// <summary>Queues an information line.</summary>
    /// <param name="text">The line.</param>
    public void ReportInfo(string text) => steps.Enqueue(new Step(text, null, null, null));

    /// <summary>Queues a run of output data, reported as data received.</summary>
    /// <param name="data">The bytes of the run; copied.</param>
    public void ReportDataReceived(ReadOnlySpan<byte> data) => steps.Enqueue(new Step(null, data.ToArray(), null, null));

    /// <summary>
    /// Queues one write to the server whose failure is reported as curl's
    /// <c>failf(data, "Sending data failed (%d)", SOCKERRNO)</c> reports it.
    /// </summary>
    /// <param name="reply">The bytes of the write.</param>
    public void Send(byte[] reply) => steps.Enqueue(new Step(null, null, reply, DescribeSendingDataFailure));

    /// <summary>
    /// Queues one write to the server sent as curl's <c>send_telnet_data</c> sends it, whose
    /// failure is reported as curl's socket filter reports a failed send,
    /// <c>failf(data, "Send failure: %s", curlx_strerror(sockerr, ...))</c>; curl ignores the
    /// result itself, so the steps after it go on (the window size inside
    /// <c>sendsuboption</c>, BL-1312).
    /// </summary>
    /// <param name="data">The bytes of the write.</param>
    public void SendTelnetData(byte[] data) => steps.Enqueue(new Step(null, null, data, DescribeSendFailure));

    /// <summary>
    /// Sends and reports every queued step in order. A write that fails with a socket error
    /// is reported in its step's words, and the steps after it go on.
    /// </summary>
    /// <param name="trySend">
    /// Sends one write, returning <see langword="null" /> when the connection took it or the
    /// socket error when it did not; any other failure is thrown, and leaves the steps after
    /// it queued for <see cref="ReportPending" />.
    /// </param>
    /// <returns>A task that completes when every step has gone.</returns>
    public async Task SendPendingAsync(Func<byte[], Task<SocketException?>> trySend)
    {
        while (steps.TryDequeue(out Step step))
        {
            if (step.Reply is not { } reply)
            {
                Report(step);
                continue;
            }

            writeTried = true;
            if (await trySend(reply).ConfigureAwait(false) is { } failure)
            {
                writeFailed = true;
                events.ReportInfo(step.DescribeFailure!(failure));
            }
        }
    }

    /// <summary>
    /// Reports every queued line and run of data in order, dropping any write still queued:
    /// a session that ends before its replies go out never sends them.
    /// </summary>
    public void ReportPending()
    {
        while (steps.TryDequeue(out Step step))
        {
            Report(step);
        }
    }

    private void Report(Step step)
    {
        if (step.Info is { } text)
        {
            events.ReportInfo(text);
        }
        else if (step.Received is { } data)
        {
            events.ReportDataReceived(data);
        }
    }

    /// <summary>
    /// curl's <c>failf(data, "Sending data failed (%d)", SOCKERRNO)</c>: the socket error
    /// number, the WSA code on Windows and the errno elsewhere.
    /// </summary>
    private static string DescribeSendingDataFailure(SocketException failure) =>
        string.Create(CultureInfo.InvariantCulture, $"Sending data failed ({failure.NativeErrorCode})");

    /// <summary>curl's socket filter's <c>Send failure: &lt;text&gt;</c>.</summary>
    private static string DescribeSendFailure(SocketException failure) =>
        "Send failure: " + CurlSocketErrorText.Words(failure, OperatingSystem.IsWindows());

    /// <summary>
    /// One queued step: exactly one of a line, a run of data or a write, a write with the
    /// words its failure is reported in.
    /// </summary>
    private readonly record struct Step(string? Info, byte[]? Received, byte[]? Reply, Func<SocketException, string>? DescribeFailure);
}
