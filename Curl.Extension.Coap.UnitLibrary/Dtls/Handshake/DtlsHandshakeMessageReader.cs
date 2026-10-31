using Curl.Extension.Coap.Dtls.Wire;

namespace Curl.Extension.Coap.Dtls.Handshake;

/// <summary>
/// Gives the peer's handshake messages in message_seq order, each once (RFC 6347 section 4.2.2): it
/// rebuilds each message from its fragments, holds a message that arrives before the one expected,
/// and drops the fragments of a message already given, which a retransmitted flight repeats.
/// </summary>
internal sealed class DtlsHandshakeMessageReader
{
    private readonly Dictionary<ushort, DtlsHandshakeReassembly> reassemblies = [];

    private ushort nextMessageSequence;

    /// <summary>Adds one fragment the peer sent.</summary>
    public void Add(DtlsHandshakeFragment fragment)
    {
        ushort messageSequence = fragment.Header.MessageSequence;
        if (messageSequence < nextMessageSequence)
        {
            return;
        }

        if (reassemblies.TryGetValue(messageSequence, out DtlsHandshakeReassembly? reassembly))
        {
            reassembly.Add(fragment);
        }
        else
        {
            reassemblies[messageSequence] = new DtlsHandshakeReassembly(fragment);
        }
    }

    /// <summary>Takes the next message in order once all its bytes have arrived.</summary>
    /// <returns><see langword="true" /> with the message; <see langword="false" /> while it is still incomplete or missing.</returns>
    public bool TryTakeNext(out DtlsHandshakeMessage message)
    {
        message = null!;
        if (!reassemblies.TryGetValue(nextMessageSequence, out DtlsHandshakeReassembly? reassembly) || !reassembly.IsComplete)
        {
            return false;
        }

        reassemblies.Remove(nextMessageSequence);
        message = new DtlsHandshakeMessage(reassembly.MessageType, nextMessageSequence++, reassembly.ToMessageBody());
        return true;
    }
}
