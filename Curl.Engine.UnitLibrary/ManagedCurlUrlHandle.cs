using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The managed engine's <see cref="ICurlUrlHandle" /> (<c>CURLU</c>, ADR-0601): the parts of one URL,
/// parsed by Curl's <see cref="CurlUrl" /> parser and read back as upstream curl 8.22.0's
/// <c>curl_url_get</c> reads them, every part and every get flag.
/// </summary>
/// <remarks>
/// <see cref="Set" /> parses a whole URL (<see cref="CurlUrlPart.Url" />), a relative one against the
/// URL held, or sets or clears one part with <see cref="UrlPartSetter" />, as <c>curl_url_set</c> does
/// (ADR-0602); <see cref="Duplicate" /> is <c>curl_url_dup</c>.
/// </remarks>
internal sealed class ManagedCurlUrlHandle : ICurlUrlHandle
{
    private const string FileScheme = "file";

    /// <summary>The longest string a set accepts for any part (upstream's <c>CURL_MAX_INPUT_LENGTH</c>).</summary>
    private const int MaximumInputLength = 8_000_000;

    private const string DefaultScheme = "https";

    /// <summary>The <see cref="CurlUrlCode" /> for each <see cref="CurlUrlRejection" />, by its number.</summary>
    private static readonly CurlUrlCode[] RejectionCodes =
    [
        CurlUrlCode.Ok,
        CurlUrlCode.MalformedInput,
        CurlUrlCode.BadSlashes,
        CurlUrlCode.NoHost,
        CurlUrlCode.BadPortNumber,
        CurlUrlCode.BadIPv6,
        CurlUrlCode.BadHostname,
        CurlUrlCode.BadFileUrl,
        CurlUrlCode.UserNotAllowed,
        CurlUrlCode.Backslash,
    ];

    /// <summary>The reader of each <see cref="CurlUrlPart" />, by its number.</summary>
    private static readonly Func<UrlHandleParts, CurlUrlFlags, UrlPartResult>[] Readers =
    [
        ReadUrl,
        ReadScheme,
        (parts, flags) => Transformed(parts.User, CurlUrlCode.NoUser, flags, CurlUrlPart.User),
        (parts, flags) => Transformed(parts.Password, CurlUrlCode.NoPassword, flags, CurlUrlPart.Password),
        (parts, flags) => Transformed(parts.Options, CurlUrlCode.NoOptions, flags, CurlUrlPart.Options),
        (parts, flags) => Transformed(parts.Host, CurlUrlCode.NoHost, flags, CurlUrlPart.Host),
        (parts, flags) => UrlPartResult.FoundOr(PortText(parts.Port, parts.Scheme, flags), CurlUrlCode.NoPort),
        (parts, flags) => Transformed(parts.Path ?? "/", CurlUrlCode.Ok, flags, CurlUrlPart.Path),
        ReadQuery,
        ReadFragment,
        (parts, flags) => Transformed(parts.ZoneId, CurlUrlCode.NoZoneId, flags, CurlUrlPart.ZoneId),
    ];

    private UrlHandleParts parts;

    /// <summary>A handle holding no part, as <c>curl_url</c> gives.</summary>
    public ManagedCurlUrlHandle()
        : this(UrlHandleParts.Empty)
    {
    }

    /// <summary>A handle holding <paramref name="parts" />.</summary>
    /// <param name="parts">The parts.</param>
    internal ManagedCurlUrlHandle(UrlHandleParts parts) => this.parts = parts;

