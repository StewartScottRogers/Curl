using Curl.Extension.Coap.Dtls.Record;
using Curl.Extension.Coap.Dtls.Wire;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using TlsContentType = Curl.Tls.TlsContentType;

namespace Curl.Extension.Coap.Dtls.Handshake;

/// <summary>
/// A DTLS 1.2 association after its handshake: it sends application data protected under epoch 1 and
/// gives the server's, dropping a record that is not protected application data, fails its tag or is a
/// replay (RFC 6347 section 4.1.2.6).
/// </summary>
internal sealed class DtlsClientSession(IDatagramChannel channel, DtlsRecordProtector protector, DtlsRecordUnprotector unprotector) : IDisposable
{
    private const int MaximumDatagramLength = 65535;

    private readonly Queue<byte[]> received = new();

    private readonly byte[] buffer = new byte[MaximumDatagramLength];

    /// <summary>Sends <paramref name="payload" /> as one protected application data record.</summary>
    public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        channel.SendAsync(protector.Protect(TlsContentType.ApplicationData, payload.Span), channel.ServerEndPoint, cancellationToken);

    /// <summary>Gives the plaintext of the server's next protected application data record.</summary>
    public async ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (received.Count == 0)
        {
            DatagramReceived datagram = await channel.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            foreach (byte[] record in DtlsDatagramRecords.Split(buffer.AsSpan(0, datagram.Length)))
            {
                if (unprotector.TryUnprotect(record, out DtlsRecordHeader header, out byte[] plaintext) && IsProtectedApplicationData(header))
                {
                    received.Enqueue(plaintext);
                }
            }
        }

        return received.Dequeue();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        protector.Dispose();
        unprotector.Dispose();
    }

    private static bool IsProtectedApplicationData(DtlsRecordHeader header) =>
        header.Epoch != 0 && header.ContentType == TlsContentType.ApplicationData;
}
