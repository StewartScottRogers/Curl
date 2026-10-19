using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <content><c>lib1560.c</c>'s table-driven groups: <c>get_parts</c>, <c>get_url</c>, <c>setget_parts</c>, <c>set_parts</c>, <c>set_url</c> and <c>append</c>.</content>
public sealed partial class Lib1560Checks
{
    /// <summary><c>get_parts</c>: each URL set, its code checked, and on success its parts.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.GetParts"/> in the program.</param>
    /// <returns>The errors of the first failing row, or 0.</returns>
    public int GetParts(IEnumerable<Lib1560UrlCase> rows) => UntilError(rows, GetParts);

    /// <summary><c>get_url</c>: each URL set and, on success, read back whole.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.GetUrls"/> in the program.</param>
    /// <returns>The errors of the first failing row, or 0.</returns>
    public int GetUrls(IEnumerable<Lib1560UrlCase> rows) => UntilError(rows, GetUrl);

    /// <summary><c>setget_parts</c>: each URL set, its part commands run, then its parts read back.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.SetGetParts"/> in the program.</param>
    /// <returns>The errors of the first failing row, or 0.</returns>
    public int SetGetParts(IEnumerable<Lib1560SetGetCase> rows) => UntilError(rows, SetGetParts);

    /// <summary><c>set_parts</c>: each URL set, its part commands run, then the URL read back.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.SetParts"/> in the program.</param>
    /// <returns>The errors of the first failing row, or 0.</returns>
    public int SetParts(IEnumerable<Lib1560SetCase> rows) => UntilError(rows, SetParts);

    /// <summary><c>set_url</c>: each URL set, then the second URL over it, then the URL read back with <c>CURLU_GET_EMPTY</c>.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.SetUrls"/> in the program.</param>
    /// <returns>The errors of the first failing row, or 0.</returns>
    public int SetUrls(IEnumerable<Lib1560SetCase> rows) => UntilError(rows, SetUrl);

    /// <summary><c>append</c>: each URL set, its query appended, then the URL read back.</summary>
    /// <param name="rows">The rows, <see cref="Lib1560Tables.Appends"/> in the program.</param>
    /// <returns>The errors of the first failing row, or 0.</returns>
    public int Appends(IEnumerable<Lib1560SetCase> rows) => UntilError(rows, Append);

    private int GetParts(Lib1560UrlCase row)
    {
        if ((row.WindowsOnly && !isWindows) || !Runs(row.GetFlags))
        {
            return 0;
        }

        using CurlUrlHandle url = library.UrlInit();
        CurlUrlCode code = url.Set(CurlUrlPart.Url, row.In, row.UrlFlags);
        if (code != row.Code)
        {
            error.Write($"Get parts\nin: {row.In}\nreturned {(int)code} (expected {(int)row.Code})\n");
            return 1;
        }

        return code == CurlUrlCode.Ok ? CheckParts(url, row.In, row.Out, row.GetFlags) : 0;
    }

    private int GetUrl(Lib1560UrlCase row)
    {
        if (!Runs(row.GetFlags))
        {
            return 0;
        }

        using CurlUrlHandle url = library.UrlInit();
        int errors = 0;
        CurlUrlCode code = url.Set(CurlUrlPart.Url, row.In, row.UrlFlags);
        if (code == CurlUrlCode.Ok)
        {
            code = url.Get(CurlUrlPart.Url, row.GetFlags, out string? text);
            errors += code != CurlUrlCode.Ok
                ? Report($"lib1560.c:1879 returned {Describe(code)}. URL: '{row.In}'\n")
                : CheckUrl(row.In, text, row.Out);
        }

        if (code != row.Code)
        {
            errors += Report($"Get URL\nin: {row.In}\nreturned {(int)code} (expected {(int)row.Code})\n");
        }

        return errors;
    }

