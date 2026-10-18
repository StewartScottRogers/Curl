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
/// <param name="createHandle">Makes an empty URL handle, as <c>curl_url</c> does.</param>
public sealed class McpUrlTool(Func<ICurlUrlHandle> createHandle)
{
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
        CurlUrlCode code = handle.Set(CurlUrlPart.Url, url, urlFlags);
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
            CurlUrlCode set = values[index] is null ? CurlUrlCode.Ok : handle.Set(part, values[index], urlFlags);
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

    private static bool IsNotSet(CurlUrlCode code) => code is >= CurlUrlCode.NoScheme and <= CurlUrlCode.NoZoneId;

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
