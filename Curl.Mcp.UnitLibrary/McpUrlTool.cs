using System.Diagnostics.CodeAnalysis;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Mcp;

/// <summary>
/// The logic of the MCP tools <c>parse_url</c> and <c>build_url</c> (ADR-0548): each runs one
/// <see cref="ICurlUrlHandle" /> from the injected factory, libcurl's URL API. A part the handle reports
/// as not set (a <c>CURLUE_NO_*</c> code) is absent; any other <see cref="CurlUrlCode" /> but
/// <see cref="CurlUrlCode.Ok" /> is a tool error naming the code.
/// </summary>
/// <remarks>
/// A URL or <c>scheme</c> part whose scheme is one of the extended schemes is set with
/// <see cref="CurlUrlFlags.NonSupportScheme" /> added, so Curl's extended protocols parse and build with no flag as
/// curl's own do (BL-4451); with none given, only curl's own schemes are accepted without that flag.
/// </remarks>
/// <param name="createHandle">Makes an empty URL handle, as <c>curl_url</c> does.</param>
/// <param name="extendedSchemes">The schemes Curl's extensions serve, matched without regard to case.</param>
public sealed class McpUrlTool(Func<ICurlUrlHandle> createHandle, IEnumerable<string> extendedSchemes)
{
    private readonly HashSet<string> extended = new(extendedSchemes, StringComparer.OrdinalIgnoreCase);

    /// <summary>Makes the tool over curl's own schemes alone.</summary>
    /// <param name="createHandle">Makes an empty URL handle, as <c>curl_url</c> does.</param>
    public McpUrlTool(Func<ICurlUrlHandle> createHandle)
        : this(createHandle, [])
    {
    }

    /// <summary>
    /// The names <c>flags</c> accepts: every <see cref="CurlUrlFlags" /> but <see cref="CurlUrlFlags.None" />,
    /// matched without regard to case.
    /// </summary>
    public static IReadOnlyDictionary<string, CurlUrlFlags> FlagNames { get; } =
        Enum.GetValues<CurlUrlFlags>()
            .Where(flag => flag != CurlUrlFlags.None)
            .ToDictionary(flag => flag.ToString(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses <paramref name="url" /> into its parts.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="flags">The names of the flags the parse and every get use; none when <see langword="null" />.</param>
    /// <returns>The tool's result.</returns>
    public McpParseUrlToolResult Parse(string url, IReadOnlyList<string>? flags)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!TryReadFlags(flags, out CurlUrlFlags urlFlags, out McpToolFailure? failure))
        {
            return McpParseUrlToolResult.Failed(failure);
        }

        using ICurlUrlHandle handle = createHandle();
        CurlUrlCode code = handle.Set(CurlUrlPart.Url, url, urlFlags | FlagFor(SchemeOf(url)));
        if (code != CurlUrlCode.Ok)
        {
            return McpParseUrlToolResult.Failed(McpToolFailure.UrlRefused("url", code));
        }

        string?[] values = new string?[McpUrlParts.InOrder.Count];
        for (int index = 0; index < values.Length; index++)
        {
            (CurlUrlPart part, string name) = McpUrlParts.InOrder[index];
            code = handle.Get(part, urlFlags, out values[index]);
            if (code != CurlUrlCode.Ok && !IsNotSet(code))
            {
                return McpParseUrlToolResult.Failed(McpToolFailure.UrlRefused(name, code));
            }
        }

        // The URL parsed and every part was read, so rebuilding the whole URL has no failure to report.
        handle.Get(CurlUrlPart.Url, urlFlags, out string? whole);
        return new McpParseUrlToolResult(whole, McpUrlParts.FromValues(values), null);
    }

    /// <summary>Builds a URL from <paramref name="parts" />, setting only the parts that are not <see langword="null" />.</summary>
    /// <param name="parts">The parts.</param>
    /// <param name="flags">The names of the flags every set and the final get use; none when <see langword="null" />.</param>
    /// <returns>The tool's result.</returns>
    public McpBuildUrlToolResult Build(McpUrlParts parts, IReadOnlyList<string>? flags)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (!TryReadFlags(flags, out CurlUrlFlags urlFlags, out McpToolFailure? failure))
        {
            return McpBuildUrlToolResult.Failed(failure);
        }

        using ICurlUrlHandle handle = createHandle();
        string?[] values = parts.Values();
        for (int index = 0; index < values.Length; index++)
        {
            (CurlUrlPart part, string name) = McpUrlParts.InOrder[index];
            CurlUrlCode set = values[index] is null ? CurlUrlCode.Ok : handle.Set(part, values[index], urlFlags | FlagFor(parts.Scheme));
            if (set != CurlUrlCode.Ok)
            {
                return McpBuildUrlToolResult.Failed(McpToolFailure.UrlRefused(name, set));
            }
        }

        CurlUrlCode code = handle.Get(CurlUrlPart.Url, urlFlags, out string? url);
        return code == CurlUrlCode.Ok
            ? new McpBuildUrlToolResult(url, null)
            : McpBuildUrlToolResult.Failed(McpToolFailure.UrlRefused("url", code));
    }

    /// <summary>The text before a URL's first colon, its scheme when it has one.</summary>
    private static string SchemeOf(string url) => url[..Math.Max(url.IndexOf(':'), 0)];

    /// <summary><see cref="CurlUrlFlags.NonSupportScheme" /> when <paramref name="scheme" /> is an extended scheme, else none.</summary>
    private CurlUrlFlags FlagFor(string? scheme) =>
        scheme is not null && extended.Contains(scheme) ? CurlUrlFlags.NonSupportScheme : CurlUrlFlags.None;

    /// <summary>Whether <paramref name="code" /> is one of the <c>CURLUE_NO_*</c> codes, which say a part is absent rather than that anything failed.</summary>
    private static bool IsNotSet(CurlUrlCode code) => code is >= CurlUrlCode.NoScheme and <= CurlUrlCode.NoZoneId;

    /// <summary>Combines the named flags into one <see cref="CurlUrlFlags" />; <see langword="false" /> with an invalid-request error naming the first unknown one.</summary>
    private static bool TryReadFlags(IReadOnlyList<string>? names, out CurlUrlFlags flags, [NotNullWhen(false)] out McpToolFailure? failure)
    {
        flags = CurlUrlFlags.None;
        failure = null;
        IReadOnlyList<string> given = names ?? [];
        for (int index = 0; index < given.Count; index++)
        {
            if (!FlagNames.TryGetValue(given[index], out CurlUrlFlags flag))
            {
                failure = McpToolFailure.InvalidRequest(
                    $"flags[{index}]",
                    $"'{given[index]}' is not a URL flag; use one of {string.Join(", ", FlagNames.Keys)}.");
                return false;
            }

            flags |= flag;
        }

        return true;
    }
}
