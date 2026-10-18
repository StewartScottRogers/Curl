using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <content>
/// <c>lib1560.c</c>'s hand-written groups: <c>scopeid</c>, <c>get_nothing</c>, <c>clear_url</c>, <c>huge</c>,
/// <c>urldup</c>, <c>test_api_errors</c> and <c>test_scheme_guess</c>.
/// </content>
public sealed partial class Lib1560Checks
{
    /// <summary><c>huge</c>'s <c>bigpart</c> less its leading slash: 119,998 <c>a</c>s.</summary>
    public static readonly string BigPart = new('a', 120000 - 2);

    private static readonly CurlUrlPart[] HugeParts =
    [
        CurlUrlPart.Scheme, CurlUrlPart.User, CurlUrlPart.Password, CurlUrlPart.Host,
        CurlUrlPart.Path, CurlUrlPart.Query, CurlUrlPart.Fragment,
    ];

    private static readonly (CurlUrlPart Part, CurlUrlCode Wanted, int Line)[] NothingParts =
    [
        (CurlUrlPart.Scheme, CurlUrlCode.NoScheme, 2144),
        (CurlUrlPart.Host, CurlUrlCode.NoHost, 2151),
        (CurlUrlPart.User, CurlUrlCode.NoUser, 2158),
        (CurlUrlPart.Password, CurlUrlCode.NoPassword, 2165),
        (CurlUrlPart.Options, CurlUrlCode.NoOptions, 2172),
        (CurlUrlPart.Path, CurlUrlCode.Ok, 2179),
        (CurlUrlPart.Query, CurlUrlCode.NoQuery, 2187),
        (CurlUrlPart.Fragment, CurlUrlCode.NoFragment, 2194),
    ];

    private static readonly string[] DuplicatedUrls =
    [
        "http://user:pwd@[2a04:4e42:e00::347%25eth0]:80/path?query#fraggie",
        "https://example.com",
        "https://user@example.com",
        "https://user.pwd@example.com",
        "https://user.pwd@example.com:1234",
        "https://example.com:1234",
        "example.com:1234",
        "https://user.pwd@example.com:1234/path?query#frag",
    ];

