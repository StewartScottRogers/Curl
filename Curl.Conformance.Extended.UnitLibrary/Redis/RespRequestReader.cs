using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Redis;

/// <summary>
/// Reads the commands curl sends, written from the RESP specification ("Sending commands to a Redis server"): each
/// command is an array of bulk strings, <c>*&lt;count&gt;\r\n</c> then <c>$&lt;length&gt;\r\n&lt;bytes&gt;\r\n</c>
/// per argument. Anything else is a malformed request and throws <see cref="InvalidDataException" />.
/// </summary>
/// <param name="connection">The server's end of the connection.</param>
internal sealed class RespRequestReader(ReferenceConnection connection)
{
    private readonly byte[] buffer = new byte[4096];

    private int start;

    private int end;

    /// <summary>Reads the next command.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The command's arguments, or <see langword="null" /> once curl closed the connection between commands.</returns>
    internal async ValueTask<byte[][]?> ReadCommandAsync(CancellationToken cancellationToken)
    {
        if (!await FillAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        byte[][] arguments = new byte[await ReadLengthAsync((byte)'*', cancellationToken).ConfigureAwait(false)][];
        for (int index = 0; index < arguments.Length; index++)
        {
            arguments[index] = await ReadExactAsync(await ReadLengthAsync((byte)'$', cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            if (!(await ReadExactAsync(2, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual("\r\n"u8))
            {
                throw new InvalidDataException("A request's bulk string is not ended by CR LF.");
            }
        }

        return arguments;
    }

    // A '*' count or '$' length line: the prefix, then a non-negative decimal number.
    private async ValueTask<int> ReadLengthAsync(byte prefix, CancellationToken cancellationToken)
    {
        string line = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return line.Length > 1 && line[0] == prefix && int.TryParse(line.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int length)
            ? length
            : throw new InvalidDataException($"A request line '{line}' is not '{(char)prefix}' and a length.");
    }

    private async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        List<byte> line = [];
        while (line.Count < 2 || line[^2] != (byte)'\r' || line[^1] != (byte)'\n')
        {
            line.Add(await ReadByteAsync(cancellationToken).ConfigureAwait(false));
        }

        return Encoding.Latin1.GetString(CollectionsMarshal.AsSpan(line)[..^2]);
    }

    private async ValueTask<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < length; index++)
        {
            bytes[index] = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        }

        return bytes;
    }

    private async ValueTask<byte> ReadByteAsync(CancellationToken cancellationToken) =>
        await FillAsync(cancellationToken).ConfigureAwait(false)
            ? buffer[start++]
            : throw new EndOfStreamException("curl closed the connection inside a request.");

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        if (start < end)
        {
            return true;
        }

        start = 0;
        end = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return end > 0;
    }
}
