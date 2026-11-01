namespace Curl.Extended.Protocol.Coap.Wire;

/// <summary>
/// The 8-bit Code field of a CoAP message header: a 3-bit class and a 5-bit detail, written
/// <c>c.dd</c> (RFC 7252 sections 3 and 12.1).
/// </summary>
/// <param name="Value">The byte as it appears on the wire.</param>
internal readonly record struct CoapCode(byte Value)
{
    /// <summary>0.00, the Empty message.</summary>
    public static readonly CoapCode Empty = Of(0, 0);

    /// <summary>0.01 GET.</summary>
    public static readonly CoapCode Get = Of(0, 1);

    /// <summary>0.02 POST.</summary>
    public static readonly CoapCode Post = Of(0, 2);

    /// <summary>0.03 PUT.</summary>
    public static readonly CoapCode Put = Of(0, 3);

    /// <summary>0.04 DELETE.</summary>
    public static readonly CoapCode Delete = Of(0, 4);

    /// <summary>2.01 Created.</summary>
    public static readonly CoapCode Created = Of(2, 1);

    /// <summary>2.02 Deleted.</summary>
    public static readonly CoapCode Deleted = Of(2, 2);

    /// <summary>2.03 Valid.</summary>
    public static readonly CoapCode Valid = Of(2, 3);

    /// <summary>2.04 Changed.</summary>
    public static readonly CoapCode Changed = Of(2, 4);

    /// <summary>2.05 Content.</summary>
    public static readonly CoapCode Content = Of(2, 5);

    /// <summary>2.31 Continue (RFC 7959 section 2.9.1).</summary>
    public static readonly CoapCode Continue = Of(2, 31);

    /// <summary>4.00 Bad Request.</summary>
    public static readonly CoapCode BadRequest = Of(4, 0);

    /// <summary>4.04 Not Found.</summary>
    public static readonly CoapCode NotFound = Of(4, 4);

    /// <summary>4.13 Request Entity Too Large (RFC 7252 section 5.9.2.9).</summary>
    public static readonly CoapCode RequestEntityTooLarge = Of(4, 13);

    /// <summary>5.00 Internal Server Error.</summary>
    public static readonly CoapCode InternalServerError = Of(5, 0);

    /// <summary>The class, 0 to 7: 0 a request, 2 success, 4 client error, 5 server error.</summary>
    public int Class => Value >> 5;

    /// <summary>The detail, 0 to 31.</summary>
    public int Detail => Value & 0x1F;

    /// <summary>The code of class <paramref name="codeClass"/> and detail <paramref name="detail"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The class is past 7 or the detail past 31.</exception>
    public static CoapCode Of(int codeClass, int detail)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)codeClass, 7u, nameof(codeClass));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)detail, 31u, nameof(detail));
        return new CoapCode((byte)((codeClass << 5) | detail));
    }

    /// <summary>The code written <c>c.dd</c>, as RFC 7252 writes it: <c>2.05</c>.</summary>
    public override string ToString() => $"{Class}.{Detail:D2}";
}
