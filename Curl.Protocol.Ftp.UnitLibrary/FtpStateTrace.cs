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
/// Measured for a passive download, a passive upload and a listing (BL-1162 Notes), and for an
/// active-mode download and upload with <c>EPRT</c> and <c>PORT</c> (BL-1196 Notes), and for
/// <c>CWD</c>, <c>MDTM</c>, <c>REST</c>, <c>AUTH</c>, <c>PBSZ</c>, <c>PROT</c>, <c>CCC</c>,
/// <c>SYST</c>, <c>ACCT</c>, quotes, a refused <c>EPSV</c> and the end of a failed transfer
/// (BL-1197 Notes), and for <c>PRET</c>, <c>SITE NAMEFMT 1</c> and <c>MKD</c> (BL-1199 Notes).
/// </remarks>
internal sealed class FtpStateTrace(ITransferEvents events, bool enabled)
{
    /// <summary>The state curl's FTP state machine rests in between commands.</summary>
    private const string Stop = "STOP";

    /// <summary>
    /// The failures curl 8.21.0's <c>ftp_done</c> says leave the control connection usable,
    /// which its <c>done, result=</c> line writes as 0; any other failure is written as its
    /// exit code (measured for 9 and 78 as 0, and 8, 21, 67 and 81 as themselves).
    /// </summary>
    private static readonly CurlExitCode[] ConnectionKeptFailures =
    [
        CurlExitCode.BadDownloadResume,
        CurlExitCode.FtpWeirdPasvReply,
        CurlExitCode.FtpPortFailed,
        CurlExitCode.FtpAcceptFailed,
        CurlExitCode.FtpAcceptTimeout,
        CurlExitCode.FtpCouldntSetType,
        CurlExitCode.FtpCouldntRetrFile,
        CurlExitCode.PartialFile,
        CurlExitCode.UploadFailed,
        CurlExitCode.RemoteAccessDenied,
        CurlExitCode.FilesizeExceeded,
        CurlExitCode.RemoteFileNotFound,
        CurlExitCode.WriteError,
    ];

    /// <summary>The state the trace says the session is in.</summary>
    private string state = Stop;

    /// <summary>
    /// Whether the DO phase has started and no command has been sent in it yet: curl writes
    /// <c>perform, awaiting DATA connect</c> once, after the first.
    /// </summary>
    private bool performNoteDue;

    /// <summary>Whether the DO phase has started and is not yet complete.</summary>
    private bool doPhaseRunning;

    /// <summary>Whether the DO phase completed and the data connection has not been closed since.</summary>
    private bool dataConnectionOpen;

    /// <summary>Whether the <c>done</c> line has been written.</summary>
    private bool ended;

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
    /// Writes the change back to <c>CWD</c> after the reply to <c>MKD</c>, which curl writes
    /// before it sends <c>CWD</c> again rather than after (BL-1199).
    /// </summary>
    public void ChangingDirectoryAgain() => Enter("CWD");

    /// <summary>
    /// Writes the state change a <c>-Q</c> command makes once it is sent, and before its reply
    /// is read: <c>QUOTE</c> after login, <c>&lt;transfer&gt;_PREQUOTE</c> before the transfer,
    /// and after it no state change but the start of reading its reply.
    /// </summary>
    /// <param name="stage">When the command is sent.</param>
    public void QuoteSent(FtpQuoteStage stage)
    {
        switch (stage)
        {
            case FtpQuoteStage.AfterLogin:
                Enter("QUOTE");
                break;
            case FtpQuoteStage.BeforeTransfer:
                Enter(transfer + "_PREQUOTE");
                break;
            default:
                Write("getftpresponse start");
                return;
        }

        AwaitingReply();
    }

    /// <summary>Writes the reply to a <c>-Q</c> command read, for a command sent after the transfer.</summary>
    /// <param name="stage">When the command was sent.</param>
    /// <param name="reply">Its reply.</param>
    public void QuoteReplyRead(FtpQuoteStage stage, FtpReply reply)
    {
        if (stage == FtpQuoteStage.AfterTransfer)
        {
            TransferReplyRead(reply);
        }
    }

