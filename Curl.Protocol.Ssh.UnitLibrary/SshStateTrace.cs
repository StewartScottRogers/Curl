using System.Globalization;
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
/// Measured for an SFTP and an SCP download authenticated with <c>publickey</c> (BL-1166 Notes),
/// and for uploads, listings, <c>-Q</c> commands, password and <c>keyboard-interactive</c>
/// logins, a failed agent, fingerprints and failed transfers (BL-1204 Notes, ADR-0374), and
/// for an agent login, a listed symbolic link and <c>-I</c> on a directory (BL-1207, ADR-0377).
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

    /// <summary>
    /// Writes curl's CONNECT phase ending once SFTP's <c>REALPATH</c> has answered, and its DO
    /// phase starting at once (BL-1166).
    /// </summary>
    public void EndSftpConnectPhase()
    {
        Enter(Stop);
        Write("CONNECT phase done");
        Rest();
        Write("DO phase starts");
    }

    /// <summary>
    /// Writes curl's SFTP DONE phase ending in <c>SSH_SFTP_CLOSE</c> and the state machine
    /// resting (BL-1166) - unless a <c>-Q</c> command after the transfer failed and left the
    /// session in that command's state, from which <see cref="Fail" /> leaves (BL-1204).
    /// </summary>
    public void EndSftpDonePhase()
    {
        if (state == "SSH_SFTP_CLOSE")
        {
            Write("SFTP DONE done");
            Rest();
        }
    }

    /// <summary>
    /// Writes the failure's way out of the state the session failed in, after curl's failure
    /// line: the change to the state that frees what the failed state held, then the state
    /// machine's return there with <paramref name="exitCode" />. An SFTP state goes to
    /// <c>SSH_SFTP_CLOSE</c>, an SCP state to <c>SSH_SCP_CHANNEL_FREE</c>, a failed
    /// <c>keyboard-interactive</c> login returns from <c>SSH_AUTH_KEY</c> itself, and any other
    /// state goes to <c>SSH_SESSION_FREE</c>, as measured (BL-1204). A session resting in
    /// <c>SSH_STOP</c> failed outside the state machine, and writes nothing.
    /// </summary>
    /// <param name="exitCode">The transfer's exit code.</param>
    public void Fail(CurlExitCode exitCode)
    {
        if (state == Stop)
        {
            return;
        }

        string freeing = state.StartsWith("SSH_SFTP_", StringComparison.Ordinal) ? "SSH_SFTP_CLOSE"
            : state.StartsWith("SSH_SCP_", StringComparison.Ordinal) ? "SSH_SCP_CHANNEL_FREE"
            : state == "SSH_AUTH_KEY" ? state
            : "SSH_SESSION_FREE";
        Enter(freeing);
        Write("[" + freeing + "] statemachine() -> " + ((int)exitCode).ToString(CultureInfo.InvariantCulture) + ", block=0");
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
