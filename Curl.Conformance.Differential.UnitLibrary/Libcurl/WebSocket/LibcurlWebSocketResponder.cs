using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>
/// Serves one <see cref="LibcurlWebSocketCase"/>'s connection: logs the upgrade request without
/// its <c>Sec-WebSocket-Key</c> line, answers 101 and the case's frames, then (unless the case's
/// server hangs up) logs every frame the client sends, unmasked, answering a ping with a pong and
/// a close with a close, until the close, the client hanging up or a cut-short frame.
/// </summary>
/// <param name="webSocketCase">The case whose frames are sent.</param>
/// <param name="log">Where the request and the client's frames are logged.</param>
public sealed class LibcurlWebSocketResponder(LibcurlWebSocketCase webSocketCase, ProtocolWireLog log)
{
    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string KeyHeader = "sec-websocket-key:";
    private const int TwoByteLength = 126;

    /// <summary>Serves the connection.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="cancellationToken">Stops the serving.</param>
    /// <returns>A task that ends when the connection is done with.</returns>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var reader = new DifferentialHttpLineReader(connection);
        var key = await LogRequestAsync(reader, cancellationToken);
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.Latin1.GetBytes(key + AcceptGuid)));
        await SendAsync(connection, [.. Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"), .. webSocketCase.ServerFrames], cancellationToken);
        if (webSocketCase.ServerHangsUp)
        {
            return;
        }

        while (await ReadFrameAsync(reader, cancellationToken) is { } frame && await AnswerAsync(connection, frame, cancellationToken))
        {
        }
    }

    private async Task<bool> AnswerAsync(Stream connection, ClientFrame frame, CancellationToken cancellationToken)
    {
        log.Add(string.Create(CultureInfo.InvariantCulture, $"frame fin={frame.Final} op={frame.Opcode} masked={frame.Masked} len={frame.Payload.Length} data={PythonLiteral.FormatBytes(frame.Payload)}"));
        var answer = frame.Opcode switch
        {
            LibcurlWebSocketFrames.Ping => LibcurlWebSocketFrames.Pong,
            LibcurlWebSocketFrames.Close => LibcurlWebSocketFrames.Close,
            _ => -1,
        };
        if (answer >= 0)
        {
            await SendAsync(connection, LibcurlWebSocketFrames.Frame(answer, frame.Payload), cancellationToken);
        }

        return frame.Opcode != LibcurlWebSocketFrames.Close;
    }

    private async Task<string> LogRequestAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        var request = new StringBuilder();
        var key = "";
        string line;
        do
        {
            line = Encoding.Latin1.GetString(await reader.ReadLineAsync(cancellationToken));
            if (line.StartsWith(KeyHeader, StringComparison.OrdinalIgnoreCase))
            {
                key = line[KeyHeader.Length..].Trim();
            }
            else
            {
                request.Append(line);
            }
        }
        while (line.Length > 0 && line != "\r\n");

        log.Add(request.ToString());
        return key;
    }

    private static async Task SendAsync(Stream connection, byte[] bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken);
        await connection.FlushAsync(cancellationToken);
    }

    private static async Task<ClientFrame?> ReadFrameAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        var header = await reader.ReadAsync(2, cancellationToken);
        if (header.Length < 2 || await ReadLengthAsync(reader, header[1] & 0x7F, cancellationToken) is not (>= 0 and var length))
        {
            return null;
        }

        var masked = (header[1] & 0x80) != 0;
        var payload = await ReadPayloadAsync(reader, masked, length, cancellationToken);
        return payload is null ? null : new ClientFrame((header[0] & 0x80) != 0, header[0] & 0x0F, masked, payload);
    }

    private static async Task<byte[]?> ReadPayloadAsync(DifferentialHttpLineReader reader, bool masked, int length, CancellationToken cancellationToken)
    {
        var mask = masked ? await reader.ReadAsync(4, cancellationToken) : [0, 0, 0, 0];
        var payload = await reader.ReadAsync(length, cancellationToken);
        if (mask.Length < 4 || payload.Length < length)
        {
            return null;
        }

        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] ^= mask[index % 4];
        }

        return payload;
    }

    private static async Task<int> ReadLengthAsync(DifferentialHttpLineReader reader, int length, CancellationToken cancellationToken)
    {
        if (length != TwoByteLength)
        {
            return length;
        }

        var extended = await reader.ReadAsync(2, cancellationToken);
        return extended.Length < 2 ? -1 : BinaryPrimitives.ReadUInt16BigEndian(extended);
    }

    private sealed record ClientFrame(bool Final, int Opcode, bool Masked, byte[] Payload);
}
