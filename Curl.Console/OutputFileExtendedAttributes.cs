namespace Curl.Console;

/// <summary>
/// The extended attributes curl 8.21.0's <c>--xattr</c> stores on a transfer's output file, in the
/// order its <c>fwrite_xattr</c> (<c>src/tool_xattr.c</c>) writes them: <c>user.creator</c> set to
/// <c>curl</c>, <c>user.xdg.referrer.url</c> when a <c>Referer</c> was sent, <c>user.mime_type</c>
/// when the reply had a <c>Content-Type</c>, and <c>user.xdg.origin.url</c>, the URL without its
/// user name and password (ADR-0320).
/// </summary>
/// <remarks>
/// Measured with curl 8.18.0 (Ubuntu, OpenSSL build; <c>tool_xattr.c</c> is unchanged to 8.21.0) on
/// 2026-10-01: <c>--xattr -e http://ref.example/ -o out2.txt "http://u:p@127.0.0.1:18653/a?b#frag"</c>
/// against a reply with <c>Content-Type: text/plain; charset=utf-8</c> left exactly these four on
/// the file, the origin URL as <c>http://127.0.0.1:18653/a?b#frag</c> (BL-651 Notes).
/// </remarks>
internal static class OutputFileExtendedAttributes
{
    /// <summary>The attribute naming the program that wrote the file.</summary>
    internal const string CreatorName = "user.creator";

    /// <summary>The value curl stores in <see cref="CreatorName" />.</summary>
    internal const string CreatorValue = "curl";

    /// <summary>The attribute holding the <c>Referer</c> the transfer sent.</summary>
    internal const string ReferrerUrlName = "user.xdg.referrer.url";

    /// <summary>The attribute holding the reply's <c>Content-Type</c>.</summary>
    internal const string MimeTypeName = "user.mime_type";

    /// <summary>The attribute holding the URL transferred, without its credentials.</summary>
    internal const string OriginUrlName = "user.xdg.origin.url";

    /// <summary>
    /// Lists the attributes for one transfer, in the order curl writes them.
    /// </summary>
    /// <param name="originUrl">The URL as <c>%{url_effective}</c> normalises it, credentials and all.</param>
    /// <param name="referer">The <c>Referer</c> the last request sent, or <see langword="null" />.</param>
    /// <param name="contentType">The reply's <c>Content-Type</c>, or <see langword="null" />.</param>
    /// <returns>The name and value of each attribute.</returns>
    internal static IReadOnlyList<KeyValuePair<string, string>> For(string originUrl, string? referer, string? contentType)
    {
        List<KeyValuePair<string, string>> attributes = [new(CreatorName, CreatorValue)];
        if (referer is not null)
        {
            attributes.Add(new(ReferrerUrlName, referer));
        }

        if (contentType is not null)
        {
            attributes.Add(new(MimeTypeName, contentType));
        }

        attributes.Add(new(OriginUrlName, WithoutCredentials(originUrl)));
        return attributes;
    }

    /// <summary>
    /// Writes <paramref name="attributes" /> to <paramref name="path" /> in order, stopping at the
    /// first that fails, as curl's loop does.
    /// </summary>
    /// <param name="writer">Sets each attribute.</param>
    /// <param name="path">The output file.</param>
    /// <param name="attributes">The attributes, from <see cref="For" />.</param>
    /// <returns>
    /// <see langword="null" /> when every attribute was set; otherwise curl's warning line,
    /// <c>Warning: Error setting extended attributes on '&lt;file&gt;': &lt;strerror&gt;</c>.
    /// </returns>
    internal static string? Write(IExtendedAttributeWriter writer, string path, IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            if (!writer.TryWrite(path, attribute.Key, attribute.Value, out string errorText))
            {
                return $"Warning: Error setting extended attributes on '{path}': {errorText}";
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the user name and password from <paramref name="url" />'s authority, as curl's
    /// <c>stripcredentials</c> does by clearing both parts of its parsed URL.
    /// </summary>
    /// <param name="url">A URL of the form <c>scheme://authority/path</c>.</param>
    /// <returns>The URL with everything up to the authority's last <c>@</c> removed from it.</returns>
    internal static string WithoutCredentials(string url)
    {
        const string SchemeEnd = "://";
        int authorityStart = url.IndexOf(SchemeEnd, StringComparison.Ordinal);
        if (authorityStart < 0)
        {
            return url;
        }

        authorityStart += SchemeEnd.Length;
        int authorityEnd = url.IndexOfAny(['/', '?', '#'], authorityStart);
        int authorityLength = (authorityEnd < 0 ? url.Length : authorityEnd) - authorityStart;
        int at = url.LastIndexOf('@', authorityStart + authorityLength - 1, authorityLength);
        return at < 0 ? url : url[..authorityStart] + url[(at + 1)..];
    }
}
