using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Wire;

/// <summary>
/// Reassembles one handshake message from its fragments, which may arrive out of order,
/// repeated or overlapping (RFC 6347 section 4.2.3).
/// </summary>
internal sealed class DtlsHandshakeReassembly
{
    private readonly byte[] body;

    private readonly bool[] arrived;

    private int arrivedCount;

    /// <summary>Starts reassembling the message <paramref name="first" /> belongs to, and adds it.</summary>
    public DtlsHandshakeReassembly(DtlsHandshakeFragment first)
    {
        MessageType = first.Header.MessageType;
        MessageSequence = first.Header.MessageSequence;
        body = new byte[first.Header.Length];
        arrived = new bool[first.Header.Length];
        Add(first);
    }

    /// <summary>The message's type.</summary>
    public HandshakeType MessageType { get; }

    /// <summary>The message's message_seq.</summary>
    public ushort MessageSequence { get; }

    /// <summary>Whether every byte of the message body has arrived.</summary>
    public bool IsComplete => arrivedCount == body.Length;

    /// <summary>Adds a fragment's bytes.</summary>
    /// <exception cref="FormatException">
    /// The fragment's type, message_seq or message length differs from the message's, or its
    /// overlapping bytes differ from those already received.
    /// </exception>
    public void Add(DtlsHandshakeFragment fragment)
    {
        DtlsHandshakeHeader header = fragment.Header;
        if (header.MessageType != MessageType || header.MessageSequence != MessageSequence || header.Length != body.Length)
        {
            throw new FormatException($"A DTLS handshake fragment of message {header.MessageSequence} does not belong to message {MessageSequence}.");
        }

        for (int index = 0; index < header.FragmentLength; index++)
        {
            AddByte(header.FragmentOffset + index, fragment.Body[index]);
        }
    }

    /// <summary>The reassembled message body.</summary>
    /// <exception cref="InvalidOperationException">Bytes of the message have not arrived yet.</exception>
    public byte[] ToMessageBody()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException($"Only {arrivedCount} of the {body.Length} bytes of handshake message {MessageSequence} have arrived.");
        }

        return [.. body];
    }

    private void AddByte(int position, byte value)
    {
        if (!arrived[position])
        {
            arrived[position] = true;
            body[position] = value;
            arrivedCount++;
        }
        else if (body[position] != value)
        {
            throw new FormatException($"Overlapping fragments of DTLS handshake message {MessageSequence} disagree at byte {position}.");
        }
    }
}
