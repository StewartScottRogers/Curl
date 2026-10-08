using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Writes what an MQTT test arranges and gets - the URL and options, each scripted read and
/// the bytes sent and written, with their MQTT packet types decoded, and the transfer's result
/// - as <c>ARRANGE</c>, <c>ACT</c>, <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c> lines.
/// </summary>
internal static class MqttDiagnostics
{
    /// <summary>The most scripted reads written one by one; the rest are counted.</summary>
    private const int ReadsShown = 8;

    /// <summary>The most packets named in a packet list; the rest are counted.</summary>
    private const int PacketsNamed = 12;

    /// <summary>Writes the transfer's URL and the options it was given.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="context">The transfer's context.</param>
    public static void ArrangeContext(this TestDiagnostics diagnostics, TransferContext context)
    {
        string url = context.Url.OriginalString;
        diagnostics.Arrange("url", url.Length > 120 ? $"{url[..120]}... ({url.Length} characters)" : url);
        if (context.PostData is { } postData)
        {
            diagnostics.Arrange("post data", $"{postData.Length} bytes");
            diagnostics.Bytes("post data", postData.Span);
        }

        if (context.Credentials is { } credentials)
        {
            diagnostics.Arrange("credentials", $"user {credentials.UserName.Length} characters, password {credentials.Password.Length} characters");
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

    /// <summary>Writes each scripted read the peer will send, with its packets decoded.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="reads">The scripted reads; <see langword="null" /> is a failing read.</param>
    public static void ArrangeReads(this TestDiagnostics diagnostics, IReadOnlyList<byte[]?> reads)
    {
        diagnostics.Arrange("scripted reads", reads.Count);
        for (int index = 0; index < Math.Min(reads.Count, ReadsShown); index++)
        {
            if (reads[index] is { } read)
            {
                diagnostics.Bytes($"read {index} ({Packets(read)})", read);
            }
            else
            {
                diagnostics.Arrange($"read {index}", "fails");
            }
        }

        if (reads.Count > ReadsShown)
        {
            diagnostics.Arrange("further reads", $"{reads.Count - ReadsShown} not shown, {reads.Sum(read => read?.Length ?? 0)} bytes in all");
        }
    }

    /// <summary>Writes one chunk a test sends the transfer through a gated connection.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="chunk">The chunk.</param>
    public static void ArrangeSent(this TestDiagnostics diagnostics, byte[] chunk)
    {
        diagnostics.Arrange("peer sends", Packets(chunk));
        diagnostics.Bytes("peer sends", chunk);
    }

    /// <summary>Writes a <see cref="ManualTimeProvider" /> advance.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="clock">The clock, after the advance.</param>
    /// <param name="advance">How far it was advanced.</param>
    public static void ArrangeAdvance(this TestDiagnostics diagnostics, ManualTimeProvider clock, TimeSpan advance) =>
        diagnostics.Arrange(
            "clock advanced",
            string.Create(CultureInfo.InvariantCulture, $"{advance.TotalMilliseconds} ms to {clock.Now.TotalMilliseconds} ms, next timer {Milliseconds(clock.NextTimerDueAt)}"));

    /// <summary>Writes the transfer's result: exit code by name and number, bytes and error text.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result) =>
        diagnostics.Act("result", Describe(result));

    /// <summary>Writes bytes the transfer produced, with their MQTT packets decoded.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the bytes are, such as <c>sent</c>.</param>
    /// <param name="bytes">The bytes.</param>
    public static void ActPackets(this TestDiagnostics diagnostics, string label, byte[] bytes)
    {
        diagnostics.Act(label, $"{bytes.Length} bytes: {Packets(bytes)}");
        diagnostics.Bytes(label, bytes);
    }

    /// <summary>Writes bytes the transfer wrote to its output, which are PUBLISH bodies rather than packets.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="output">The output.</param>
    public static void ActOutput(this TestDiagnostics diagnostics, RecordingStream output)
    {
        byte[] written = output.ToArray();
        diagnostics.Act("output", $"{written.Length} bytes in {output.Writes.Count} writes");
        diagnostics.Bytes("output", written);
    }

    /// <summary>Writes the last lines of a <c>-v</c> transcript, escaped so each stays on one line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="transcript">The transcript.</param>
    public static void ActTranscript(this TestDiagnostics diagnostics, IReadOnlyList<string> transcript) =>
        diagnostics.Act(
            $"transcript ({transcript.Count} lines, last {Math.Min(transcript.Count, ReadsShown)})",
            Lines(transcript.Skip(Math.Max(0, transcript.Count - ReadsShown))));

    /// <summary>Writes the lines a diagnostic log recorded, each with its level.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="log">The log.</param>
    public static void ActLog(this TestDiagnostics diagnostics, RecordingDiagnosticLog log) =>
        diagnostics.Act(
            $"diagnostic log ({log.Lines.Count} lines)",
            Lines(log.Lines.Select(line => $"{line.Level} {line.Component}: {line.Message}")));

    /// <summary>Escapes each line and joins them in brackets.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The lines as one line.</returns>
    public static string Lines(IEnumerable<string> lines) => "[" + string.Join(", ", lines.Select(Escape)) + "]";

    /// <summary>Quotes text, escaping CR, LF and other control or non-ASCII characters so it stays on one line.</summary>
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

    /// <summary>Writes an ASSERT line for the transfer's result.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The result expected.</param>
    /// <param name="actual">The result the transfer returned.</param>
    public static void AssertResult(this TestDiagnostics diagnostics, TransferResult expected, TransferResult actual) =>
        diagnostics.Assert("result", Describe(expected), Describe(actual));

    /// <summary>Writes a DIFF line for the bytes sent to the peer.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The bytes expected.</param>
    /// <param name="actual">The bytes sent.</param>
    public static void DiffSent(this TestDiagnostics diagnostics, byte[] expected, byte[] actual) =>
        diagnostics.Diff($"sent ({Packets(expected)})", expected, actual);

    /// <summary>Writes a DIFF line for the bytes written to the output.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The bytes expected.</param>
    /// <param name="actual">The bytes written.</param>
    public static void DiffOutput(this TestDiagnostics diagnostics, byte[] expected, byte[] actual) =>
        diagnostics.Diff("output", expected, actual);

    /// <summary>Describes a transfer result in one line.</summary>
    /// <param name="result">The result.</param>
    /// <returns>Exit code by name and number, bytes transferred, error text and whether the connection was refused.</returns>
    public static string Describe(TransferResult result) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{result.ExitCode} ({(int)result.ExitCode}), {result.BytesTransferred} bytes, error \"{result.ErrorMessage}\", refused {result.IsConnectionRefused}");

    /// <summary>
    /// Names the MQTT packets in <paramref name="bytes" /> in order, each with its remaining
    /// length, and says where the bytes end inside a packet.
    /// </summary>
    /// <param name="bytes">The bytes, from a packet boundary.</param>
    /// <returns>The packet list, such as <c>CONNACK(2) SUBACK(3)</c>.</returns>
    public static string Packets(ReadOnlySpan<byte> bytes)
    {
        StringBuilder names = new();
        int offset = 0;
        int count = 0;
        while (offset < bytes.Length)
        {
            if (count == PacketsNamed)
            {
                return names.Append(CultureInfo.InvariantCulture, $" ... ({bytes.Length - offset} more bytes)").ToString();
            }

            string name = PacketName(bytes[offset]);
            if (!TryReadRemainingLength(bytes, offset + 1, out int remaining, out int headerLength))
            {
                return names.Append(names.Length == 0 ? string.Empty : " ").Append(name).Append("(header cut short)").ToString();
            }

            names.Append(names.Length == 0 ? string.Empty : " ").Append(CultureInfo.InvariantCulture, $"{name}({remaining})");
            int end = offset + 1 + headerLength + remaining;
            if (end > bytes.Length)
            {
                return names.Append(CultureInfo.InvariantCulture, $" cut short after {bytes.Length - offset - 1 - headerLength} of {remaining} bytes").ToString();
            }

            offset = end;
            count++;
        }

        return names.Length == 0 ? "empty" : names.ToString();
    }

    private static string PacketName(byte header) => (header >> 4) switch
    {
        1 => "CONNECT",
        2 => "CONNACK",
        3 => $"PUBLISH/qos{(header >> 1) & 3}",
        4 => "PUBACK",
        8 => "SUBSCRIBE",
        9 => "SUBACK",
        12 => "PINGREQ",
        13 => "PINGRESP",
        14 => "DISCONNECT",
        _ => string.Create(CultureInfo.InvariantCulture, $"type{header >> 4}"),
    } + ((header & 0x0f) is 0 or 2 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"[flags {header & 0x0f:x}]"));

    private static bool TryReadRemainingLength(ReadOnlySpan<byte> bytes, int start, out int remaining, out int headerLength)
    {
        remaining = 0;
        for (headerLength = 1; headerLength <= 4 && start + headerLength - 1 < bytes.Length; headerLength++)
        {
            byte digit = bytes[start + headerLength - 1];
            remaining |= (digit & 0x7f) << (7 * (headerLength - 1));
            if ((digit & 0x80) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Milliseconds(TimeSpan? time) =>
        time is { } due ? string.Create(CultureInfo.InvariantCulture, $"due at {due.TotalMilliseconds} ms") : "none";
}
