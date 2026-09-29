namespace Curl.Tls;

/// <summary>
/// The <c>status_request</c> extension (RFC 6066 section 8, RFC 8446 section 4.4.2.1): an
/// OCSP request in a ClientHello, and the stapled OCSP response in a TLS 1.3 Certificate
/// entry. Only the <c>ocsp</c> status type (1) exists; any other decodes as
/// <see cref="TlsAlertDescription.IllegalParameter" />.
/// </summary>
public static class StatusRequestExtension
{
    private const byte OcspStatusType = 1;

    /// <summary>Returns a ClientHello's <c>status_request</c> carrying <paramref name="request" />.</summary>
    /// <param name="request">The responder IDs and request extensions.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeOcspRequest(OcspStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TlsWriter writer = new();
        writer.WriteUInt8(OcspStatusType);
        writer.WriteVector(2, list =>
        {
            foreach (byte[] responderId in request.ResponderIds)
            {
                list.WriteOpaque(2, responderId);
            }
        });
        writer.WriteOpaque(2, request.RequestExtensions);
        return new TlsExtension(TlsExtensionType.StatusRequest, writer.ToArray());
    }

    /// <summary>Decodes a ClientHello's <c>status_request</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The OCSP request, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<OcspStatusRequest> DecodeOcspRequest(byte[] data)
    {
        TlsReader reader = new(data);
        ReadStatusType(reader);
        TlsReader list = reader.ReadVector(2);
        List<byte[]> responderIds = [];
        while (list.HasMore)
        {
            responderIds.Add(list.ReadOpaque(2));
        }

        byte[] requestExtensions = reader.ReadOpaque(2);
        return reader.Finish(new OcspStatusRequest(responderIds, requestExtensions));
    }

    /// <summary>Returns a Certificate entry's <c>status_request</c> stapling <paramref name="ocspResponse" />.</summary>
    /// <param name="ocspResponse">The DER-encoded OCSP response.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeOcspResponse(byte[] ocspResponse)
    {
        TlsWriter writer = new();
        writer.WriteUInt8(OcspStatusType);
        writer.WriteOpaque(3, ocspResponse);
        return new TlsExtension(TlsExtensionType.StatusRequest, writer.ToArray());
    }

    /// <summary>Decodes a Certificate entry's <c>status_request</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The DER-encoded OCSP response, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<byte[]> DecodeOcspResponse(byte[] data)
    {
        TlsReader reader = new(data);
        ReadStatusType(reader);
        return reader.Finish(reader.ReadOpaque(3));
    }

    private static void ReadStatusType(TlsReader reader)
    {
        if (reader.ReadUInt8() != OcspStatusType)
        {
            reader.Fail(TlsAlertDescription.IllegalParameter);
        }
    }
}
