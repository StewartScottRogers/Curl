using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// Reads what a NATS server sends over one connection: control lines ended by CRLF and the payload
/// bytes that follow a <c>MSG</c> line (NATS client protocol, "Protocol conventions").
/// </summary>
/// <param name="connection">The connection to read.</param>
internal sealed class NatsWireReader(IConnection connection)
{
    /// <summary>The longest control line accepted, so a server cannot make the client buffer without end.</summary>
    public const int MaxLineLength = 65536;

    private readonly byte[] buffer = new byte[16384];

    private int start;

    private int end;

    /// <summary>Reads the next control line without its CRLF.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The line, or <see langword="null"/> when the server closed the connection before one began.</returns>
    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new MemoryStream();
        while (true)
        {
            int newline = Array.IndexOf(buffer, (byte)'\n', start, end - start);
            if (newline >= 0)
            {
                line.Write(buffer, start, newline - start);
                start = newline + 1;
                return LineOf(line);
            }

            line.Write(buffer, start, end - start);
            CheckLength(line);
            if (!await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return line.Length == 0 ? null : throw new NatsException(CurlExitCode.RecvError, NatsMessages.ClosedMidMessage);
            }
        }
    }

    /// <summary>Reads a payload of the given length and the CRLF that ends it.</summary>
    /// <param name="length">The payload's length in bytes.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The payload.</returns>
    public async ValueTask<byte[]> ReadPayloadAsync(int length, CancellationToken cancellationToken)
    {
        byte[] payload = new byte[length + 2];
        int filled = 0;
        while (filled < payload.Length)
        {
            if (start == end && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new NatsException(CurlExitCode.RecvError, NatsMessages.ClosedMidMessage);
            }

            int count = Math.Min(end - start, payload.Length - filled);
            Array.Copy(buffer, start, payload, filled, count);
            start += count;
            filled += count;
        }

        return payload[length] == '\r' && payload[length + 1] == '\n'
            ? payload[..length]
            : throw new NatsException(CurlExitCode.WeirdServerReply, NatsMessages.PayloadNotEndedByCrlf);
    }

    private static string LineOf(MemoryStream line)
    {
        CheckLength(line);
        ReadOnlySpan<byte> bytes = line.GetBuffer().AsSpan(0, (int)line.Length);
        return Encoding.UTF8.GetString(bytes.EndsWith("\r"u8) ? bytes[..^1] : bytes);
    }

    private static void CheckLength(MemoryStream line)
    {
        if (line.Length > MaxLineLength)
        {
            throw new NatsException(CurlExitCode.WeirdServerReply, NatsMessages.LineTooLong);
        }
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        start = 0;
        end = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return end > 0;
    }
}
