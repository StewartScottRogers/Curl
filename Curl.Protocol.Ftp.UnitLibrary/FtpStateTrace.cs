using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Writes curl 8.21.0's <c>--trace-config ftp</c> lines, <c>[FTP] ...</c>, as the session
/// steps through what curl's FTP state machine calls its states (BL-1162): each state change
/// as <c>[FROM] -&gt; [TO]</c> and each note as <c>[STATE] note</c>, through the transfer's
/// <see cref="ITransferEvents.ReportInfo" /> so they fall among the <c>-v</c> lines where curl's do.
/// </summary>
/// <param name="events">The transfer's events.</param>
/// <param name="enabled">Whether the lines are written; <see langword="false" /> writes nothing.</param>
/// <remarks>
/// Measured for a passive download, a passive upload and a listing (BL-1162 Notes). A command
/// whose state was not measured (<c>CWD</c>, <c>MDTM</c>, <c>AUTH</c>, a quote, active mode's
/// <c>EPRT</c> and <c>PORT</c>, <c>REST</c>) writes no state change.
/// </remarks>
internal sealed class FtpStateTrace(ITransferEvents events, bool enabled)
{
    /// <summary>The state curl's FTP state machine rests in between commands.</summary>
    private const string Stop = "STOP";

    /// <summary>The state the trace says the session is in.</summary>
    private string state = Stop;

    /// <summary>
    /// The transfer the DO phase prepares, which names its <c>TYPE</c>, <c>SIZE</c> and transfer
    /// states: <c>RETR</c>, <c>STOR</c> or <c>LIST</c>.
    /// </summary>
    private string transfer = "RETR";

    /// <summary>Writes the line curl writes as it sets the connection up, before it connects.</summary>
    public void SetupConnection() => Note("setup connection -> 0");

    /// <summary>Writes the change to waiting for the greeting, once the control connection is open.</summary>
    public void AwaitingGreeting() => Enter("WAIT220");

    /// <summary>
    /// Writes the state change <paramref name="command" /> makes once it is sent, for the
    /// commands whose state was measured.
    /// </summary>
    /// <param name="command">The command line sent, without its line end.</param>
    public void Sent(string command)
    {
        if (StateOf(command) is { } next)
        {
            Enter(next);
        }
    }

    /// <summary>
    /// Writes the note curl writes while it waits for the reply to the command just sent: that
    /// a passive data connection is awaited, or the DO_MORE phase's poll.
    /// </summary>
    public void AwaitingReply()
    {
        if (state == "PASV")
        {
            Note("perform, awaiting DATA connect");
        }
        else if (state.StartsWith(transfer, StringComparison.Ordinal))
        {
            Note("ftp_domore_pollset()");
        }
    }

    /// <summary>Writes the end of the connect phase: back to <c>STOP</c>, and the phase done.</summary>
    public void ConnectPhaseDone()
    {
        Enter(Stop);
        Note("protocol connect phase DONE");
    }

    /// <summary>Writes the start of the DO phase, which prepares <paramref name="transferVerb" />.</summary>
    /// <param name="transferVerb"><c>RETR</c>, <c>STOR</c> or <c>LIST</c>.</param>
    public void DoPhaseStarts(string transferVerb)
    {
        transfer = transferVerb;
        Note("DO phase starts");
    }

    /// <summary>Writes the end of the DO phase, as the passive data connection is dialled.</summary>
    public void DoPhaseComplete()
    {
        Enter(Stop);
        Note("DO phase is complete2");
    }

    /// <summary>Writes the DO_MORE phase's poll while the data connection is still connecting.</summary>
    public void DataConnectionPending() => Note("ftp_domore_pollset()");

    /// <summary>Writes the note curl writes before it sends <c>RETR</c> for a file.</summary>
    public void RetrieveNext() => Note("ftp_state_retr()");

    /// <summary>Writes the start of the data transfer: the transfer initiated, and back to <c>STOP</c>.</summary>
    public void TransferInitiated()
    {
        Write("ftp_initiate_transfer()");
        Enter(Stop);
    }

    /// <summary>Writes the close of the data connection and the start of reading the end-of-transfer reply.</summary>
    public void ClosingDataConnection()
    {
        Note("closing DATA connection");
        Write("getftpresponse start");
    }

    /// <summary>Writes the end-of-transfer reply read.</summary>
    /// <param name="reply">The reply.</param>
    public void TransferReplyRead(FtpReply reply) =>
        Write(string.Create(CultureInfo.InvariantCulture, $"getftpresponse -> result=0, nread={reply.ByteCount}, ftpcode={reply.Code}"));

    /// <summary>Writes the transfer done without an error.</summary>
    public void Done() => Note("done, result=0");

    private string? StateOf(string command)
    {
        string verb = command.Split(' ', 2)[0];
        return verb switch
        {
            "USER" or "PASS" or "PWD" => verb,
            "EPSV" or "PASV" => "PASV",
            "TYPE" or "SIZE" => transfer + "_" + verb,
            "RETR" or "STOR" or "APPE" or "LIST" or "NLST" => transfer,
            _ => null,
        };
    }

    private void Enter(string next)
    {
        Write("[" + state + "] -> [" + next + "]");
        state = next;
    }

    private void Note(string text) => Write("[" + state + "] " + text);

    private void Write(string text)
    {
        if (enabled)
        {
            events.ReportInfo("[FTP] " + text);
        }
    }
}
