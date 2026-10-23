using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Binary;

/// <summary>
/// Answers a WebSocket connection the way proto2_servers.py's <c>Ws</c> class does (port 18440): logs the
/// upgrade request without its <c>Sec-WebSocket-Key</c> line, answers 101, sends a text, a binary and a close
/// frame, then logs every frame the client sends until a close frame. Where the Python handler crashed (an
/// upgrade request with no usable key, an unmasked frame with data, a short mask) the connection ends.
/// </summary>
public sealed class WebSocketResponder(ProtocolWireLog log)
{
    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string KeyHeader = "sec-websocket-key";
    private const int CloseOpcode = 8;
    private const int MaxLoggedBytes = 20;

    private static readonly char[] PythonWhitespace = [' ', '\t', '\n', '\r', '\v', '\f'];

    /// <summary>Serves one connection until a close frame, the client hanging up, or a crash-equivalent frame.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new DifferentialHttpLineReader(connection);
        var lines = (await ReadRequestAsync(reader, cancellationToken)).Split("\r\n");
        log.Add(string.Concat(lines.Where(line => !IsKeyLine(line)).Select(line => line + "\n")));
        var key = KeyOf(lines);
        if (key is null)
        {
            return;
        }

        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.Latin1.GetBytes(key + AcceptGuid)));
        byte[] reply =
        [
            .. Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"),
            0x81, 0x05, .. "hello"u8,
            0x82, 0x03, 0x01, 0x02, 0x03,
            0x88, 0x00,
        ];
        await connection.WriteAsync(reply, cancellationToken);
        await connection.FlushAsync(cancellationToken);
        while (await LogNextFrameAsync(reader, cancellationToken) is { } opcode && opcode != CloseOpcode)
        {
        }
    }

    private static async Task<string> ReadRequestAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        using var raw = new MemoryStream();
        byte[] line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
            raw.Write(line);
        }
        while (line.Length != 0 && !line.AsSpan().SequenceEqual("\r\n"u8));

        return Encoding.Latin1.GetString(raw.ToArray());
    }

    private static bool IsKeyLine(string line) => line.StartsWith(KeyHeader, StringComparison.OrdinalIgnoreCase);

    private static string? KeyOf(string[] lines)
    {
        var parts = lines.FirstOrDefault(IsKeyLine)?.Split(':');
        return parts is { Length: > 1 } ? parts[1].Trim(PythonWhitespace) : null;
    }

    private async Task<int?> LogNextFrameAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        var header = await reader.ReadAsync(2, cancellationToken);
        if (header.Length < 2)
        {
            return null;
        }

        var opcode = header[0] & 15;
        var masked = (header[1] & 128) != 0;
        if (await ReadLengthAsync(reader, header[1] & 127, cancellationToken) is not { } length
            || await ReadUnmaskedDataAsync(reader, masked, length, cancellationToken) is not { } data)
        {
            return null;
        }

        log.Add(string.Create(CultureInfo.InvariantCulture, $"frame op={opcode} masked={(masked ? "True" : "False")} len={length} data={PythonLiteral.FormatBytes(data)}"));
        return opcode;
    }

    private static async Task<int?> ReadLengthAsync(DifferentialHttpLineReader reader, int length, CancellationToken cancellationToken)
    {
        if (length != 126)
        {
            return length;
        }

        var extended = await reader.ReadAsync(2, cancellationToken);
        return extended.Length < 2 ? null : BinaryPrimitives.ReadUInt16BigEndian(extended);
    }

    private static async Task<byte[]?> ReadUnmaskedDataAsync(DifferentialHttpLineReader reader, bool masked, int length, CancellationToken cancellationToken)
    {
        var mask = masked ? await reader.ReadAsync(4, cancellationToken) : [];
        var data = await reader.ReadAsync(length, cancellationToken);
        return mask.Length < 4 && data.Length > mask.Length
            ? null
            : data.Select((value, index) => (byte)(value ^ mask[index % 4])).Take(MaxLoggedBytes).ToArray();
    }
}
