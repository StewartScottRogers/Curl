using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Writes an SMB transfer's steps to Curl's diagnostic log under the <c>smb</c> component
/// (ADR-0222): each reply's command and NT status at <c>verbose</c>, the negotiate, session
/// setup, tree connect and open at <c>info</c>, a refused step's NT status and the failure
/// that ends the transfer at <c>error</c>, and the transfer's end at <c>info</c>.
/// </summary>
/// <remarks>
/// Every line tests <see cref="IDiagnosticLog.IsEnabled" /> before it is formatted, and no
/// line carries the password or the NTLM responses computed from it.
/// </remarks>
/// <param name="diagnosticLog">Where the lines are written.</param>
internal sealed class SmbTransferLog(IDiagnosticLog diagnosticLog)
{
    private static readonly Dictionary<byte, string> CommandNames = new()
    {
        [SmbMessageHeader.NegotiateCommand] = "SMB_COM_NEGOTIATE",
        [SmbMessageHeader.SessionSetupAndXCommand] = "SMB_COM_SESSION_SETUP_ANDX",
        [SmbMessageHeader.TreeConnectAndXCommand] = "SMB_COM_TREE_CONNECT_ANDX",
        [SmbMessageHeader.NtCreateAndXCommand] = "SMB_COM_NT_CREATE_ANDX",
        [SmbMessageHeader.ReadAndXCommand] = "SMB_COM_READ_ANDX",
        [SmbMessageHeader.WriteAndXCommand] = "SMB_COM_WRITE_ANDX",
        [SmbMessageHeader.CloseCommand] = "SMB_COM_CLOSE",
        [SmbMessageHeader.TreeDisconnectCommand] = "SMB_COM_TREE_DISCONNECT",
    };

    /// <summary>Gets a log that writes nothing, for a reader built without one.</summary>
    public static SmbTransferLog None { get; } = new(NoDiagnosticLog.Instance);

    /// <summary>Writes a received message's command and NT status at <c>verbose</c>.</summary>
    /// <param name="message">The whole message, NetBIOS header included.</param>
    public void Received(byte[] message)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"received {CommandName(message[SmbMessageHeader.NetBiosHeaderLength + 4])} status {Status(SmbMessageHeader.ReadStatus(message))}");
        }
    }

    /// <summary>Writes that the dialect was negotiated, at <c>info</c>.</summary>
    public void Negotiated() =>
        WriteIfEnabled(DiagnosticLogLevel.Info, "negotiate done: dialect NT LM 0.12");

    /// <summary>Writes that the session was set up, at <c>info</c>.</summary>
    /// <param name="userId">The UID the server assigned.</param>
    public void SessionSetUp(ushort userId)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"session setup done: UID {userId.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes that the tree connect was accepted, at <c>info</c>.</summary>
    /// <param name="share">The share connected to, as the URL decoded it.</param>
    /// <param name="treeId">The TID the server assigned.</param>
    public void TreeConnected(byte[] share, ushort treeId)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"tree connect to share {Encoding.UTF8.GetString(share)} done: TID {treeId.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes that the file was opened, and its size, at <c>info</c>.</summary>
    /// <param name="size">The file's size the open response carries.</param>
    public void Opened(long size)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"file opened: {size.ToString(CultureInfo.InvariantCulture)} bytes");
        }
    }

    /// <summary>Writes a step the server refused, with its NT status, at <c>error</c>.</summary>
    /// <param name="step">The step, such as <c>tree connect</c>.</param>
    /// <param name="status">The NT status of the reply.</param>
    public void Refused(string step, uint status)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, $"{step} refused: status {Status(status)}");
        }
    }

    /// <summary>
    /// Writes the transfer's end: its bytes and elapsed milliseconds at <c>info</c> when it
    /// succeeded, otherwise its <see cref="CurlExitCode" /> and message at <c>error</c>.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    public void Ended(TransferResult result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
        {
            Done(result.BytesTransferred, elapsed);
        }
        else
        {
            Failed(result);
        }
    }

    private static string CommandName(byte command) =>
        CommandNames.TryGetValue(command, out string? name) ? name : $"command 0x{command:x2}";

    private static string Status(uint status) => $"0x{status:X8}";

    private void WriteIfEnabled(DiagnosticLogLevel level, string message)
    {
        if (diagnosticLog.IsEnabled(level))
        {
            Write(level, message);
        }
    }

    private void Done(long bytes, TimeSpan elapsed)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"transfer done: {bytes.ToString(CultureInfo.InvariantCulture)} bytes in {((long)elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)} ms");
        }
    }

    private void Failed(TransferResult result)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, $"transfer failed with {result.ExitCode} (exit {((int)result.ExitCode).ToString(CultureInfo.InvariantCulture)}): {result.ErrorMessage}");
        }
    }

    private void Write(DiagnosticLogLevel level, string message) =>
        diagnosticLog.Write(level, DiagnosticLogComponents.Smb, message);
}
