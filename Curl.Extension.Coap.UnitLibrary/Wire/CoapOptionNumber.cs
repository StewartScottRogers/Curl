namespace Curl.Extension.Coap.Wire;

/// <summary>
/// The registered CoAP option numbers (RFC 7252 sections 5.10 and 12.2, RFC 7959 section 2.1,
/// RFC 7641 section 2).
/// </summary>
internal static class CoapOptionNumber
{
    /// <summary>If-Match (1).</summary>
    public const int IfMatch = 1;

    /// <summary>Uri-Host (3).</summary>
    public const int UriHost = 3;

    /// <summary>ETag (4).</summary>
    public const int ETag = 4;

    /// <summary>If-None-Match (5).</summary>
    public const int IfNoneMatch = 5;

    /// <summary>Observe (6, RFC 7641).</summary>
    public const int Observe = 6;

    /// <summary>Uri-Port (7).</summary>
    public const int UriPort = 7;

    /// <summary>Location-Path (8).</summary>
    public const int LocationPath = 8;

    /// <summary>Uri-Path (11).</summary>
    public const int UriPath = 11;

    /// <summary>Content-Format (12).</summary>
    public const int ContentFormat = 12;

    /// <summary>Max-Age (14).</summary>
    public const int MaxAge = 14;

    /// <summary>Uri-Query (15).</summary>
    public const int UriQuery = 15;

    /// <summary>Accept (17).</summary>
    public const int Accept = 17;

    /// <summary>Location-Query (20).</summary>
    public const int LocationQuery = 20;

    /// <summary>Block2 (23, RFC 7959).</summary>
    public const int Block2 = 23;

    /// <summary>Block1 (27, RFC 7959).</summary>
    public const int Block1 = 27;

    /// <summary>Size2 (28, RFC 7959).</summary>
    public const int Size2 = 28;

    /// <summary>Proxy-Uri (35).</summary>
    public const int ProxyUri = 35;

    /// <summary>Proxy-Scheme (39).</summary>
    public const int ProxyScheme = 39;

    /// <summary>Size1 (60).</summary>
    public const int Size1 = 60;
}
