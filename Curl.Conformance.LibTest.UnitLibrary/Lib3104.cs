namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3104.c</c>: <see cref="LibSharedCookieThroughProxy"/> for <c>http://example.com/</c> with a session cookie
/// in Netscape cookie-file format.
/// </summary>
public sealed class Lib3104() : LibSharedCookieThroughProxy(3104)
{
    /// <inheritdoc/>
    public override string SiteUrl => "http://example.com/";

    /// <inheritdoc/>
    public override string CookieLine => "example.com\tFALSE\t/\tFALSE\t0\tname\tvalue";
}
