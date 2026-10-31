namespace Curl.Extension.WebDavTools.Propfind;

/// <summary>
/// The parts of a WebDAV <c>PROPFIND</c> request (RFC 4918 section 9.1) that ask for the four properties a
/// listing needs: <c>resourcetype</c>, <c>getcontentlength</c>, <c>getlastmodified</c> and <c>getetag</c>.
/// </summary>
public static class WebDavPropfindRequest
{
    /// <summary>The request method.</summary>
    public const string Method = "PROPFIND";

    /// <summary>The name of the header that carries the depth (RFC 4918 section 10.2).</summary>
    public const string DepthHeaderName = "Depth";

    /// <summary>The media type of <see cref="Body"/>.</summary>
    public const string ContentType = "application/xml; charset=utf-8";

    /// <summary>The request body: a <c>DAV:propfind</c> whose <c>prop</c> names the four properties.</summary>
    public const string Body =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<D:propfind xmlns:D=\"DAV:\"><D:prop><D:resourcetype/><D:getcontentlength/><D:getlastmodified/><D:getetag/></D:prop></D:propfind>\n";

    /// <summary>Returns the <c>Depth</c> header value for <paramref name="depth"/>: <c>0</c>, <c>1</c> or <c>infinity</c>.</summary>
    /// <param name="depth">How far below the request URL the request reaches.</param>
    /// <returns>The header value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not a defined value.</exception>
    public static string DepthHeaderValue(WebDavDepth depth) => depth switch
    {
        WebDavDepth.Zero => "0",
        WebDavDepth.One => "1",
        WebDavDepth.Infinity => "infinity",
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, "The depth is not 0, 1 or infinity."),
    };
}
