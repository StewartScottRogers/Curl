using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1560.c</c>'s checks over <see cref="CurlUrlHandle"/>s from one library: <see cref="CheckParts"/>,
/// <see cref="CheckUrl"/> and <see cref="UpdateUrl"/> are its helpers, and each check group answers its
/// <c>error</c> count, stopping at the first row that fails as upstream's <c>&amp;&amp; !error</c> loops do.
/// Mismatches go to <paramref name="error"/> in upstream's words, with <c>lib1560.c</c>'s line numbers.
/// </summary>
/// <param name="library">The library each handle comes from.</param>
/// <param name="error">The program's stderr.</param>
/// <param name="output">The program's stdout, which <see cref="Huge"/> and <see cref="UrlDup"/> write to.</param>
/// <param name="isWindows">Whether upstream's <c>_WIN32</c> rows run.</param>
/// <param name="hasUtf8">Whether <c>CURL_TEST_HAVE_CODESET_UTF8</c> is set, which runs the <c>CURLU_PUNYCODE</c> and <c>CURLU_PUNY2IDN</c> rows.</param>
public sealed partial class Lib1560Checks(CurlLibrary library, TextWriter error, TextWriter output, bool isWindows, bool hasUtf8)
{
    private static readonly (CurlUrlPart Part, string Name)[] PartNames =
    [
        (CurlUrlPart.Url, "url"),
        (CurlUrlPart.Scheme, "scheme"),
        (CurlUrlPart.User, "user"),
        (CurlUrlPart.Password, "password"),
        (CurlUrlPart.Options, "options"),
        (CurlUrlPart.Host, "host"),
        (CurlUrlPart.Port, "port"),
        (CurlUrlPart.Path, "path"),
        (CurlUrlPart.Query, "query"),
        (CurlUrlPart.Fragment, "fragment"),
        (CurlUrlPart.ZoneId, "zoneid"),
    ];

    /// <summary><c>part2id</c>'s answer for a name it does not know.</summary>
    public const CurlUrlPart UnknownPart = (CurlUrlPart)9999;

    /// <summary>
    /// <c>checkparts</c>: every part but the URL read with <paramref name="getFlags"/> into one line, each part's
    /// text (an IPv6 host followed by its zone) or its code in brackets, joined by <c> | </c>.
    /// </summary>
    /// <param name="url">The handle.</param>
    /// <param name="input">What the mismatch line names as <c>in</c>.</param>
    /// <param name="wanted">The line wanted.</param>
    /// <param name="getFlags">The flags each part is read with.</param>
    /// <returns>1 when the line differs (written to stderr), else 0.</returns>
    public int CheckParts(CurlUrlHandle url, string? input, string wanted, CurlUrlFlags getFlags)
    {
        ArgumentNullException.ThrowIfNull(url);
        var got = new StringBuilder();
        foreach ((CurlUrlPart part, _) in PartNames.AsSpan(1, 9))
        {
            got.Append(got.Length > 0 ? " | " : string.Empty);
            got.Append(PartText(url, part, getFlags));
        }

        if (got.ToString() == wanted)
        {
            return 0;
        }

        error.Write($"in: {Text(input)}\nwanted: {wanted}\ngot:    {got}\n");
        return 1;
    }

    /// <summary><c>checkurl</c>: compares the URL read back with the one wanted.</summary>
    /// <param name="original">The URL first set, which the mismatch lines name.</param>
    /// <param name="url">The URL read back.</param>
    /// <param name="wanted">The URL wanted.</param>
    /// <returns>1 when they differ (written to stderr), else 0.</returns>
    public int CheckUrl(string? original, string? url, string wanted)
    {
        if (url == wanted)
        {
            return 0;
        }

        error.Write($"Org:    {Text(original)}\nWanted: {wanted}\nGot   : {Text(url)}\n");
        return 1;
    }

    /// <summary>
    /// <c>updateurl</c>: sets each <c>part=value</c> command of <paramref name="commands"/> that ends with a comma,
    /// <c>NULL</c> clearing the part and <c>""</c> setting it empty; a command with no name or no value is skipped.
    /// </summary>
    /// <param name="url">The handle.</param>
    /// <param name="commands">The commands, each ending with a comma.</param>
    /// <param name="setFlags">The flags each part is set with.</param>
    /// <returns>The first failing set's code, or <see cref="CurlUrlCode.Ok"/>.</returns>
    public CurlUrlCode UpdateUrl(CurlUrlHandle url, string commands, CurlUrlFlags setFlags)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(commands);
        string[] segments = commands.Split(',');
        foreach (string command in segments.AsSpan(0, segments.Length - 1))
        {
            int equals = command.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0 || equals == command.Length - 1)
            {
                continue;
            }

            CurlUrlCode code = SetPart(url, command[..equals], command[(equals + 1)..], setFlags);
            if (code != CurlUrlCode.Ok)
            {
                return code;
            }
        }

        return CurlUrlCode.Ok;
    }

    /// <summary><c>part2id</c>: the part a command names, or <see cref="UnknownPart"/>.</summary>
    /// <param name="name">The part's name, such as <c>host</c>.</param>
    /// <returns>The part.</returns>
    public static CurlUrlPart PartId(string name)
    {
        foreach ((CurlUrlPart part, string partName) in PartNames)
        {
            if (partName == name)
            {
                return part;
            }
        }

        return UnknownPart;
    }

    private CurlUrlCode SetPart(CurlUrlHandle url, string name, string value, CurlUrlFlags setFlags)
    {
        CurlUrlPart part = PartId(name);
        if (part > CurlUrlPart.ZoneId)
        {
            error.Write($"UNKNOWN part '{name}'\n");
        }

        string? text = value switch
        {
            "NULL" => null,
            "\"\"" => string.Empty,
            _ => value,
        };
        return url.Set(part, text, setFlags);
    }

    private static string PartText(CurlUrlHandle url, CurlUrlPart part, CurlUrlFlags getFlags)
    {
        CurlUrlCode code = url.Get(part, getFlags, out string? value);
        if (code != CurlUrlCode.Ok || value is null)
        {
            return $"[{(int)code}]";
        }

        string? zone = null;
        if (part == CurlUrlPart.Host && value.StartsWith('['))
        {
            url.Get(CurlUrlPart.ZoneId, getFlags, out zone);
        }

        return zone is null ? value : $"{value} {zone}";
    }

    private bool Runs(CurlUrlFlags getFlags) =>
        (getFlags != CurlUrlFlags.Punycode && getFlags != CurlUrlFlags.PunycodeToIdn) || hasUtf8;

    private static string Text(string? value) => value ?? "(null)";

    private static string Describe(CurlUrlCode code) => $"{(int)code} ({CurlUrlHandle.ErrorText(code)})";

    private static int UntilError<T>(IEnumerable<T> rows, Func<T, int> check)
    {
        foreach (T row in rows)
        {
            int errors = check(row);
            if (errors > 0)
            {
                return errors;
            }
        }

        return 0;
    }
}
