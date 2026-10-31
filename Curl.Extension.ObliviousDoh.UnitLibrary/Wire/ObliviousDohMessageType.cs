namespace Curl.Extension.ObliviousDoh.Wire;

/// <summary>The <c>message_type</c> octet of an <see cref="ObliviousDohMessage" /> (RFC 9230 section 6.1).</summary>
public enum ObliviousDohMessageType : byte
{
    /// <summary>An encrypted query, <c>0x01</c>.</summary>
    Query = 0x01,

    /// <summary>An encrypted response, <c>0x02</c>.</summary>
    Response = 0x02,
}
