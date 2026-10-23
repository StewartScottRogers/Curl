using System.Buffers.Binary;
using System.Globalization;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Binary;

/// <summary>
/// Answers a TFTP request datagram the way proto2_servers.py's <c>Tftp</c> class does (UDP port 18460): logs the
/// opcode, file, mode and options; a read request is answered with one data block and the first ACK is logged;
/// a write request is acknowledged block by block until a short block and the stored bytes are logged. A request
/// the Python handler crashed on (under two bytes, or without a file and a mode) is ignored.
/// </summary>
public sealed class TftpResponder(ProtocolWireLog log)
{
    private const int ReadRequest = 1;
    private const int WriteRequest = 2;
    private const int MaxAckSize = 100;
    private const int MaxDataSize = 1000;
    private const int FullDataPacketSize = 516;

    /// <summary>Handles one request datagram, talking to the client through <paramref name="transfer"/>.</summary>
    public async Task RespondAsync(byte[] request, ITftpTransferSocket transfer, CancellationToken cancellationToken)
    {
        if (request.Length < 2)
        {
            return;
        }

        var opcode = BinaryPrimitives.ReadUInt16BigEndian(request);
        var parts = SplitOnNul(request.AsSpan(2));
        if (parts.Count < 2)
        {
            return;
        }

        var options = string.Join(", ", parts.Skip(2).Select(part => PythonLiteral.FormatBytes(part)));
        log.Add(string.Create(CultureInfo.InvariantCulture, $"op={opcode} file={PythonLiteral.FormatBytes(parts[0])} mode={PythonLiteral.FormatBytes(parts[1])} opts=[{options}]"));
        if (opcode == ReadRequest)
        {
            await SendFileAsync(transfer, cancellationToken);
        }
        else if (opcode == WriteRequest)
        {
            await StoreFileAsync(transfer, cancellationToken);
        }
    }

    private static List<byte[]> SplitOnNul(ReadOnlySpan<byte> bytes)
    {
        List<byte[]> parts = [];
        int nul;
        while ((nul = bytes.IndexOf((byte)0)) >= 0)
        {
            parts.Add(bytes[..nul].ToArray());
            bytes = bytes[(nul + 1)..];
        }

        parts.Add(bytes.ToArray());
        return parts;
    }

    private async Task SendFileAsync(ITftpTransferSocket transfer, CancellationToken cancellationToken)
    {
        await transfer.SendAsync([0, 3, 0, 1, .. "tftp file contents\n"u8], cancellationToken);
        if (await transfer.ReceiveAsync(cancellationToken) is { } ack)
        {
            log.Add("ack=" + Convert.ToHexStringLower(ack.AsSpan(0, Math.Min(ack.Length, MaxAckSize))));
        }
    }

    private async Task StoreFileAsync(ITftpTransferSocket transfer, CancellationToken cancellationToken)
    {
        await transfer.SendAsync([0, 4, 0, 0], cancellationToken);
        using var stored = new MemoryStream();
        while (await transfer.ReceiveAsync(cancellationToken) is { Length: >= 4 } received)
        {
            var data = received[..Math.Min(received.Length, MaxDataSize)];
            stored.Write(data, 4, data.Length - 4);
            await transfer.SendAsync([0, 4, data[2], data[3]], cancellationToken);
            if (data.Length < FullDataPacketSize)
            {
                break;
            }
        }

        log.Add("stored=" + PythonLiteral.FormatBytes(stored.ToArray()));
    }
}
