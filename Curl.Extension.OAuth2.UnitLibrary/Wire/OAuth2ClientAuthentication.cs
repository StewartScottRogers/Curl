namespace Curl.Extension.OAuth2.Wire;

/// <summary>How a confidential client sends its password to the server (RFC 6749 §2.3.1).</summary>
public enum OAuth2ClientAuthentication
{
    /// <summary>In an HTTP Basic <c>Authorization</c> header, the way every server must support.</summary>
    Basic,

    /// <summary>As the <c>client_id</c> and <c>client_secret</c> body parameters.</summary>
    RequestBody,
}
