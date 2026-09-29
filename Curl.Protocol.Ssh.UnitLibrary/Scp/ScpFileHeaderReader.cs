using System.Text;
using Curl.Protocol.Ssh.Connection;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Reads the start of an SCP download as libssh2 1.11.1's <c>scp_recv</c> does for curl
/// 8.21.0 (ADR-0221): sends the first acknowledgement, a zero byte; reads the
/// <c>T&lt;mtime&gt; 0 &lt;atime&gt; 0</c> line and acknowledges it; reads the
/// <c>C&lt;mode&gt; &lt;size&gt; &lt;name&gt;</c> line and acknowledges it. Each line is read
/// a byte at a time, checked byte by byte, and held to libssh2's 256-byte response buffer.
/// </summary>
/// <param name="channel">The channel <c>scp -pf</c> runs on.</param>
internal sealed class ScpFileHeaderReader(SshSessionChannel channel)
{
    /// <summary>libssh2's <c>LIBSSH2_SCP_RESPONSE_BUFLEN</c>: the longest line it reads.</summary>
    internal const int ResponseBufferLength = 256;

    private const string InvalidResponse = "Invalid response from SCP server";

    private static readonly byte[] Acknowledgement = [0];

    // A T line must start with 'T', or libssh2 reports the remote error line it holds as
    // "Failed to recv file"; after that only digits, spaces and line ends.
    private static readonly LineRules TimesLine = new(
        (byte)'T',
        "Failed to recv file",
        next => char.IsAsciiDigit((char)next) || next is (byte)' ' or (byte)'\r' or (byte)'\n',
        CompleteLength: 9,
        MinimumLength: 8);

    // A C line must start with 'C'; after that no control byte but a line end.
    private static readonly LineRules FileLine = new(
        (byte)'C',
        InvalidResponse,
        next => next >= 32 || next is (byte)'\r' or (byte)'\n',
        CompleteLength: 7,
        MinimumLength: 6);

    private readonly byte[] oneByte = new byte[1];

    /// <summary>
    /// Reads the header of the file <c>scp -pf</c> sends.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The size the <c>C</c> line gives, which may be negative.</returns>
    /// <exception cref="SshTransferException">
    /// The server sent an error line, a malformed line or no line before the channel ended
    /// (exit 78, libssh2's message), or the connection broke (exit 79, <c>Failed reading
    /// SCP response</c>).
    /// </exception>
    internal async ValueTask<long> ReadFileSizeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
            CheckTimes(await ReadLineAsync(TimesLine, cancellationToken).ConfigureAwait(false));
            await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
            long size = ParseSize(await ReadLineAsync(FileLine, cancellationToken).ConfigureAwait(false));
            await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
            return size;
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
            throw SshTransferException.ScpResponseReadFailed();
        }
    }

    // mtime, then its microseconds, then atime, each ended by a space; the values are
    // not used.
    private static void CheckTimes(string times)
    {
        string afterModified = FieldAndRest(times, "malformed mtime").Remainder;
        string afterMicroseconds = FieldAndRest(afterModified, "malformed mtime.usec").Remainder;
        FieldAndRest(afterMicroseconds, "too short or malformed");
    }

    private static long ParseSize(string file)
    {
        (string mode, string afterMode) = FieldAndRest(file, "malformed mode");
        if (!ScpHeaderNumber.TryParse(mode, 8, out _))
        {
            throw Protocol($"{InvalidResponse}, invalid mode");
        }

        string sizeText = FieldAndRest(afterMode, "too short or malformed").Field;
        return ScpHeaderNumber.TryParse(sizeText, 10, out long size) ? size : throw Protocol($"{InvalidResponse}, invalid size");
    }

    // A field must end in a space and must not be empty, as libssh2's strchr checks.
    private static (string Field, string Remainder) FieldAndRest(string text, string failure)
    {
        int space = text.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? (text[..space], text[(space + 1)..]) : throw Protocol($"{InvalidResponse}, {failure}");
    }

    private static SshTransferException Protocol(string message) => SshTransferException.ScpProtocolError(message);

    private async ValueTask AcknowledgeAsync(CancellationToken cancellationToken) =>
        await channel.SendAsync(Acknowledgement, cancellationToken).ConfigureAwait(false);

    // A line is complete at a line feed once it is long enough; a shorter one reads on.
    private async ValueTask<string> ReadLineAsync(LineRules rules, CancellationToken cancellationToken)
    {
        byte[] line = new byte[ResponseBufferLength];
        for (int length = 1; length <= ResponseBufferLength; length++)
        {
            line[length - 1] = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            rules.Check(line, length);
            if (length >= rules.CompleteLength && line[length - 1] == '\n')
            {
                return rules.Content(line.AsSpan(0, length));
            }
        }

        throw Protocol("Unterminated response from SCP server");
    }

    private async ValueTask<byte> ReadByteAsync(CancellationToken cancellationToken)
    {
        int read = await channel.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false);
        return read == 1 ? oneByte[0] : throw Protocol("Unexpected channel close");
    }

    // What libssh2 checks in one kind of line.
    private sealed record LineRules(byte First, string WrongFirstMessage, Func<byte, bool> IsAllowed, int CompleteLength, int MinimumLength)
    {
        internal void Check(byte[] line, int length)
        {
            if (line[0] != First)
            {
                throw Protocol(WrongFirstMessage);
            }

            if (length > 1 && !IsAllowed(line[length - 1]))
            {
                throw Protocol("Invalid data in SCP response");
            }
        }

        // The line without its first byte and its line ends, one character per byte.
        internal string Content(ReadOnlySpan<byte> line)
        {
            ReadOnlySpan<byte> trimmed = line.TrimEnd("\r\n"u8);
            return trimmed.Length >= MinimumLength ? Encoding.Latin1.GetString(trimmed[1..]) : throw Protocol($"{InvalidResponse}, too short");
        }
    }
}
