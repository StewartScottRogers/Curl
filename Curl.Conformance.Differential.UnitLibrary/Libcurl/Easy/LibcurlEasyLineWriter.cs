using System.Globalization;

namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>
/// Writes one round's result as the line the tester's <c>LibTest easy</c> mode wrote
/// (<c>EasyMode.cs</c>), so both libraries' runs are compared as text: <c>step rc= err= set=[]
/// body= hdr=</c>, then <c> |info</c>, <c> |H</c> query and <c> |W</c> walk segments, bytes in
/// upper-case hexadecimal.
/// </summary>
public static class LibcurlEasyLineWriter
{
    /// <summary>The most headers one walk reads, where the tester's driver stopped.</summary>
    public const int WalkLimit = 500;

    /// <summary>Writes the start of a round's line.</summary>
    /// <param name="step">The round's number on the handle, from 0.</param>
    /// <param name="code">The <c>CURLcode</c> <c>curl_easy_perform</c> returned.</param>
    /// <param name="errorText">The error buffer's bytes.</param>
    /// <param name="setFailures">The <see cref="SetFailure"/> entries of options that did not set.</param>
    /// <param name="body">The bytes the write callback received.</param>
    /// <param name="header">The bytes the header callback received.</param>
    /// <returns>The line's start.</returns>
    public static string Round(int step, int code, ReadOnlySpan<byte> errorText, IEnumerable<string> setFailures, ReadOnlySpan<byte> body, ReadOnlySpan<byte> header) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{step} rc={code} err={Convert.ToHexString(errorText)} set=[{string.Join(',', setFailures)}] body={Convert.ToHexString(body)} hdr={Convert.ToHexString(header)}");

    /// <summary>Writes the entry for an option <c>curl_easy_setopt</c> refused.</summary>
    /// <param name="id">The <c>CURLoption</c> number.</param>
    /// <param name="code">The <c>CURLcode</c> it returned.</param>
    /// <returns>The entry, such as <c>set84=1</c>.</returns>
    public static string SetFailure(int id, int code) => string.Create(CultureInfo.InvariantCulture, $"set{id}={code}");

    /// <summary>Writes one <c>curl_easy_getinfo</c> segment.</summary>
    /// <param name="id">The <c>CURLINFO</c> number.</param>
    /// <param name="code">The <c>CURLcode</c> it returned.</param>
    /// <param name="value">The value as text, <c>null</c> for no string.</param>
    /// <returns>The segment, such as <c> |200002=0:200</c>.</returns>
    public static string Info(int id, int code, string value) => string.Create(CultureInfo.InvariantCulture, $" |{id:X}={code}:{value}");

    /// <summary>Writes one <c>curl_easy_header</c> segment.</summary>
    /// <param name="query">The query.</param>
    /// <param name="code">The <c>CURLHcode</c> it returned.</param>
    /// <param name="header">The header it returned, or <see langword="null"/>.</param>
    /// <returns>The segment.</returns>
    public static string HeaderQuery(LibcurlEasyHeaderQuery query, int code, LibcurlEasyHeaderFields? header)
    {
        ArgumentNullException.ThrowIfNull(query);
        var found = header is null
            ? "null"
            : string.Create(CultureInfo.InvariantCulture, $"{Convert.ToHexString(header.Name)}:{Convert.ToHexString(header.Value)}:{header.Amount}:{header.Index}:{header.Origin}");
        return string.Create(CultureInfo.InvariantCulture, $" |H{query.Name}/{query.Index}/{query.Origin}/{query.Request}={code}:{found}");
    }

    /// <summary>Writes one <c>curl_easy_nextheader</c> walk segment.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="headers">The headers it returned, in order.</param>
    /// <returns>The segment.</returns>
    public static string HeaderWalk(LibcurlEasyHeaderWalk walk, IEnumerable<LibcurlEasyHeaderFields> headers)
    {
        ArgumentNullException.ThrowIfNull(walk);
        ArgumentNullException.ThrowIfNull(headers);
        var parts = headers.Select(h => string.Create(
            CultureInfo.InvariantCulture,
            $"{Convert.ToHexString(h.Name)}={Convert.ToHexString(h.Value)}/{h.Amount}/{h.Index}/{h.Origin}"));
        return string.Create(CultureInfo.InvariantCulture, $" |W{walk.Origin}/{walk.Request}=[{string.Join(',', parts)}]");
    }
}
