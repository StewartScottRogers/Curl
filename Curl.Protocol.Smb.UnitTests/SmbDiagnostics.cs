using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Writes what an SMB test arranges and gets - the URL and options, each scripted reply and
/// the bytes sent, with each SMB message's command and NT status decoded, and the transfer's
/// result - as <c>ARRANGE</c>, <c>ACT</c>, <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c> lines.
/// </summary>
internal static class SmbDiagnostics
{
    /// <summary>The most scripted replies written one by one; the rest are counted.</summary>
    private const int RepliesShown = 8;

    /// <summary>The most messages named in a message list; the rest are counted.</summary>
    private const int MessagesNamed = 12;

    private static readonly Dictionary<byte, string> CommandNames = new()
    {
        [SmbMessageHeader.NegotiateCommand] = "NEGOTIATE",
        [SmbMessageHeader.SessionSetupAndXCommand] = "SESSION_SETUP_ANDX",
        [SmbMessageHeader.TreeConnectAndXCommand] = "TREE_CONNECT_ANDX",
        [SmbMessageHeader.NtCreateAndXCommand] = "NT_CREATE_ANDX",
        [SmbMessageHeader.ReadAndXCommand] = "READ_ANDX",
        [SmbMessageHeader.WriteAndXCommand] = "WRITE_ANDX",
        [SmbMessageHeader.CloseCommand] = "CLOSE",
        [SmbMessageHeader.TreeDisconnectCommand] = "TREE_DISCONNECT",
    };

    /// <summary>Gets the four bytes that start every SMB message after its NetBIOS header.</summary>
    private static ReadOnlySpan<byte> SmbSignature => [0xff, 0x53, 0x4d, 0x42];

    /// <summary>Writes the transfer's URL and the options it was given; never the password.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="context">The transfer's context.</param>
    public static void ArrangeContext(this TestDiagnostics diagnostics, TransferContext context)
    {
        diagnostics.Arrange("url", context.Url.OriginalString);
        diagnostics.Arrange(
            "credentials",
            context.Credentials is { } credentials
                ? $"user \"{credentials.UserName}\", password {credentials.Password.Length} characters"
                : "none");
        if (context.Upload is { } upload)
        {
            diagnostics.Arrange("upload", upload.CanSeek ? $"{upload.Length} bytes, seekable" : "unseekable");
        }

        if (context.NoBody)
        {
            diagnostics.Arrange("no body", true);
        }

        if (context.MaxFileSize is { } maxFileSize)
        {
            diagnostics.Arrange("max file size", maxFileSize);
        }

        if (context.Proxy is { } proxy)
        {
            diagnostics.Arrange("proxy", proxy);
        }
    }

    /// <summary>Writes each reply the server is scripted to send, with its messages decoded, and any scripted failure.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="connection">The scripted connection.</param>
    public static void ArrangeReplies(this TestDiagnostics diagnostics, ScriptedConnection connection)
    {
        IReadOnlyList<byte[]> reads = connection.Reads;
        diagnostics.Arrange("scripted replies", $"{reads.Count}: {Messages(reads.SelectMany(read => read).ToArray())}");
        for (int index = 0; index < Math.Min(reads.Count, RepliesShown); index++)
        {
            diagnostics.Bytes($"reply {index} ({Messages(reads[index])})", reads[index]);
        }

        if (reads.Count > RepliesShown)
        {
            diagnostics.Arrange("further replies", $"{reads.Count - RepliesShown} not shown");
        }

        if (connection.FailingWrite != 0 || connection.FailingRead != 0)
        {
            diagnostics.Arrange(
                "scripted failure",
                $"write {connection.FailingWrite}, read {connection.FailingRead} (0 is none): {connection.Failure.GetType().Name} \"{connection.Failure.Message}\""
                + (connection.Failure.InnerException is { } inner ? $" from {inner.GetType().Name} \"{inner.Message}\"" : string.Empty));
        }
    }

    /// <summary>Writes the transfer's result: exit code by name and number, bytes, error text and report.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result) =>
        diagnostics.Act("result", Describe(result));

    /// <summary>Writes the bytes the client sent, with their SMB messages decoded.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="connection">The scripted connection.</param>
    public static void ActSent(this TestDiagnostics diagnostics, ScriptedConnection connection)
    {
        byte[] sent = connection.Sent;
        diagnostics.Act("sent", $"{sent.Length} bytes: {Messages(sent)}; disposed {connection.IsDisposed}");
        diagnostics.Bytes("sent", sent);
    }

    /// <summary>Writes the bytes a transfer wrote to its output.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="output">The output.</param>
    public static void ActOutput(this TestDiagnostics diagnostics, Stream output)
    {
        diagnostics.Act("output", $"{output.Length} bytes");
        if (output is MemoryStream memory)
        {
            diagnostics.Bytes("output", memory.ToArray());
        }
    }

    /// <summary>Writes a transcript of <c>-v</c> lines, escaped so it stays on one line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="transcript">The transcript.</param>
    public static void ActTranscript(this TestDiagnostics diagnostics, IReadOnlyCollection<string> transcript) =>
        diagnostics.Act($"transcript ({transcript.Count} lines)", Lines(transcript));

    /// <summary>Writes the lines a diagnostic log recorded, each with its level.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="log">The log.</param>
    public static void ActLog(this TestDiagnostics diagnostics, RecordingDiagnosticLog log) =>
        diagnostics.Act(
            $"diagnostic log ({log.Lines.Count} lines)",
            Lines(log.Lines.Select(line => $"{line.Level} {line.Component}: {line.Message}")));

