namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>The media types Oblivious HTTP registers (RFC 9458 section 9, RFC 9540 section 3).</summary>
public static class ObliviousHttpMediaTypes
{
    /// <summary><c>application/ohttp-keys</c>: a gateway's list of length-prefixed key configurations.</summary>
    public const string KeyConfigurations = "application/ohttp-keys";

    /// <summary><c>message/ohttp-req</c>: an encapsulated request.</summary>
    public const string EncapsulatedRequest = "message/ohttp-req";

    /// <summary><c>message/ohttp-res</c>: an encapsulated response.</summary>
    public const string EncapsulatedResponse = "message/ohttp-res";
}
