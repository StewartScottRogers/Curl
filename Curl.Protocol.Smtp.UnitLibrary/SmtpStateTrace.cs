using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Writes curl 8.21.0's <c>--trace-config smtp</c> lines, <c>[SMTP] ...</c>, as the session
/// steps through what curl's SMTP state machine calls its states (BL-1163): each state change
/// as <c>state change from FROM to TO</c>, the perform, do and doing calls, the message body
/// reads and the done call, through the transfer's <see cref="ITransferEvents.ReportInfo" /> so
/// they fall among the <c>-v</c> lines where curl's do.
/// </summary>
/// <param name="events">The transfer's events.</param>
/// <param name="enabled">Whether the lines are written; <see langword="false" /> writes nothing.</param>
/// <remarks>
/// Measured for a one-recipient send, a send after <c>AUTH CRAM-MD5</c>, a refused <c>RCPT</c>, a
/// refused <c>DATA</c> and a <c>VRFY</c> (BL-1163 Notes). A state curl changes to only once is
/// written once: a second <c>RCPT</c> or a second command writes no state change, as curl writes
/// none when the state does not change.
/// </remarks>
internal sealed class SmtpStateTrace(ITransferEvents events, bool enabled)
{
    /// <summary>The state curl's SMTP state machine rests in between steps.</summary>
    private const string Stop = "STOP";

    /// <summary>The state the trace says the session is in.</summary>
    private string state = Stop;

    /// <summary>Whether the DO phase has started, so a later command writes a doing call.</summary>
    private bool performing;

    /// <summary>Gets a trace that writes nothing.</summary>
    public static SmtpStateTrace Off { get; } = new(NoTransferEvents.Instance, enabled: false);

    /// <summary>Gets a value indicating whether the lines are written.</summary>
    public bool Enabled => enabled;

    /// <summary>Writes the line curl writes as it sets the connection up, before it connects.</summary>
    public void SetupConnection() => Write("smtp_setup_connection() -> 0");

    /// <summary>Writes the change to <paramref name="next" />, unless the session is already in it.</summary>
    /// <param name="next">The state curl enters, such as <c>EHLO</c> or <c>AUTH</c>.</param>
    public void Enter(string next)
    {
        if (next != state)
        {
            Write("state change from " + state + " to " + next);
            state = next;
        }
    }

    /// <summary>Writes the end of the connect phase, back to <c>STOP</c>, and the start of the perform call.</summary>
    public void PerformStarts()
    {
        Enter(Stop);
        Write("smtp_perform(), start");
    }

    /// <summary>
    /// Writes the change to <paramref name="next" /> once its command is sent in the DO phase:
    /// the first one ends the perform, do and first doing calls, each later one a doing call.
    /// </summary>
    /// <param name="next"><c>MAIL</c>, <c>RCPT</c>, <c>DATA</c> or <c>COMMAND</c>.</param>
    public void CommandSent(string next)
    {
        Enter(next);
        if (!performing)
        {
            performing = true;
            Write("smtp_perform() -> 0, connected=1, done=0");
            Write("smtp_regular_transfer() -> 0, done=0");
            Write("smtp_do() -> 0, done=0");
        }

        Write("smtp_doing() -> 0, done=0");
    }

    /// <summary>Writes the end of the DO phase: back to <c>STOP</c>, and the doing call done.</summary>
    public void DoingDone()
    {
        Enter(Stop);
        Write("smtp_doing() -> 0, done=1");
    }

    /// <summary>Writes one read of the message body that returned bytes.</summary>
    /// <param name="length">The bytes read from the upload, before any dot is doubled.</param>
    public void BodyRead(int length) =>
        Write(string.Create(CultureInfo.InvariantCulture, $"cr_eob_read, next_read(len=65536) -> 0, {length} eos=0"));

    /// <summary>Writes the read that found the end of the body, and the end-of-data mark added.</summary>
    public void BodyEnded()
    {
        Write("cr_eob_read, next_read(len=65536) -> 0, 0 eos=1");
        Write("auto-ending mail body with '\\r\\n.\\r\\n'");
        Write("mail body complete, returning EOS");
    }

    /// <summary>
    /// Writes the end of the transfer: a failed doing call when the DO phase failed, then the
    /// done call with <paramref name="exitCode" />. Nothing is written for a failure before the
    /// DO phase, which was not measured.
    /// </summary>
    /// <param name="exitCode">The transfer's exit code.</param>
    public void Ended(CurlExitCode exitCode)
    {
        if (!performing)
        {
            return;
        }

        int code = (int)exitCode;
        bool inDoPhase = state != Stop && state != "POSTDATA";
        if (code != 0 && inDoPhase)
        {
            Write(string.Create(CultureInfo.InvariantCulture, $"smtp_doing() -> {code}, done=0"));
        }

        int status = inDoPhase ? code : 0;
        Write(string.Create(CultureInfo.InvariantCulture, $"smtp_done(status={status}, premature=0) -> {code}"));
    }

    private void Write(string text)
    {
        if (enabled)
        {
            events.ReportInfo("[SMTP] " + text);
        }
    }
}
