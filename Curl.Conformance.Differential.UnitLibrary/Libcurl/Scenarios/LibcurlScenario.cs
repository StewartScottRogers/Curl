using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>
/// One entry of the tester's <c>scen.json</c>: a kind letter and its inputs (ADR-0791).
/// <c>U</c> sets <see cref="Text"/> as a whole URL with <see cref="Flags"/>; <c>P</c> sets
/// <see cref="Value"/> as URL part <see cref="Part"/> with <see cref="Flags"/> on the base URL
/// <see cref="Text"/>; <c>E</c> escapes and <c>N</c> unescapes <see cref="Bytes"/>; <c>D</c> parses
/// the date <see cref="Text"/>; <c>S</c> lists the error texts and <c>V</c> the version text.
/// </summary>
/// <param name="Index">The scenario's position in the file, which starts each line it replays to.</param>
/// <param name="Kind">The kind letter: <c>U</c>, <c>P</c>, <c>E</c>, <c>N</c>, <c>D</c>, <c>S</c> or <c>V</c>.</param>
/// <param name="Text">The URL, base URL or date the kind reads, or <see langword="null"/>.</param>
/// <param name="Part">The <c>CURLUPART_*</c> number a <c>P</c> scenario sets.</param>
/// <param name="Flags">The <c>CURLU_*</c> flags a <c>U</c> or <c>P</c> scenario sets with.</param>
/// <param name="Value">The part value a <c>P</c> scenario sets, <see langword="null"/> to clear it.</param>
/// <param name="Bytes">The bytes an <c>E</c> or <c>N</c> scenario escapes or unescapes.</param>
public sealed record LibcurlScenario(int Index, string Kind, string? Text, int Part, int Flags, string? Value, byte[] Bytes)
{
    /// <summary>The <c>CURLUPART_HOST</c> number.</summary>
    private const int HostPart = 5;

    /// <summary>
    /// Gets the reference build's fault this scenario would only show, or <see langword="null"/>:
    /// <see cref="ReferenceBuildLimitation.BrokenIdn"/> for a URL or host part with a non-ASCII host name.
    /// </summary>
    public ReferenceBuildLimitation? Limitation => NamesNonAsciiHost() ? ReferenceBuildLimitation.BrokenIdn : null;

    /// <summary>Gets whether this scenario is replayed against the reference build (it has no <see cref="Limitation"/>).</summary>
    public bool RunsAgainstReferenceBuild => Limitation is null;

    private bool NamesNonAsciiHost() => Kind switch
    {
        "U" => !Ascii.IsValid(HostOf(Text!)),
        "P" => Part == HostPart && Value is not null && !Ascii.IsValid(Value),
        _ => false,
    };

    /// <summary>Gets the host of a URL: the authority after any scheme, without any user information.</summary>
    private static string HostOf(string url)
    {
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var rest = schemeEnd < 0 ? url : url[(schemeEnd + 3)..];
        var authorityEnd = rest.IndexOfAny(['/', '?', '#']);
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        return authority[(authority.LastIndexOf('@') + 1)..];
    }
}