    /// <inheritdoc />
    public CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value)
    {
        UrlPartResult result = (uint)part < (uint)Readers.Length
            ? Readers[(int)part](parts, flags)
            : UrlPartResult.Failed(CurlUrlCode.UnknownPart);
        value = result.Value;

        return result.Code;
    }

    /// <inheritdoc />
    public CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags)
    {
        if ((uint)part > (uint)CurlUrlPart.ZoneId)
        {
            return CurlUrlCode.UnknownPart;
        }

        if (value?.Length > MaximumInputLength)
        {
            return CurlUrlCode.MalformedInput;
        }

        if (value is null)
        {
            return Keep(UrlPartSetter.Cleared(parts, part));
        }

        return part == CurlUrlPart.Url ? SetUrl(value, flags) : Keep(UrlPartSetter.Set(parts, part, value, flags));
    }

    /// <inheritdoc />
    public ICurlUrlHandle Duplicate() => new ManagedCurlUrlHandle(parts);

    /// <summary><c>curl_url_cleanup</c>: the handle holds nothing to release.</summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// The port a get gives: the one written, hidden under <c>CURLU_NO_DEFAULT_PORT</c> when it is the
    /// scheme's default; with none written, the scheme's default under <c>CURLU_DEFAULT_PORT</c>.
    /// </summary>
    private static string? PortText(int? written, string? scheme, CurlUrlFlags flags)
    {
        int? defaultPort = scheme is null ? null : UrlHandleParts.DefaultPortOf(scheme);
        int? shown = written is null ? DefaultPortShown(defaultPort, flags) : WrittenPortShown(written.Value, defaultPort, flags);

        return shown?.ToString(CultureInfo.InvariantCulture);
    }

    private static int? DefaultPortShown(int? defaultPort, CurlUrlFlags flags) =>
        flags.HasFlag(CurlUrlFlags.DefaultPort) ? defaultPort : null;

    private static int? WrittenPortShown(int written, int? defaultPort, CurlUrlFlags flags) =>
        flags.HasFlag(CurlUrlFlags.NoDefaultPort) && defaultPort == written ? null : written;

    private static UrlPartResult Transformed(string? value, CurlUrlCode missing, CurlUrlFlags flags, CurlUrlPart part) =>
        value is null ? UrlPartResult.Failed(missing) : UrlPartText.Transform(value, flags, part);

    private static UrlPartResult ReadScheme(UrlHandleParts parts, CurlUrlFlags flags) =>
        flags.HasFlag(CurlUrlFlags.NoGuessScheme) && parts.SchemeGuessed
            ? UrlPartResult.Failed(CurlUrlCode.NoScheme)
            : UrlPartResult.FoundOr(parts.Scheme, CurlUrlCode.NoScheme);

    /// <summary>A bare <c>?</c> gives no query unless <c>CURLU_GET_EMPTY</c> asks for the empty one.</summary>
    private static UrlPartResult ReadQuery(UrlHandleParts parts, CurlUrlFlags flags) =>
        Transformed(Shown(parts.Query, flags), CurlUrlCode.NoQuery, flags, CurlUrlPart.Query);

    /// <summary>A bare <c>#</c> gives no fragment unless <c>CURLU_GET_EMPTY</c> asks for the empty one.</summary>
    private static UrlPartResult ReadFragment(UrlHandleParts parts, CurlUrlFlags flags) =>
        Transformed(Shown(parts.Fragment, flags), CurlUrlCode.NoFragment, flags, CurlUrlPart.Fragment);

    private static string? Shown(string? value, CurlUrlFlags flags) =>
        value is { Length: 0 } && !flags.HasFlag(CurlUrlFlags.GetEmpty) ? null : value;

    /// <summary>
    /// The whole URL as upstream's <c>curl_url_get(CURLUPART_URL)</c> builds it: a <c>file</c> URL
    /// from its path, query and fragment; any other from its scheme (<c>https</c> under
    /// <c>CURLU_DEFAULT_SCHEME</c> when none is set; left out under <c>CURLU_NO_GUESS_SCHEME</c> when
    /// guessed), login, host, port, path, query and fragment.
    /// </summary>
    private static UrlPartResult ReadUrl(UrlHandleParts parts, CurlUrlFlags flags)
    {
        string tail = TailOf(parts, flags);
        if (parts.Scheme == FileScheme)
        {
            return UrlPartResult.Found("file://" + tail);
        }

        if (parts.Host is null)
        {
            return UrlPartResult.Failed(CurlUrlCode.NoHost);
        }

        string? scheme = SchemeForUrl(parts, flags);

        return scheme is null ? UrlPartResult.Failed(CurlUrlCode.NoScheme) : NetworkUrl(parts, flags, scheme, tail);
    }

    /// <summary>The path, then the query and fragment when <see cref="Shown" /> shows them.</summary>
    private static string TailOf(UrlHandleParts parts, CurlUrlFlags flags) =>
        (parts.Path ?? "/") + Suffix('?', Shown(parts.Query, flags)) + Suffix('#', Shown(parts.Fragment, flags));

    /// <summary>The scheme set, else <c>https</c> under <c>CURLU_DEFAULT_SCHEME</c>, else <see langword="null" />.</summary>
    private static string? SchemeForUrl(UrlHandleParts parts, CurlUrlFlags flags) =>
        parts.Scheme ?? (flags.HasFlag(CurlUrlFlags.DefaultScheme) ? DefaultScheme : null);

    /// <summary>A URL that is not a <c>file</c> one, from its scheme, login, host and port and <paramref name="tail" />.</summary>
    private static UrlPartResult NetworkUrl(UrlHandleParts parts, CurlUrlFlags flags, string scheme, string tail)
    {
        UrlPartResult host = HostInUrl(parts, flags);
        if (host.Code != CurlUrlCode.Ok)
        {
            return host;
        }

        string schemeText = flags.HasFlag(CurlUrlFlags.NoGuessScheme) && parts.SchemeGuessed ? string.Empty : scheme + "://";

        return UrlPartResult.Found(
            schemeText + Login(parts) + host.Value + Suffix(':', PortText(parts.Port, scheme, flags)) + tail);
    }

    /// <summary>
    /// The host as the whole URL writes it: an IPv6 address with <c>%25</c> and its zone id, else the
    /// name escaped under <c>CURLU_URLENCODE</c>, else converted as <see cref="UrlPartText.Punycode" /> does.
    /// </summary>
    private static UrlPartResult HostInUrl(UrlHandleParts parts, CurlUrlFlags flags)
    {
        string host = parts.Host!;
        if (host.StartsWith('['))
        {
            return UrlPartResult.Found(parts.ZoneId is null ? host : $"{host[..^1]}%25{parts.ZoneId}]");
        }

        return flags.HasFlag(CurlUrlFlags.UrlEncode)
            ? UrlPartResult.Found(UrlPartText.Escape(host))
            : UrlPartText.Punycode(host, flags);
    }

    private static string Login(UrlHandleParts parts)
    {
        bool any = parts.User is not null || parts.Password is not null || parts.Options is not null;

        return any ? parts.User + Suffix(':', parts.Password) + Suffix(';', parts.Options) + "@" : string.Empty;
    }

    private static string Suffix(char separator, string? value) => value is null ? string.Empty : separator + value;

    /// <summary>
    /// A whole URL: empty is
    /// <see cref="CurlUrlCode.MalformedInput" />; a relative one is resolved against the URL held, when
    /// the handle holds one (<see cref="UrlRelativeReference" />); then it is parsed.
    /// </summary>
    private CurlUrlCode SetUrl(string value, CurlUrlFlags flags)
    {
        if (value.Length == 0)
        {
            return CurlUrlCode.MalformedInput;
        }

        if (UrlRelativeReference.IsAbsolute(value, flags))
        {
            return IsAcceptedScheme(value, flags) ? Parse(value, flags) : CurlUrlCode.UnsupportedScheme;
        }

        return Get(CurlUrlPart.Url, flags & ~CurlUrlFlags.NoGuessScheme, out string? current) == CurlUrlCode.Ok
            ? Parse(UrlRelativeReference.Resolve(current!, value), flags)
            : ParseWithoutScheme(value, flags);
    }

    /// <summary>
    /// A URL that does not start with a scheme and a colon (<c>Curl_is_absolute_url</c> found none: it
    /// starts with no letter, a character no scheme holds ends it early, or it runs past
    /// <c>MAX_SCHEME_LEN</c>) is <see cref="CurlUrlCode.BadScheme" /> unless <c>CURLU_GUESS_SCHEME</c> or
    /// <c>CURLU_DEFAULT_SCHEME</c> is set, as upstream's <c>parseurl</c> answers before reading a host.
    /// </summary>
    private CurlUrlCode ParseWithoutScheme(string value, CurlUrlFlags flags) =>
        (flags & (CurlUrlFlags.GuessScheme | CurlUrlFlags.DefaultScheme)) == 0
            ? CurlUrlCode.BadScheme
            : Parse(value, flags);

    /// <summary>
    /// Parses a whole URL with <see cref="CurlUrl" /> under <c>CURLU_PATH_AS_IS</c> and
    /// <c>CURLU_DISALLOW_USER</c>, its spaces read as <c>%20</c> under <c>CURLU_ALLOW_SPACE</c>, its
    /// path, query and fragment encoded under <c>CURLU_URLENCODE</c> (<see cref="UrlHandleParts.UrlEncoded" />),
    /// each <see cref="CurlUrlRejection" /> mapped to its <see cref="CurlUrlCode" />.
    /// </summary>
    private CurlUrlCode Parse(string value, CurlUrlFlags flags)
    {
        string text = flags.HasFlag(CurlUrlFlags.AllowSpace) ? value.Replace(" ", "%20", StringComparison.Ordinal) : value;
        if (!TryParseUnder(text, flags, out CurlUrl? url, out CurlUrlRejection rejection))
        {
            return RejectionCodes[(int)rejection];
        }

        UrlHandleParts newParts = UrlHandleParts.Of(url!);
        bool spaced = !ReferenceEquals(text, value) && !value.Contains("%20", StringComparison.Ordinal);
        newParts = spaced ? newParts.WithSpaces() : newParts;

        return Accept(value, flags, flags.HasFlag(CurlUrlFlags.UrlEncode) ? newParts.UrlEncoded() : newParts);
    }

    /// <summary>
    /// Parses <paramref name="text" /> under <c>CURLU_PATH_AS_IS</c>, <c>CURLU_DISALLOW_USER</c> and
    /// <c>CURLU_NO_AUTHORITY</c> as libcurl's own parser, a <c>file</c> URL keeping its backslashes and,
    /// without <c>CURLU_GUESS_SCHEME</c> or <c>CURLU_DEFAULT_SCHEME</c>, needing no <c>/</c> after
    /// <c>file:</c> (<see cref="CurlUrl.TryParseForUrlApi(string, bool, CurlUrlApiFlags, out CurlUrl, out CurlUrlRejection)" />).
    /// </summary>
    private static bool TryParseUnder(string text, CurlUrlFlags flags, out CurlUrl? url, out CurlUrlRejection rejection) =>
        CurlUrl.TryParseForUrlApi(
            text,
            flags.HasFlag(CurlUrlFlags.PathAsIs),
            new CurlUrlApiFlags(
                flags.HasFlag(CurlUrlFlags.DisallowUser),
                flags.HasFlag(CurlUrlFlags.NoAuthority),
                (flags & (CurlUrlFlags.GuessScheme | CurlUrlFlags.DefaultScheme)) != 0),
            out url,
            out rejection);

    /// <summary>
    /// A parsed URL with no scheme: <c>https</c> under <c>CURLU_DEFAULT_SCHEME</c>, the guessed one under
    /// <c>CURLU_GUESS_SCHEME</c>, else <see cref="CurlUrlCode.BadSlashes" />, as upstream's <c>parseurl</c>
    /// answers text whose accepted scheme (<see cref="IsAcceptedScheme" />) has no <c>/</c> after its colon
    /// (<c>http:80/x</c>); with one libcurl does not support, <see cref="CurlUrlCode.UnsupportedScheme" /> unless
    /// <c>CURLU_NON_SUPPORT_SCHEME</c>.
    /// </summary>
    private CurlUrlCode Accept(string value, CurlUrlFlags flags, UrlHandleParts newParts)
    {
        if (!newParts.SchemeGuessed)
        {
            bool supported = flags.HasFlag(CurlUrlFlags.NonSupportScheme) || UrlHandleParts.IsSupportedScheme(newParts.Scheme!);

            return supported ? Keep(newParts) : CurlUrlCode.UnsupportedScheme;
        }

        if (flags.HasFlag(CurlUrlFlags.DefaultScheme))
        {
            return Parse(DefaultScheme + "://" + value, flags & ~CurlUrlFlags.DefaultScheme);
        }

        return flags.HasFlag(CurlUrlFlags.GuessScheme) ? Keep(newParts) : CurlUrlCode.BadSlashes;
    }

    /// <summary>
    /// Whether the scheme an absolute URL starts with, up to its first colon, is one libcurl supports or
    /// <c>CURLU_NON_SUPPORT_SCHEME</c> is set: upstream's <c>parseurl</c> answers
    /// <see cref="CurlUrlCode.UnsupportedScheme" /> otherwise, before it reads anything after the colon,
    /// so <c>about:config</c>, <c>mailto:x@y</c> and <c>example.com:80/x</c> fail there.
    /// </summary>
    private static bool IsAcceptedScheme(string value, CurlUrlFlags flags) =>
        flags.HasFlag(CurlUrlFlags.NonSupportScheme)
        || UrlHandleParts.IsSupportedScheme(value[..value.IndexOf(':', StringComparison.Ordinal)].ToLowerInvariant());

    private CurlUrlCode Keep(UrlHandleParts newParts)
    {
        parts = newParts;

        return CurlUrlCode.Ok;
    }

    private CurlUrlCode Keep(UrlSetResult result)
    {
        parts = result.Parts ?? parts;

        return result.Code;
    }
}
