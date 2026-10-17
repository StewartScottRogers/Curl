using System.Text;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The file a program names in <c>libtest_arg2</c> and writes a line to from a callback, opening and closing it
/// each time: <c>curlx_fopen(libtest_arg2, "wb")</c> in <c>lib578.c</c> and <c>lib599.c</c>, <c>"ab"</c> in
/// <c>lib579.c</c>, each followed by <c>curl_mfprintf</c> and <c>curlx_fclose</c>.
/// </summary>
public static class LibTestArgumentFile
{
    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, as ASCII bytes, replacing or appending.</summary>
    /// <param name="path">The file, or <see langword="null"/> when the case gives none.</param>
    /// <param name="text">What to write.</param>
    /// <param name="append">Whether to append (<c>"ab"</c>) rather than replace (<c>"wb"</c>).</param>
    /// <returns>Whether the file opened; <see langword="false"/> writes nothing, as a <c>NULL</c> <c>FILE *</c> does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static bool TryWrite(string? path, string text, bool append)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            using var file = new FileStream(path!, append ? FileMode.Append : FileMode.Create, FileAccess.Write);
            file.Write(Encoding.ASCII.GetBytes(text));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
