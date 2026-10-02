using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Writes curl 8.21.0's <c>--trace-config ssh</c> lines, <c>[SSH] ...</c>, as the session steps
/// through the states of curl's libssh2 state machine (BL-1166): each state change as
/// <c>[FROM] -&gt; [TO]</c>, the phase lines, and the state machine's return once it rests in
/// <c>SSH_STOP</c>, through the transfer's <see cref="ITransferEvents.ReportInfo" /> so they fall
/// among the <c>-v</c> lines where curl's do.
/// </summary>
/// <param name="events">The transfer's events.</param>
/// <param name="enabled">Whether the lines are written; <see langword="false" /> writes nothing.</param>
/// <remarks>
/// Measured for an SFTP and an SCP download authenticated with <c>publickey</c> (BL-1166 Notes).
/// curl also writes <c>[STATE] statemachine() -&gt; 0, block=1</c> and <c>pollset, flags=1</c>
/// each time libssh2 would block on the socket; how many depends on when the server's packets
/// arrive, so they are not written, as for a server that always answers before curl asks
/// (ADR-0372).
/// </remarks>
internal sealed class SshStateTrace(ITransferEvents events, bool enabled)
{
    /// <summary>The state curl's state machine rests in between phases.</summary>
    internal const string Stop = "SSH_STOP";

    /// <summary>The state the trace says the session is in.</summary>
    private string state = Stop;

    /// <summary>Gets a trace that writes nothing.</summary>
    public static SshStateTrace Off { get; } = new(NoTransferEvents.Instance, enabled: false);

    /// <summary>Writes the change to <paramref name="next" />, unless the session is already in it.</summary>
    /// <param name="next">The state curl enters, such as <c>SSH_INIT</c>.</param>
    public void Enter(string next)
    {
        if (next != state)
        {
            Write("[" + state + "] -> [" + next + "]");
            state = next;
        }
    }

    /// <summary>Writes the change back to <c>SSH_STOP</c> and the state machine's return there.</summary>
    public void Rest()
    {
        Enter(Stop);
        Write("[" + Stop + "] statemachine() -> 0, block=0");
    }

    /// <summary>Writes one line that is not a state change, such as <c>DO phase starts</c>.</summary>
    /// <param name="text">The line, without its <c>[SSH] </c> prefix.</param>
    public void Write(string text)
    {
        if (enabled)
        {
            events.ReportInfo("[SSH] " + text);
        }
    }
}
