using System.Buffers.Binary;

namespace Curl.Kerberos;

/// <summary>
/// Sends one request to a realm's KDCs and returns the first reply (RFC 4120 section 7.2):
/// each KDC <see cref="KerberosKdcLocator" /> finds, in order, until one answers. A
/// <see cref="KerberosKdcTransport.UdpOrTcp" /> KDC gets UDP when the request is no longer
/// than <see cref="KerberosConfiguration.UdpPreferenceLimit" /> and TCP otherwise; a UDP
/// reply of <c>KRB_ERR_RESPONSE_TOO_BIG</c> is asked again of the same KDC over TCP. An HTTPS
/// (MS-KKDCP) KDC is sent a <see cref="KerberosKdcProxyMessage" /> through
/// <paramref name="proxyTransport" />, or skipped when there is none (ADR-0168, BL-827).
/// </summary>
/// <param name="configuration">The parsed <c>krb5.conf</c>.</param>
/// <param name="locator">Finds the realm's KDCs.</param>
/// <param name="transport">Moves the bytes to UDP and TCP KDCs.</param>
/// <param name="proxyTransport">Moves the bytes to HTTPS KDC proxies, or <see langword="null" /> to skip them.</param>
internal sealed class KerberosKdcSender(KerberosConfiguration configuration, KerberosKdcLocator locator, IKerberosKdcTransport transport, IKerberosKdcProxyTransport? proxyTransport = null)
{
    /// <summary>The longest TCP reply accepted: a KDC reply carrying a large PAC is tens of kilobytes.</summary>
    internal const int MaximumTcpReplyLength = 1 << 20;

    private const int LengthPrefixSize = sizeof(uint);

    /// <summary>Sends <paramref name="request" /> to <paramref name="realm" />'s KDCs.</summary>
    /// <param name="realm">The realm whose KDCs answer.</param>
    /// <param name="request">The encoded AS-REQ or TGS-REQ.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The reply, not yet decoded.</returns>
    /// <exception cref="KerberosKdcException">
    /// <see cref="KerberosKdcError.NoKdc" />, <see cref="KerberosKdcError.KdcUnreachable" />, or
    /// <see cref="KerberosKdcError.UnexpectedReply" /> for a TCP reply longer than <see cref="MaximumTcpReplyLength" />.
    /// </exception>
    public async Task<byte[]> SendAsync(string realm, byte[] request, CancellationToken cancellationToken)
    {
        IReadOnlyList<KerberosKdcAddress> located = await locator.LocateAsync(realm, cancellationToken).ConfigureAwait(false);
        KerberosKdcAddress[] kdcs = [.. located.Where(kdc => kdc.Transport != KerberosKdcTransport.Https || proxyTransport is not null)];
        if (kdcs.Length == 0)
        {
            throw new KerberosKdcException(KerberosKdcError.NoKdc);
        }

        foreach (KerberosKdcAddress kdc in kdcs)
        {
            try
            {
                return await SendToAsync(realm, kdc, request, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // RFC 4120 section 7.2.3: try the realm's next KDC.
            }
        }

        throw new KerberosKdcException(KerberosKdcError.KdcUnreachable);
    }

    private static bool IsResponseTooBig(byte[] reply)
    {
        try
        {
            return KerberosMessage.PeekType(reply) == KerberosMessageType.Error
                && KerberosErrorMessage.Decode(reply).ErrorCode == KerberosErrorMessage.ResponseTooBig;
        }
        catch (KerberosMessageException)
        {
            return false;
        }
    }

    private async Task<byte[]> SendToAsync(string realm, KerberosKdcAddress kdc, byte[] request, CancellationToken cancellationToken)
    {
        if (kdc.Transport == KerberosKdcTransport.Https)
        {
            return await ExchangeThroughProxyAsync(realm, kdc, request, cancellationToken).ConfigureAwait(false);
        }

        bool overUdp = kdc.Transport == KerberosKdcTransport.Udp
            || (kdc.Transport == KerberosKdcTransport.UdpOrTcp && request.Length <= configuration.UdpPreferenceLimit);
        if (!overUdp)
        {
            return await ExchangeOverTcpAsync(kdc, request, cancellationToken).ConfigureAwait(false);
        }

        byte[] reply = await transport.ExchangeDatagramAsync(kdc.Host, kdc.Port, request, cancellationToken).ConfigureAwait(false);
        return IsResponseTooBig(reply)
            ? await ExchangeOverTcpAsync(kdc, request, cancellationToken).ConfigureAwait(false)
            : reply;
    }

    /// <summary>
    /// Posts the request to an MS-KKDCP proxy inside a <c>KDC-PROXY-MESSAGE</c> naming
    /// <paramref name="realm" />, as MIT's <c>sendto_kdc.c</c> does. A reply that is not one is
    /// an <see cref="IOException" />, so the realm's next KDC is tried, as MIT does.
    /// </summary>
    private async Task<byte[]> ExchangeThroughProxyAsync(string realm, KerberosKdcAddress kdc, byte[] request, CancellationToken cancellationToken)
    {
        byte[] body = new KerberosKdcProxyMessage(request, realm).Encode();
        byte[] reply = await proxyTransport!.PostAsync(kdc.Host, kdc.Port, kdc.HttpsPath, body, cancellationToken).ConfigureAwait(false);
        try
        {
            return KerberosKdcProxyMessage.Decode(reply).KerberosMessage;
        }
        catch (KerberosMessageException exception)
        {
            throw new IOException("The KDC proxy's reply is not a KDC-PROXY-MESSAGE.", exception);
        }
    }

    /// <summary>Sends the request over TCP, each message preceded by its length in four big-endian bytes (RFC 4120 section 7.2.2).</summary>
    private async Task<byte[]> ExchangeOverTcpAsync(KerberosKdcAddress kdc, byte[] request, CancellationToken cancellationToken)
    {
        Stream stream = await transport.ConnectStreamAsync(kdc.Host, kdc.Port, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            byte[] framed = new byte[LengthPrefixSize + request.Length];
            BinaryPrimitives.WriteInt32BigEndian(framed, request.Length);
            request.CopyTo(framed, LengthPrefixSize);
            await stream.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            byte[] prefix = new byte[LengthPrefixSize];
            await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(prefix);
            if (length > MaximumTcpReplyLength)
            {
                throw new KerberosKdcException(KerberosKdcError.UnexpectedReply);
            }

            byte[] reply = new byte[length];
            await stream.ReadExactlyAsync(reply, cancellationToken).ConfigureAwait(false);
            return reply;
        }
    }
}
