using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One channel to the tftpd emulation of a <see cref="TftpServerConnector"/>: a read or write
/// request sent to <see cref="TftpServerConnector.TftpPort"/> is logged and answered as tftpd
/// answers it, from <see cref="TftpServerConnector.TransferPort"/>.
/// </summary>
/// <remarks>
/// tftpd never sends an OACK: it logs the request's options and goes on in 512-byte blocks. A
/// read request is served block by block, each sent once the previous one is acknowledged (and
/// resent on an acknowledgement of the one before), <c>netascii</c> turning LF into CR LF and CR
/// into CR NUL; a write request is acknowledged block by block until a block shorter than 512
/// bytes. A request whose mode is neither <c>octet</c> nor <c>netascii</c>, or whose strings are
/// not NUL-terminated, gets ERROR 4; a file name with no number after its last slash gets ERROR 2.
/// Each new DATA block's bytes are added, as received, to <see cref="TftpServerConnector.UploadedBytes"/>.
/// A receive with nothing to answer waits until cancelled.
/// </remarks>
internal sealed class TftpServerChannel(TftpServerConnector server, IPAddress address) : IDatagramChannel
{
    private const int BlockSize = 512;

    private readonly Channel<(byte[] Datagram, bool Delayed)> replies = Channel.CreateUnbounded<(byte[] Datagram, bool Delayed)>();

    private readonly IPEndPoint transferEndPoint = new(address, TftpServerConnector.TransferPort);

    private List<byte[]> blocks = [];

    private int sentBlock;

    private int expectedBlock = -1;

    /// <inheritdoc/>
    public EndPoint ServerEndPoint { get; } = new IPEndPoint(address, TftpServerConnector.TftpPort);

    /// <inheritdoc/>
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        if (datagram.Length >= 4)
        {
            Answer(datagram.Span, destination.Equals(ServerEndPoint));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        (byte[] datagram, bool delayed) = await replies.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (delayed && server.WriteDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(server.WriteDelaySeconds), server.TimeProvider, cancellationToken).ConfigureAwait(false);
        }

        datagram.CopyTo(buffer);
        return new DatagramReceived(datagram.Length, transferEndPoint);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void Answer(ReadOnlySpan<byte> datagram, bool toListener)
    {
        int opcode = BinaryPrimitives.ReadUInt16BigEndian(datagram);
        int block = BinaryPrimitives.ReadUInt16BigEndian(datagram[2..]);
        switch (opcode)
        {
            case 1 or 2 when toListener:
                Request(opcode, datagram[2..]);
                break;
            case 3:
                Receive(block, datagram[4..]);
                break;
            case 4:
                Acknowledge(block);
                break;
        }
    }

    // tftpd's do_tftp: the dump gets the opcode, the mode and each option, then the file name.
    private void Request(int opcode, ReadOnlySpan<byte> payload)
    {
        server.Log("opcode", opcode.ToString("x", CultureInfo.InvariantCulture));
        string[] strings = Encoding.Latin1.GetString(payload).Split('\0');
        for (int index = 1; index < strings.Length - 1; index += 2)
        {
            server.Log(index == 1 ? "mode" : strings[index - 1], strings[index]);
        }

        if (strings.Length < 3 || strings[^1].Length > 0)
        {
            SendError(4, "Illegal TFTP operation");
            return;
        }

        server.Log("filename", strings[0]);
        Start(opcode, strings[1].ToLowerInvariant(), server.FindFile(strings[0]));
    }

    private void Start(int opcode, string mode, byte[]? file)
    {
        if (mode is not ("octet" or "netascii"))
        {
            SendError(4, "Illegal TFTP operation");
        }
        else if (file is null)
        {
            SendError(2, "Access violation");
        }
        else
        {
            StartTransfer(opcode, mode, file);
        }
    }

    // A write request is acknowledged with block 0; a read request sends the first DATA block.
    private void StartTransfer(int opcode, string mode, byte[] file)
    {
        if (opcode == 2)
        {
            expectedBlock = 1;
            Enqueue(Packet(4, 0, []), false);
        }
        else
        {
            blocks = Blocks(mode == "netascii" ? ToNetascii(file) : file);
            sentBlock = 1;
            Enqueue(Packet(3, 1, blocks[0]), true);
        }
    }

    private void Acknowledge(int block)
    {
        if (block == sentBlock && sentBlock < blocks.Count)
        {
            sentBlock++;
            Enqueue(Packet(3, sentBlock, blocks[sentBlock - 1]), true);
        }
        else if (block == sentBlock - 1)
        {
            Enqueue(Packet(3, sentBlock, blocks[sentBlock - 1]), false);
        }
    }

    private void Receive(int block, ReadOnlySpan<byte> data)
    {
        if (block == expectedBlock)
        {
            server.RecordUpload(data);
            Enqueue(Packet(4, block, []), false);
            expectedBlock = data.Length == BlockSize ? expectedBlock + 1 : -1;
        }
        else if (block == expectedBlock - 1)
        {
            Enqueue(Packet(4, block, []), false);
        }
    }

    private void SendError(int code, string message) =>
        Enqueue(Packet(5, code, [.. Encoding.ASCII.GetBytes(message), 0]), false);

    private void Enqueue(byte[] datagram, bool delayed) => replies.Writer.TryWrite((datagram, delayed));

    private static byte[] Packet(int opcode, int number, byte[] body)
    {
        byte[] packet = new byte[4 + body.Length];
        BinaryPrimitives.WriteUInt16BigEndian(packet, (ushort)opcode);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)number);
        body.CopyTo(packet, 4);
        return packet;
    }

    // tftpd's sendtftp sends blocks while the last one sent was full, so a file whose length is a
    // multiple of 512 bytes (none included) ends with an empty block.
    private static List<byte[]> Blocks(byte[] file) =>
        [.. Enumerable.Range(0, (file.Length / BlockSize) + 1).Select(index => file[(index * BlockSize)..Math.Min(file.Length, (index + 1) * BlockSize)])];

    private static byte[] ToNetascii(byte[] file) =>
        [.. file.SelectMany(octet => octet switch
        {
            (byte)'\n' => new[] { (byte)'\r', (byte)'\n' },
            (byte)'\r' => [(byte)'\r', 0],
            _ => [octet],
        })];
}
