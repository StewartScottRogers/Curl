using System.Buffers.Binary;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// The reference container's end of one connection: reads protocol headers and frames (Part 2 sections 2.2 and 2.3)
/// and writes them with the container's <see cref="Amqp1ReferenceEncoder"/>.
/// </summary>
/// <param name="connection">The server's end of the connection.</param>
/// <param name="encoder">How the container encodes what it sends.</param>
/// <param name="cancellationToken">Cancels the exchange.</param>
public sealed class Amqp1ReferenceSession(ReferenceConnection connection, Amqp1ReferenceEncoder encoder, CancellationToken cancellationToken)
{
    private const int FrameHeaderLength = 8;

    /// <summary>The protocol header for <paramref name="protocolId"/>, version 1.0.0.</summary>
    /// <param name="protocolId">0 for AMQP, 3 for SASL.</param>
    /// <returns>The eight header bytes.</returns>
    public static byte[] Header(byte protocolId) => [(byte)'A', (byte)'M', (byte)'Q', (byte)'P', protocolId, 1, 0, 0];

    /// <summary>Reads the client's next protocol header.</summary>
    /// <returns>Its eight bytes; null once the client has closed the connection.</returns>
    public async ValueTask<byte[]?> ReadHeaderAsync()
    {
        byte[] header = new byte[FrameHeaderLength];
        return await FillAsync(header).ConfigureAwait(false) ? header : null;
    }

    /// <summary>Reads the client's next frame.</summary>
    /// <returns>The frame; null once the client has closed the connection, before or inside it.</returns>
    public async ValueTask<Amqp1ReferenceFrame?> ReadFrameAsync()
    {
        byte[] header = new byte[FrameHeaderLength];
        if (!await FillAsync(header).ConfigureAwait(false))
        {
            return null;
        }

        byte[] rest = new byte[BinaryPrimitives.ReadUInt32BigEndian(header) - FrameHeaderLength];
        return await FillAsync(rest).ConfigureAwait(false) ? Parse(header, rest) : null;
    }

    /// <summary>Sends the protocol header for <paramref name="protocolId"/>.</summary>
    /// <param name="protocolId">0 for AMQP, 3 for SASL.</param>
    /// <returns>A task that ends once it is written.</returns>
    public ValueTask SendHeaderAsync(byte protocolId) => connection.WriteAsync(Header(protocolId), cancellationToken);

    /// <summary>Sends an AMQP frame on <paramref name="channel"/> carrying <paramref name="performative"/> and <paramref name="payload"/>.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="performative">The performative.</param>
    /// <param name="payload">The bytes after it, such as a transfer's message.</param>
    /// <returns>A task that ends once it is written.</returns>
    public ValueTask SendAsync(ushort channel, Amqp1ReferenceDescribed performative, byte[]? payload = null) =>
        SendFrameAsync(Amqp1ReferenceFrame.AmqpType, channel, performative, payload ?? []);

    /// <summary>Sends an empty AMQP frame on channel 0, the frame with no body an idle peer sends to keep a connection open (Part 2 sections 2.3.2 and 2.4.5).</summary>
    /// <returns>A task that ends once it is written.</returns>
    public ValueTask SendEmptyFrameAsync() => connection.WriteAsync(new byte[] { 0, 0, 0, FrameHeaderLength, 2, Amqp1ReferenceFrame.AmqpType, 0, 0 }, cancellationToken);

    /// <summary>Sends a SASL frame carrying <paramref name="body"/> on channel 0.</summary>
    /// <param name="body">The SASL frame body.</param>
    /// <returns>A task that ends once it is written.</returns>
    public ValueTask SendSaslAsync(Amqp1ReferenceDescribed body) => SendFrameAsync(Amqp1ReferenceFrame.SaslType, 0, body, []);

    private ValueTask SendFrameAsync(byte type, ushort channel, Amqp1ReferenceDescribed body, byte[] payload)
    {
        byte[] encoded = encoder.Encode(body);
        byte[] frame = new byte[FrameHeaderLength + encoded.Length + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)frame.Length);
        frame[4] = 2;
        frame[5] = type;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6), channel);
        encoded.CopyTo(frame, FrameHeaderLength);
        payload.CopyTo(frame, FrameHeaderLength + encoded.Length);
        return connection.WriteAsync(frame, cancellationToken);
    }

    // The data offset counts 4-byte words from the frame's start; any extended header is skipped.
    private static Amqp1ReferenceFrame Parse(byte[] header, byte[] rest)
    {
        int position = (header[4] * 4) - FrameHeaderLength;
        ushort channel = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6));
        if (position == rest.Length)
        {
            return new Amqp1ReferenceFrame(header[5], channel, null, []);
        }

        Amqp1ReferenceDescribed body = Amqp1ReferenceDecoder.Read(rest, ref position) as Amqp1ReferenceDescribed
            ?? throw new InvalidDataException("A frame body is not a described value.");
        return new Amqp1ReferenceFrame(header[5], channel, body, rest[position..]);
    }

    private async ValueTask<bool> FillAsync(byte[] buffer)
    {
        for (int filled = 0; filled < buffer.Length;)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }
}
