using System.Collections;

namespace Libcurl.Misc.DatesAndLists;

/// <summary>
/// <c>struct curl_slist</c>: the list of strings every list-valued option takes
/// (<c>CURLOPT_HTTPHEADER</c>, <c>CURLOPT_QUOTE</c>, <c>CURLOPT_RESOLVE</c> and the rest).
/// <see cref="Append"/> is <c>curl_slist_append</c> and <see cref="FreeAll"/> is
/// <c>curl_slist_free_all</c>.
/// </summary>
/// <remarks>
/// Setting a list-valued option copies the list's strings at that moment, so a list appended to or
/// freed after <c>SetOption</c> leaves the option as it was set (ADR-0578). Upstream keeps the
/// caller's pointer instead and needs the list alive until the transfer ends; a copy is what a
/// managed caller can rely on, and a list kept alive gives the same transfer either way.
/// </remarks>
public sealed class CurlSlist : IReadOnlyList<string>
{
    private readonly List<string> strings = [];

    // Private so a list only comes from Append, as upstream starts a list with curl_slist_append(NULL, ...).
    private CurlSlist()
    {
    }

    /// <summary>How many strings the list holds.</summary>
    public int Count => strings.Count;

    /// <summary>The string at <paramref name="index"/>, in the order they were appended.</summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The string.</returns>
    public string this[int index] => strings[index];

    /// <summary>
    /// <c>curl_slist_append</c>: adds <paramref name="text"/> to the end of <paramref name="list"/>,
    /// or to a new list when <paramref name="list"/> is <see langword="null"/>, and answers the list.
    /// </summary>
    /// <param name="list">The list to add to, or <see langword="null"/> to start one.</param>
    /// <param name="text">The string to add; a string is immutable, so this is upstream's copy.</param>
    /// <returns><paramref name="list"/>, or the new list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static CurlSlist Append(CurlSlist? list, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        CurlSlist appendedTo = list ?? new CurlSlist();
        appendedTo.strings.Add(text);
        return appendedTo;
    }

    /// <summary>
    /// <c>curl_slist_free_all</c>: empties <paramref name="list"/>; <see langword="null"/> does
    /// nothing, as upstream allows. An option set from the list before keeps its copy.
    /// </summary>
    /// <param name="list">The list to free, or <see langword="null"/>.</param>
    public static void FreeAll(CurlSlist? list) => list?.strings.Clear();

    /// <inheritdoc/>
    public IEnumerator<string> GetEnumerator() => strings.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
