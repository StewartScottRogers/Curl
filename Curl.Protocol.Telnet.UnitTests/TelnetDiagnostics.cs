using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Writes what a TELNET test arranges and gets - the URL and <c>-t</c> options, each scripted
/// read with its IAC commands decoded, the bytes sent with theirs, the data written out and the
/// transfer's result with its exit code and error text - as <c>ARRANGE</c>, <c>ACT</c>,
/// <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c> lines through the shared
/// <see cref="TestDiagnostics" /> helper.
/// </summary>
internal static class TelnetDiagnostics
{
    /// <summary>The most scripted reads written one by one; the rest are counted.</summary>
    private const int ReadCap = 8;

    /// <summary>The most decoded commands written for one byte sequence; the rest are counted.</summary>
    private const int CommandCap = 24;

    /// <summary>The most characters of one text value written; the rest are counted.</summary>
    private const int TextCap = 200;

    /// <summary>Writes the transfer's URL and the options that shape the session.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="context">The transfer's context.</param>
    public static void ArrangeContext(this TestDiagnostics diagnostics, TransferContext context)
    {
        diagnostics.Arrange("url", context.Url.OriginalString);
        diagnostics.Arrange("telnet options (-t)", context.TelnetOptions.Count == 0 ? "(none)" : Text(string.Join(" | ", context.TelnetOptions)));
        if (context.Proxy is { } proxy)
        {
            diagnostics.Arrange("proxy", $"{proxy.Kind} {proxy.Host}:{proxy.Port}");
        }

        if (context.NoBody)
        {
            diagnostics.Arrange("no body (-I)", true);
        }

        if (context.MaxFileSize is { } maxFileSize)
        {
            diagnostics.Arrange("max file size", maxFileSize);
        }

        if (context.MaxTime is { } maxTime)
        {
            diagnostics.Arrange("max time (-m)", maxTime);
        }

        diagnostics.Arrange("upload", context.Upload is null ? "(none)" : context.Upload.GetType().Name);
    }

    /// <summary>Writes the bytes the upload holds, as bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="upload">The upload's bytes.</param>
    public static void ArrangeUpload(this TestDiagnostics diagnostics, byte[] upload)
    {
        diagnostics.Arrange("upload bytes", upload.Length);
        diagnostics.Bytes("upload", upload);
    }

    /// <summary>Writes each read the scripted server returns, in order, with its IAC commands decoded.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="reads">The reads.</param>
    public static void ArrangeReads(this TestDiagnostics diagnostics, IReadOnlyList<ScriptedRead> reads)
    {
        diagnostics.Arrange("scripted reads", $"{reads.Count} reads, {reads.Sum(read => read.Bytes.Length)} bytes, then the server closes");
        for (int index = 0; index < Math.Min(reads.Count, ReadCap); index++)
        {
            ScriptedRead read = reads[index];
            diagnostics.Bytes($"read {index}", read.Bytes);
            diagnostics.Arrange($"read {index} decoded", Commands(read.Bytes));
            if (read.AfterBytesSent > 0)
            {
                diagnostics.Arrange($"read {index} waits for bytes sent", read.AfterBytesSent);
            }
        }
    }

    /// <summary>Writes the bytes each scripted read returns, when the test gives them as plain byte arrays.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="reads">The reads.</param>
    public static void ArrangeReads(this TestDiagnostics diagnostics, IReadOnlyList<byte[]> reads) =>
        diagnostics.ArrangeReads([.. reads.Select(read => new ScriptedRead(read))]);

    /// <summary>Writes the transfer's result: exit code, error text and bytes written.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result)
    {
        diagnostics.Act("exit code", ExitCode(result.ExitCode));
        diagnostics.Act("error", result.ErrorMessage is null ? "(none)" : Text(result.ErrorMessage));
        diagnostics.Act("bytes transferred", result.BytesTransferred);
        if (result.IsConnectionRefused)
        {
            diagnostics.Act("connection refused", true);
        }
    }

    /// <summary>Writes the bytes the connection was sent, with their IAC commands decoded.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="sent">The bytes sent.</param>
    public static void ActSent(this TestDiagnostics diagnostics, byte[] sent)
    {
        diagnostics.Act("sent decoded", sent.Length == 0 ? "(nothing)" : Commands(sent));
        diagnostics.Bytes("sent", sent);
    }

    /// <summary>Writes the data the session wrote out.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="output">The bytes written.</param>
    public static void ActOutput(this TestDiagnostics diagnostics, byte[] output)
    {
        diagnostics.Act("output bytes", output.Length);
        diagnostics.Bytes("output", output);
    }

    /// <summary>Writes a list of lines, such as a transcript or diagnostic log, one escaped line each.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the lines are.</param>
    /// <param name="lines">The lines.</param>
    public static void ActLines(this TestDiagnostics diagnostics, string label, IEnumerable<string> lines) =>
        diagnostics.Act(label, Lines(lines));

    /// <summary>Writes the ASSERT line for an exit code.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected exit code.</param>
    /// <param name="actual">The actual exit code.</param>
    public static void AssertExitCode(this TestDiagnostics diagnostics, CurlExitCode expected, CurlExitCode actual) =>
        diagnostics.Assert("exit code", ExitCode(expected), ExitCode(actual));

