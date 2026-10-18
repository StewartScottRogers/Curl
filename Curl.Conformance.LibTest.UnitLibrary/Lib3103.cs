namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3103.c</c>: <see cref="LibSharedCookieThroughProxy"/> for <c>http://localhost/</c> with a <c>Set-Cookie</c>
/// line carrying neither <c>Max-Age</c> nor <c>Expires</c>.
/// </summary>
public sealed class Lib3103() : LibSharedCookieThroughProxy(3103)
{
    /// <inheritdoc/>
    public override string SiteUrl => "http://localhost/";

    /// <inheritdoc/>
    public override string CookieLine => "Set-Cookie: c1=v1; domain=localhost";
}
