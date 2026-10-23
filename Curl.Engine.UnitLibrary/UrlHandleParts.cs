using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The parts a URL handle holds, as upstream's <c>struct Curl_URL</c> in <c>lib/urlapi.c</c> holds
/// them: each one <see langword="null" /> when it is not set (ADR-0601).
/// </summary>
internal sealed record UrlHandleParts
{
    /// <summary>A new handle's parts: none set (<c>curl_url</c>).</summary>
    public static readonly UrlHandleParts Empty = new();

    /// <summary>Gets the scheme, in lower case when parsed and in the case given when set, or <see langword="null" />.</summary>
    public string? Scheme { get; init; }

    /// <summary>Gets a value indicating whether the scheme was guessed from the host (<c>CURLU_GUESS_SCHEME</c>).</summary>
    public bool SchemeGuessed { get; init; }

    /// <summary>Gets the user name as written, still percent-encoded, or <see langword="null" />.</summary>
    public string? User { get; init; }

    /// <summary>Gets the password as written, or <see langword="null" />.</summary>
    public string? Password { get; init; }

    /// <summary>Gets the login options as written, or <see langword="null" />.</summary>
    public string? Options { get; init; }

    /// <summary>Gets the host: a name percent-decoded, or an IPv6 address in brackets without its zone id; or <see langword="null" />.</summary>
    public string? Host { get; init; }

    /// <summary>Gets the IPv6 zone id, or <see langword="null" />.</summary>
    public string? ZoneId { get; init; }

    /// <summary>Gets the port written in the URL, or <see langword="null" /> when none was.</summary>
    public int? Port { get; init; }

    /// <summary>Gets the path, or <see langword="null" /> when none is set.</summary>
    public string? Path { get; init; }

    /// <summary>Gets the query without its <c>?</c>: empty for a bare <c>?</c>, <see langword="null" /> with none.</summary>
    public string? Query { get; init; }

    /// <summary>Gets the fragment without its <c>#</c>: empty for a bare <c>#</c>, <see langword="null" /> with none.</summary>
    public string? Fragment { get; init; }

    /// <summary>
    /// Gets a value indicating whether an empty <see cref="Fragment" /> was set rather than parsed: upstream
    /// shows a set empty fragment as a bare <c>#</c> without <c>CURLU_GET_EMPTY</c> (BL-3197).
    /// </summary>
    public bool EmptyFragmentSet { get; init; }

    /// <summary>The parts of a URL <see cref="CurlUrl" /> parsed.</summary>
    /// <param name="url">The parsed URL.</param>
    /// <returns>Its parts; the scheme counts as guessed when the text does not start with it.</returns>
    public static UrlHandleParts Of(CurlUrl url) => new()
    {
        Scheme = url.Scheme,
        SchemeGuessed = IsSchemeGuessed(url),
        User = url.User,
        Password = url.Password,
        Options = url.Options,
        Host = url.Scheme == "file" ? null : url.Host,
        ZoneId = url.ZoneId,
        Port = WrittenPortOf(url),
        Path = url.AbsolutePath,
        Query = url.Query,
        Fragment = url.Fragment,
    };

    /// <summary>
    /// The default port of <paramref name="scheme" /> as Curl's URL parser knows it, or
    /// <see langword="null" /> for a scheme with none (<c>file</c>, or one curl does not know).
    /// </summary>
    /// <param name="scheme">The scheme, in any case.</param>
    /// <returns>The port, or <see langword="null" />.</returns>
    public static int? DefaultPortOf(string scheme) =>
        CurlUrl.TryParse(scheme.ToLowerInvariant() + "://localhost/", pathAsIs: true, out CurlUrl? probe) && probe.Port >= 0
            ? probe.Port
            : null;

    /// <summary>
    /// Whether libcurl supports <paramref name="scheme" />: <c>file</c> or one with a default port in
    /// Curl's URL parser, as <c>Curl_get_scheme_handler</c> finds one.
    /// </summary>
    /// <param name="scheme">The scheme, in any case.</param>
    /// <returns><see langword="true" /> when the scheme is supported.</returns>
    public static bool IsSupportedScheme(string scheme) => string.Equals(scheme, "file", StringComparison.OrdinalIgnoreCase) || DefaultPortOf(scheme) is not null;

    /// <summary>
    /// These parts with every <c>%20</c> outside the host turned back into the space it stood for, as a
    /// URL parsed under <c>CURLU_ALLOW_SPACE</c> keeps its spaces.
    /// </summary>
    /// <returns>The parts with their spaces.</returns>
    public UrlHandleParts WithSpaces() => this with
    {
        User = Spaced(User),
        Password = Spaced(Password),
        Options = Spaced(Options),
        Path = Spaced(Path),
        Query = Spaced(Query),
        Fragment = Spaced(Fragment),
    };

    /// <summary>
    /// These parts with the path, query and fragment encoded as upstream's <c>parseurl</c> encodes them
    /// under <c>CURLU_URLENCODE</c> (<c>urlencode_str</c>, <see cref="UrlPartText.Encode" />): a space
    /// as <c>%20</c>, or <c>+</c> in the query, and a control or non-ASCII byte as <c>%XX</c>.
    /// </summary>
    /// <returns>The encoded parts.</returns>
    public UrlHandleParts UrlEncoded() => this with
    {
        Path = Encoded(Path, UrlEncodeQuery.No),
        Query = Encoded(Query, UrlEncodeQuery.Yes),
        Fragment = Encoded(Fragment, UrlEncodeQuery.No),
    };

    private static string? Encoded(string? text, UrlEncodeQuery query) =>
        text is null ? null : UrlPartText.UrlEncodeString(text, relative: true, query);

    private static string? Spaced(string? text) => text?.Replace("%20", " ", StringComparison.Ordinal);

    private static bool IsSchemeGuessed(CurlUrl url) =>
        !url.OriginalString.StartsWith(url.Scheme + ":/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The port written in <paramref name="url" />: <see cref="CurlUrl.Port" /> when it is not the
    /// default, or when the default was written out; upstream stores it, without leading zeros.
    /// </summary>
    private static int? WrittenPortOf(CurlUrl url)
    {
        if (url.Port < 0)
        {
            return null;
        }

        return !url.IsDefaultPort || AuthorityHasPort(url) ? url.Port : null;
    }

    /// <summary>
    /// Whether the authority of <paramref name="url" />'s text ends in <c>:</c> and digits. Only
    /// asked of a URL curl accepted with a port the scheme defaults to.
    /// </summary>
    private static bool AuthorityHasPort(CurlUrl url)
    {
        string text = IsSchemeGuessed(url)
            ? url.OriginalString
            : url.OriginalString[(url.Scheme.Length + 1)..].TrimStart('/');
        int end = text.AsSpan().IndexOfAny('/', '?', '#');
        string authority = end < 0 ? text : text[..end];
        string hostAndPort = authority[(authority.IndexOf('@') + 1)..];
        int colon = hostAndPort.StartsWith('[') ? hostAndPort.IndexOf(']') + 1 : hostAndPort.IndexOf(':');

        return colon > 0 && colon < hostAndPort.Length - 1;
    }
}