    /// <summary><c>scopeid</c>: an IPv6 host with a zone set, read, replaced and given a new zone, each call wanted to succeed.</summary>
    /// <returns>The number of calls that failed.</returns>
    public int ScopeId()
    {
        using CurlUrlHandle url = library.UrlInit();
        (string What, int Line, Func<CurlUrlCode> Call)[] calls =
        [
            ("curl_url_set", 2014, () => url.Set(CurlUrlPart.Url, "https://[fe80::20c:29ff:fe9c:409b%25eth0]/hello.html", CurlUrlFlags.None)),
            ("curl_url_get CURLUPART_HOST", 2022, () => url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out _)),
            ("curl_url_set CURLUPART_HOST", 2033, () => url.Set(CurlUrlPart.Host, "[::1]", CurlUrlFlags.None)),
            ("curl_url_get CURLUPART_URL", 2041, () => url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _)),
            ("curl_url_set CURLUPART_HOST", 2052, () => url.Set(CurlUrlPart.Host, "example.com", CurlUrlFlags.None)),
            ("curl_url_get CURLUPART_URL", 2060, () => url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _)),
            ("curl_url_set CURLUPART_HOST", 2071, () => url.Set(CurlUrlPart.Host, "[fe80::20c:29ff:fe9c:409b%25eth0]", CurlUrlFlags.None)),
            ("curl_url_get CURLUPART_URL", 2079, () => url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _)),
            ("curl_url_get CURLUPART_HOST", 2090, () => url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out _)),
            ("curl_url_get CURLUPART_ZONEID", 2101, () => url.Get(CurlUrlPart.ZoneId, CurlUrlFlags.None, out _)),
            ("curl_url_set CURLUPART_ZONEID", 2112, () => url.Set(CurlUrlPart.ZoneId, "clown", CurlUrlFlags.None)),
            ("curl_url_get CURLUPART_URL", 2120, () => url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _)),
        ];
        int errors = 0;
        foreach ((string what, int line, Func<CurlUrlCode> call) in calls)
        {
            CurlUrlCode code = call();
            errors += code != CurlUrlCode.Ok ? Report($"lib1560.c:{line} {what} returned {Describe(code)}\n") : 0;
        }

        return errors;
    }

    /// <summary><c>get_nothing</c>: every part of an empty handle, each wanted to answer its <c>CURLUE_NO_*</c> code (the path <c>CURLUE_OK</c>).</summary>
    /// <returns>The number of parts that answered otherwise.</returns>
    public int GetNothing()
    {
        using CurlUrlHandle url = library.UrlInit();
        int errors = 0;
        foreach ((CurlUrlPart part, CurlUrlCode wanted, int line) in NothingParts)
        {
            errors += url.Get(part, CurlUrlFlags.None, out _) != wanted ? Report($"unexpected return code line {line}\n") : 0;
        }

        CurlUrlCode zone = url.Get(CurlUrlPart.ZoneId, CurlUrlFlags.None, out _);
        return errors + (zone != CurlUrlCode.NoZoneId ? Report($"unexpected return code {(int)zone} on line 2201\n") : 0);
    }

    /// <summary><c>clear_url</c>: each part set, the whole URL cleared, then the part read, wanted gone (the path <c>/</c>).</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.ClearUrls"/> in the program, all on one handle.</param>
    /// <returns>The errors of the first failing part, or 0.</returns>
    public int ClearUrl(IEnumerable<Lib1560ClearCase> rows)
    {
        using CurlUrlHandle url = library.UrlInit();
        return UntilError(rows, row => ClearPart(url, row));
    }

    /// <summary><c>huge</c>: URLs with one part 119,998 bytes long, the scheme wanted refused and every other part read back whole.</summary>
    /// <returns>The number of failures, each written to stdout.</returns>
    public int Huge()
    {
        using CurlUrlHandle url = library.UrlInit();
        int errors = 0;
        for (int i = 0; i < HugeParts.Length; i++)
        {
            string[] pieces = [.. Enumerable.Range(0, 7).Select(piece => piece == i ? BigPart : "c")];
            string total = $"{pieces[0]}://{pieces[1]}:{pieces[2]}@{pieces[3]}/{pieces[4]}?{pieces[5]}#{pieces[6]}";
            CurlUrlCode code = url.Set(CurlUrlPart.Url, total, CurlUrlFlags.NonSupportScheme);
            errors += (i == 0 ? code != CurlUrlCode.BadScheme : code != CurlUrlCode.Ok) ? Write(output, $"URL {i}: failed to parse [{total}]\n") : 0;
            errors += code == CurlUrlCode.Ok ? CheckHugePart(url, i) : 0;
        }

        return errors;
    }

    /// <summary><c>urldup</c>: each URL set with <c>CURLU_GUESS_SCHEME</c>, duplicated, and both read back the same.</summary>
    /// <returns>1 at the first URL whose copy differs or fails, else 0.</returns>
    public int UrlDup()
    {
        using CurlUrlHandle url = library.UrlInit();
        foreach (string text in DuplicatedUrls)
        {
            if (url.Set(CurlUrlPart.Url, text, CurlUrlFlags.GuessScheme) != CurlUrlCode.Ok)
            {
                return 1;
            }

            using CurlUrlHandle copy = url.Duplicate();
            CurlUrlCode code = url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? original);
            CurlUrlCode copyCode = copy.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? copied);
            if (code != CurlUrlCode.Ok || copyCode != CurlUrlCode.Ok)
            {
                return 1;
            }

            if (original != copied)
            {
                return Write(output, $"Original:  {text}\nParsed:    {original}\nCopy:      {copied}\n");
            }
        }

        return 0;
    }

    /// <summary>
    /// <c>test_api_errors</c> as far as C# can reach it: a part number <c>curl.h</c> lacks, wanted
    /// <c>CURLUE_UNKNOWN_PART</c>. Its <c>NULL</c> handle and <c>NULL</c> part pointer checks have no C# form.
    /// </summary>
    /// <returns>3 when the unknown part answers otherwise, else 0.</returns>
    public int ApiErrors()
    {
        using CurlUrlHandle url = library.UrlInit();
        return url.Get((CurlUrlPart)12345, CurlUrlFlags.None, out _) != CurlUrlCode.UnknownPart ? 3 : 0;
    }

    /// <summary><c>test_scheme_guess</c>: URLs set with and without <c>CURLU_GUESS_SCHEME</c>, and the scheme read back with and without <c>CURLU_NO_GUESS_SCHEME</c>.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.SchemeGuesses"/> in the program.</param>
    /// <returns>The errors of the first failing URL, or 0.</returns>
    public int SchemeGuess(IEnumerable<Lib1560SchemeGuessCase> rows) =>
        UntilError(rows.Select((row, index) => (row, index)), pair => SchemeGuess(pair.row, pair.index));

    private int SchemeGuess(Lib1560SchemeGuessCase row, int index)
    {
        using CurlUrlHandle url = library.UrlInit();
        string? scheme = null;
        CurlUrlCode code = url.Set(CurlUrlPart.Url, row.Url, row.SetFlags);
        int errors = code != CurlUrlCode.Ok && !row.ExpectedFail ? Report($"{index}: URL parsing failed unexpectedly\n") : 0;
        if (code == CurlUrlCode.Ok)
        {
            code = url.Get(CurlUrlPart.Scheme, row.GetFlags, out scheme);
            errors += (code == CurlUrlCode.Ok) == (scheme is null) ? Report($"{index}: returned {(scheme is null ? "success but no scheme" : "error but with scheme")}\n") : 0;
        }

        return errors + CheckScheme(index, scheme, row.Scheme);
    }

    private int ClearPart(CurlUrlHandle url, Lib1560ClearCase row)
    {
        int errors = url.Set(row.Part, row.In, CurlUrlFlags.None) != CurlUrlCode.Ok ? Report("unexpected return code line 2238\n") : 0;
        errors += url.Set(CurlUrlPart.Url, null, CurlUrlFlags.None) != CurlUrlCode.Ok ? Report("unexpected return code line 2244\n") : 0;
        CurlUrlCode code = url.Get(row.Part, CurlUrlFlags.None, out string? text);
        bool differs = code != row.Code || (row.Out is not null && text != row.Out);
        return errors + (differs ? Report("unexpected return code line 2252\n") : 0);
    }

    private int CheckScheme(int index, string? scheme, string? wanted) => (scheme, wanted) switch
    {
        (null, not null) => Report($"{index}: got no scheme when {wanted} was expected\n"),
        (not null, null) => Report($"{index}: got scheme {scheme} when none was expected\n"),
        (not null, not null) when scheme != wanted => Report($"{index}: got scheme {scheme} when {wanted} was expected\n"),
        _ => 0,
    };

    private int CheckHugePart(CurlUrlHandle url, int index)
    {
        url.Get(HugeParts[index], CurlUrlFlags.None, out string? part);
        string wanted = index == 4 ? "/" + BigPart : BigPart;
        return part != wanted ? Write(output, $"URL {index} part {(int)HugeParts[index]}: failure\n") : 0;
    }

    private static int Write(TextWriter writer, string message)
    {
        writer.Write(message);
        return 1;
    }
}