    /// <summary>Writes an ASSERT line for the transfer's exit code and one for its error text.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The exit code expected.</param>
    /// <param name="expectedMessage">The error text expected, or <see langword="null" /> to write only the exit code.</param>
    /// <param name="actual">The result the transfer returned.</param>
    public static void AssertResult(this TestDiagnostics diagnostics, CurlExitCode expected, string? expectedMessage, TransferResult actual)
    {
        diagnostics.Assert("exit code", ExitCode(expected), ExitCode(actual.ExitCode));
        if (expectedMessage is not null)
        {
            diagnostics.Diff("error message", expectedMessage, actual.ErrorMessage ?? string.Empty);
        }
    }

    /// <summary>Writes a DIFF line for the bytes sent against the bytes expected, naming the messages expected.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The bytes expected.</param>
    /// <param name="actual">The bytes sent.</param>
    public static void DiffSent(this TestDiagnostics diagnostics, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual) =>
        diagnostics.Diff($"sent ({Messages(expected)})", expected, actual);

    /// <summary>Writes a DIFF line for the last bytes sent against the ending expected.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expectedEnding">The bytes the sent bytes should end with.</param>
    /// <param name="actual">The bytes sent.</param>
    public static void DiffSentEnding(this TestDiagnostics diagnostics, ReadOnlySpan<byte> expectedEnding, ReadOnlySpan<byte> actual) =>
        diagnostics.Diff(
            $"last {expectedEnding.Length} bytes sent ({Messages(expectedEnding)})",
            expectedEnding,
            actual[Math.Max(0, actual.Length - expectedEnding.Length)..]);

    /// <summary>Joins lines in brackets, each quoted with control characters escaped.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The lines as one line.</returns>
    public static string Lines(IEnumerable<string> lines) => "[" + string.Join(", ", lines.Select(Escape)) + "]";

    /// <summary>Quotes text, escaping control and non-ASCII characters so it stays on one line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text quoted, with <c>\xNN</c> escapes.</returns>
    public static string Escape(string text)
    {
        StringBuilder escaped = new("\"");
        foreach (char character in text)
        {
            escaped.Append(character is < ' ' or >= (char)0x7f
                ? string.Create(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}")
                : character.ToString());
        }

        return escaped.Append('"').ToString();
    }

    /// <summary>Describes a transfer result in one line.</summary>
    /// <param name="result">The result.</param>
    /// <returns>Exit code by name and number, bytes, error text, refusal, report sizes and remote time.</returns>
    public static string Describe(TransferResult result) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ExitCode(result.ExitCode)}, {result.BytesTransferred} bytes, error \"{result.ErrorMessage}\", refused {result.IsConnectionRefused}, "
            + $"report {(result.Report is { } report ? $"upload {report.UploadSize} download {report.DownloadSize}" : "none")}, "
            + $"remote time {(result.SourceLastWriteTimeUtc is { } time ? time.ToString("O", CultureInfo.InvariantCulture) : "none")}");

    /// <summary>Names an exit code with its number.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>Such as <c>RecvError (56)</c>.</returns>
    public static string ExitCode(CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{exitCode} ({(int)exitCode})");

    /// <summary>
    /// Names the SMB messages in <paramref name="bytes" /> in order, each with its command and NT
    /// status, and says where the bytes end inside a message.
    /// </summary>
    /// <param name="bytes">The bytes, from a NetBIOS frame boundary.</param>
    /// <returns>The message list, such as <c>NEGOTIATE ok, SESSION_SETUP_ANDX 0xC000006D</c>.</returns>
    public static string Messages(ReadOnlySpan<byte> bytes)
    {
        List<string> names = [];
        int offset = 0;
        while (offset < bytes.Length)
        {
            if (names.Count == MessagesNamed)
            {
                names.Add($"... {bytes.Length - offset} more bytes");
                break;
            }

            if (bytes.Length - offset < SmbMessageHeader.NetBiosHeaderLength)
            {
                names.Add($"{bytes.Length - offset} bytes short of a NetBIOS header");
                break;
            }

            int frameLength = (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
            int messageLength = SmbMessageHeader.NetBiosHeaderLength + frameLength;
            ReadOnlySpan<byte> message = bytes[offset..Math.Min(bytes.Length, offset + messageLength)];
            names.Add(Message(message, frameLength) + (message.Length < messageLength ? $" (cut short at {message.Length} of {messageLength} bytes)" : string.Empty));
            offset += messageLength;
        }

        return names.Count == 0 ? "nothing" : string.Join(", ", names);
    }

    private static string Message(ReadOnlySpan<byte> message, int frameLength)
    {
        if (message.Length < 13 || !message[4..8].SequenceEqual(SmbSignature))
        {
            return string.Create(CultureInfo.InvariantCulture, $"frame of {frameLength} bytes");
        }

        string command = CommandNames.TryGetValue(message[8], out string? name)
            ? name
            : string.Create(CultureInfo.InvariantCulture, $"command 0x{message[8]:x2}");
        uint status = BinaryPrimitives.ReadUInt32LittleEndian(message[9..13]);
        return status == 0 ? $"{command} ok" : string.Create(CultureInfo.InvariantCulture, $"{command} 0x{status:X8}");
    }
}