    private int SetGetParts(Lib1560SetGetCase row)
    {
        if (!Runs(row.GetFlags))
        {
            return 0;
        }

        using CurlUrlHandle url = library.UrlInit();
        CurlUrlCode code = SetFirstUrl(url, row.In, row.UrlFlags);
        if (code != CurlUrlCode.Ok)
        {
            return Report($"Set parts\nin: {Text(row.In)}\nreturned {(int)code} (expected 0)\n");
        }

        CurlUrlCode partCode = UpdateUrl(url, row.Set, row.SetFlags);
        int errors = partCode != row.PartCode
            ? Report($"updateurl\nin: {row.Set}\nreturned {(int)partCode} (expected {(int)row.PartCode})\n")
            : 0;
        return partCode == CurlUrlCode.Ok ? errors + CheckParts(url, row.Set, row.Out, row.GetFlags) : errors;
    }

    private int SetParts(Lib1560SetCase row)
    {
        using CurlUrlHandle url = library.UrlInit();
        CurlUrlCode code = SetFirstUrl(url, row.In, row.UrlFlags);
        if (code != CurlUrlCode.Ok)
        {
            return code != row.UrlCode
                ? Report($"Set parts\nin: {Text(row.In)}\nreturned {(int)code} (expected {(int)row.UrlCode})\n")
                : 0;
        }

        CurlUrlCode partCode = UpdateUrl(url, row.Set, row.SetFlags);
        int errors = partCode != row.PartCode
            ? Report($"updateurl\nin: {row.Set}\nreturned {(int)partCode} (expected {(int)row.PartCode})\n")
            : 0;
        return partCode == CurlUrlCode.Ok ? errors + ReadBack(url, row.In, row.Out, CurlUrlFlags.None, 1839) : errors;
    }

    private int SetUrl(Lib1560SetCase row)
    {
        using CurlUrlHandle url = library.UrlInit();
        CurlUrlCode code = url.Set(CurlUrlPart.Url, row.In, row.UrlFlags);
        if (code != CurlUrlCode.Ok)
        {
            return code != row.UrlCode
                ? Report($"Set URL\nin: {Text(row.In)}\nreturned {(int)code} (expected {(int)row.UrlCode})\n")
                : 0;
        }

        code = url.Set(CurlUrlPart.Url, row.Set, row.SetFlags);
        return code != CurlUrlCode.Ok
            ? Report($"lib1560.c:1725 Set URL {row.Set} returned {Describe(code)}\n")
            : ReadBack(url, row.In, row.Out, CurlUrlFlags.GetEmpty, 1734);
    }

    private int Append(Lib1560SetCase row)
    {
        using CurlUrlHandle url = library.UrlInit();
        if (url.Set(CurlUrlPart.Url, row.In, row.UrlFlags) != CurlUrlCode.Ok)
        {
            return 1;
        }

        CurlUrlCode code = url.Set(CurlUrlPart.Query, row.Set, row.SetFlags | CurlUrlFlags.AppendQuery);
        if (code != row.UrlCode)
        {
            return Report($"Append\nin: {Text(row.In)}\nreturned {(int)code} (expected {(int)row.UrlCode})\n");
        }

        return code == CurlUrlCode.Ok ? ReadBack(url, row.In, row.Out, CurlUrlFlags.None, 1986) : 0;
    }

    private static CurlUrlCode SetFirstUrl(CurlUrlHandle url, string? input, CurlUrlFlags flags) =>
        input is null ? CurlUrlCode.Ok : url.Set(CurlUrlPart.Url, input, flags);

    private int ReadBack(CurlUrlHandle url, string? original, string wanted, CurlUrlFlags getFlags, int line)
    {
        CurlUrlCode code = url.Get(CurlUrlPart.Url, getFlags, out string? text);
        return code != CurlUrlCode.Ok
            ? Report($"lib1560.c:{line} Get URL returned {Describe(code)}\n")
            : CheckUrl(original, text, wanted);
    }

    private int Report(string message)
    {
        error.Write(message);
        return 1;
    }
}