    /// <summary>
    /// Writes the note curl writes while it waits for the reply to the command just sent: once
    /// per DO phase, after its first command, that the data connection is awaited, and in a
    /// transfer state the DO_MORE phase's poll.
    /// </summary>
    public void AwaitingReply()
    {
        if (performNoteDue)
        {
            performNoteDue = false;
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
        performNoteDue = true;
        doPhaseRunning = true;
        Note("DO phase starts");
    }

    /// <summary>
    /// Writes the lines curl writes as it opens, binds and listens on the active-mode port,
    /// before <c>EPRT</c> or <c>PORT</c> announces it (BL-1196).
    /// </summary>
    public void ActivePortListening()
    {
        Note("ftp_state_use_port(), opened socket");
        Write("ftp_port_bind_socket(), socket bound to port 0");
        Write("ftp_port_listen(), listening on port");
    }

    /// <summary>
    /// Writes the DO_MORE phase's poll while the server's active-mode data connection is
    /// awaited, leaving the transfer state for <c>STOP</c> first unless already left (BL-1196).
    /// </summary>
    public void AcceptPending()
    {
        LeaveTransferState();
        Note("ftp_domore_pollset()");
    }

    /// <summary>
    /// Writes the change from the transfer state back to <c>STOP</c>, unless the state is
    /// <c>STOP</c> already.
    /// </summary>
    public void LeaveTransferState()
    {
        if (state != Stop)
        {
            Enter(Stop);
        }
    }

    /// <summary>
    /// Writes the end of the DO phase, as the passive data connection is dialled or once
    /// <c>EPRT</c> or <c>PORT</c> is accepted.
    /// </summary>
    public void DoPhaseComplete()
    {
        doPhaseRunning = false;
        dataConnectionOpen = true;
        Enter(Stop);
        Note("DO phase is complete2");
    }

    /// <summary>
    /// Writes the close of the passive data connection curl had set up for a refused <c>EPSV</c>,
    /// before it sends <c>PASV</c>.
    /// </summary>
    public void EpsvRefused() => Note("closing DATA connection");

    /// <summary>Writes the DO_MORE phase's poll while the data connection is still connecting.</summary>
    public void DataConnectionPending() => Note("ftp_domore_pollset()");

    /// <summary>Writes the note curl writes before it sends <c>RETR</c> for a file.</summary>
    public void RetrieveNext() => Note("ftp_state_retr()");

    /// <summary>
    /// Writes the start of the data transfer: the transfer initiated, and back to <c>STOP</c>
    /// unless an active-mode accept left the transfer state already.
    /// </summary>
    public void TransferInitiated()
    {
        Write("ftp_initiate_transfer()");
        LeaveTransferState();
    }

    /// <summary>Writes the close of the data connection and the start of reading the end-of-transfer reply.</summary>
    public void ClosingDataConnection()
    {
        CloseDataConnection();
        Write("getftpresponse start");
    }

    /// <summary>Writes the end-of-transfer reply read, or the reply to <c>ABOR</c> or a quote after the transfer.</summary>
    /// <param name="reply">The reply.</param>
    public void TransferReplyRead(FtpReply reply) =>
        Write(string.Create(CultureInfo.InvariantCulture, $"getftpresponse -> result=0, nread={reply.ByteCount}, ftpcode={reply.Code}"));

    /// <summary>
    /// Writes the end of the transfer, once: for a failure in the DO phase that the phase
    /// failed, the close of a data connection still open, and curl's <c>done</c> line with the
    /// result <c>ftp_done</c> keeps.
    /// </summary>
    /// <param name="exitCode">How the transfer ended.</param>
    public void Ended(CurlExitCode exitCode)
    {
        if (ended)
        {
            return;
        }

        ended = true;
        if (doPhaseRunning && exitCode != CurlExitCode.Ok)
        {
            Note("DO phase failed");
        }

        if (dataConnectionOpen)
        {
            CloseDataConnection();
        }

        int result = Array.IndexOf(ConnectionKeptFailures, exitCode) >= 0 ? 0 : (int)exitCode;
        Note(string.Create(CultureInfo.InvariantCulture, $"done, result={result}"));
    }

    private void CloseDataConnection()
    {
        dataConnectionOpen = false;
        Note("closing DATA connection");
    }

    /// <summary>
    /// The state each command enters that does not depend on the transfer. A lookup rather
    /// than a string <c>switch</c>, which compiles to a branch per character tested and
    /// put <see cref="StateOf" /> at a cyclomatic complexity of 122 (BL-1333).
    /// </summary>
    private static readonly Dictionary<string, string> FixedStates = new(StringComparer.Ordinal)
    {
        ["USER"] = "USER",
        ["PASS"] = "PASS",
        ["PWD"] = "PWD",
        ["ACCT"] = "ACCT",
        ["SYST"] = "SYST",
        ["CWD"] = "CWD",
        ["MDTM"] = "MDTM",
        ["AUTH"] = "AUTH",
        ["PBSZ"] = "PBSZ",
        ["PROT"] = "PROT",
        ["CCC"] = "CCC",
        ["PRET"] = "PRET",
        ["MKD"] = "MKD",
        ["SITE"] = "NAMEFMT",
        ["EPSV"] = "PASV",
        ["PASV"] = "PASV",
        ["EPRT"] = "PORT",
        ["PORT"] = "PORT",
    };

    /// <summary>The commands that enter <c>&lt;transfer&gt;_&lt;verb&gt;</c>, such as <c>RETR_SIZE</c>.</summary>
    private static readonly HashSet<string> TransferSteps = new(StringComparer.Ordinal) { "TYPE", "SIZE", "REST" };

    /// <summary>The commands that enter the transfer's own state, such as <c>RETR</c>.</summary>
    private static readonly HashSet<string> TransferCommands = new(StringComparer.Ordinal) { "RETR", "STOR", "APPE", "LIST", "NLST" };

    private string? StateOf(string command)
    {
        string verb = command.Split(' ', 2)[0];
        if (FixedStates.TryGetValue(verb, out string? fixedState))
        {
            return fixedState;
        }

        if (TransferSteps.Contains(verb))
        {
            return transfer + "_" + verb;
        }

        return TransferCommands.Contains(verb) ? transfer : null;
    }

    /// <summary>Writes the change to <paramref name="next" />; curl writes none when the state stays the same.</summary>
    private void Enter(string next)
    {
        if (next != state)
        {
            Write("[" + state + "] -> [" + next + "]");
            state = next;
        }
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