    /// <summary>Writes the ASSERT line for a whole result.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected result.</param>
    /// <param name="actual">The actual result.</param>
    public static void AssertResult(this TestDiagnostics diagnostics, TransferResult expected, TransferResult actual) =>
        diagnostics.Assert("result", Describe(expected), Describe(actual));

    /// <summary>Writes an ASSERT line between two lists of lines, each escaped.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected lines.</param>
    /// <param name="actual">The actual lines.</param>
    public static void AssertLines(this TestDiagnostics diagnostics, string label, IEnumerable<string> expected, IEnumerable<string> actual) =>
        diagnostics.Assert(label, Lines(expected), Lines(actual));

    /// <summary>Names an exit code with its number, as curl's manual does: <c>SendError (55)</c>.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>The name and number.</returns>
    public static string ExitCode(CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{exitCode} ({(int)exitCode})");

    /// <summary>
    /// Decodes a byte sequence the way a telnet peer reads it: each IAC command and option by
    /// name (<c>IAC WILL ECHO</c>), each subnegotiation with its payload length, and each run of
    /// data as <c>data(n)</c>.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The decoded commands, space separated, capped at <see cref="CommandCap" />.</returns>
    public static string Commands(ReadOnlySpan<byte> bytes)
    {
        var parts = new List<string>();
        int index = 0;
        int data = 0;
        while (index < bytes.Length)
        {
            if (bytes[index] != 0xFF || index + 1 == bytes.Length)
            {
                data++;
                index++;
                continue;
            }

            byte command = bytes[index + 1];
            if (command == 0xFF)
            {
                data++;
                index += 2;
                continue;
            }

            FlushData(parts, ref data);
            index = command switch
            {
                >= 0xFB and <= 0xFE when index + 2 < bytes.Length => AddNegotiation(parts, command, bytes[index + 2], index),
                0xFA => AddSubnegotiation(parts, bytes, index),
                _ => AddCommand(parts, command, index),
            };
        }

        FlushData(parts, ref data);
        return parts.Count <= CommandCap
            ? string.Join(' ', parts)
            : string.Join(' ', parts.Take(CommandCap)) + string.Create(CultureInfo.InvariantCulture, $" ... ({parts.Count - CommandCap} more)");
    }

    private static int AddNegotiation(List<string> parts, byte command, byte option, int index)
    {
        parts.Add($"IAC {CommandName(command)} {OptionName(option)}");
        return index + 3;
    }

    private static int AddSubnegotiation(List<string> parts, ReadOnlySpan<byte> bytes, int index)
    {
        int end = index + 2;
        while (end + 1 < bytes.Length && !(bytes[end] == 0xFF && bytes[end + 1] == 0xF0))
        {
            end++;
        }

        string option = index + 2 < bytes.Length ? OptionName(bytes[index + 2]) : "(none)";
        int payload = Math.Max(0, end - index - 3);
        bool closed = end + 1 < bytes.Length;
        parts.Add(string.Create(CultureInfo.InvariantCulture, $"IAC SB {option} [{payload} bytes]{(closed ? " IAC SE" : " (unterminated)")}"));
        return closed ? end + 2 : bytes.Length;
    }

    private static int AddCommand(List<string> parts, byte command, int index)
    {
        parts.Add($"IAC {CommandName(command)}");
        return index + 2;
    }

    private static void FlushData(List<string> parts, ref int data)
    {
        if (data > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"data({data})"));
            data = 0;
        }
    }

    private static string CommandName(byte command) => command switch
    {
        0xF0 => "SE",
        0xF1 => "NOP",
        0xF2 => "DM",
        0xF9 => "GA",
        0xFA => "SB",
        0xFB => "WILL",
        0xFC => "WONT",
        0xFD => "DO",
        0xFE => "DONT",
        _ => string.Create(CultureInfo.InvariantCulture, $"0x{command:X2}"),
    };

    private static string OptionName(byte option) => option switch
    {
        0x00 => "BINARY",
        0x01 => "ECHO",
        0x03 => "SGA",
        0x18 => "TTYPE",
        0x1F => "NAWS",
        0x23 => "XDISPLOC",
        0x27 => "NEW-ENVIRON",
        _ => option.ToString(CultureInfo.InvariantCulture),
    };

    private static string Describe(TransferResult result) =>
        string.Create(CultureInfo.InvariantCulture, $"{ExitCode(result.ExitCode)}, {result.BytesTransferred} bytes, error \"{result.ErrorMessage}\"");

    private static string Lines(IEnumerable<string> lines)
    {
        string[] all = [.. lines];
        return all.Length == 0
            ? "(none)"
            : string.Create(CultureInfo.InvariantCulture, $"{all.Length} lines: ") + Text(string.Join(" | ", all));
    }

    private static string Text(string text)
    {
        var escaped = new StringBuilder(Math.Min(text.Length, TextCap));
        foreach (char character in text.AsSpan(0, Math.Min(text.Length, TextCap)))
        {
            escaped.Append(character is >= ' ' and < (char)0x7F ? character : string.Create(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}"));
        }

        return text.Length > TextCap
            ? escaped + string.Create(CultureInfo.InvariantCulture, $" ... ({text.Length - TextCap} more characters)")
            : escaped.ToString();
    }
}
