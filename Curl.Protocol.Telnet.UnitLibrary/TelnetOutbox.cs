using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Holds, in the order they arose, the replies a read calls for and the <c>-v</c> and
/// <c>--trace</c> reports around them, so each reply is sent as its own write at the point
/// curl 8.21.0's <c>lib/telnet.c</c> sends it, and a reply the connection fails to take is
/// reported as <c>Sending data failed (N)</c> just there (BL-1307).
/// </summary>
/// <param name="events">Where the reports go once their turn comes.</param>
internal sealed class TelnetOutbox(ITransferEvents events)
{
    private readonly Queue<Step> steps = new();

    /// <summary>Queues an information line.</summary>
    /// <param name="text">The line.</param>
    public void ReportInfo(string text) => steps.Enqueue(new Step(text, null, null, false));

    /// <summary>Queues a run of output data, reported as data received.</summary>
    /// <param name="data">The bytes of the run; copied.</param>
    public void ReportDataReceived(ReadOnlySpan<byte> data) => steps.Enqueue(new Step(null, data.ToArray(), null, false));

    /// <summary>
    /// Queues one write to the server whose failure is reported as curl's
    /// <c>failf(data, "Sending data failed (%d)", SOCKERRNO)</c> reports it.
    /// </summary>
    /// <param name="reply">The bytes of the write.</param>
    public void Send(byte[] reply) => steps.Enqueue(new Step(null, null, reply, true));

    /// <summary>
    /// Queues one write to the server whose failure is not reported, as curl's
    /// <c>send_telnet_data</c> call inside <c>sendsuboption</c> goes unchecked.
    /// </summary>
    /// <param name="reply">The bytes of the write.</param>
    public void SendUnreported(byte[] reply) => steps.Enqueue(new Step(null, null, reply, false));

    /// <summary>
    /// Sends and reports every queued step in order. A write that fails with a socket error
    /// is reported as <c>Sending data failed (N)</c>, when its step reports failures, and
    /// the steps after it go on.
    /// </summary>
    /// <param name="trySend">
    /// Sends one write, returning <see langword="null" /> when the connection took it or the
    /// socket error number when it did not; any other failure is thrown, and leaves the
    /// steps after it queued for <see cref="ReportPending" />.
    /// </param>
    /// <returns>A task that completes when every step has gone.</returns>
    public async Task SendPendingAsync(Func<byte[], Task<int?>> trySend)
    {
        while (steps.TryDequeue(out Step step))
        {
            if (step.Reply is not { } reply)
            {
                Report(step);
            }
            else if (await trySend(reply).ConfigureAwait(false) is { } error && step.ReportsFailure)
            {
                events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"Sending data failed ({error})"));
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

    /// <summary>One queued step: exactly one of a line, a run of data or a write.</summary>
    private readonly record struct Step(string? Info, byte[]? Received, byte[]? Reply, bool ReportsFailure);
}
