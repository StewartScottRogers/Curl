namespace Curl.Conformance;

/// <summary>
/// The words of a <c>test1013.pl</c> or <c>test1022.pl</c> postcheck line, and the trailing
/// <c>&gt; FILE</c> redirect case 1014 adds, which the shell, not the script, carries out.
/// </summary>
internal static class UpstreamCurlConfigScriptLine
{
    /// <summary>Splits a line into the script's words and the file a trailing <c>&gt; FILE</c> names.</summary>
    /// <param name="arguments">The expanded line after the Perl program's name.</param>
    /// <returns>The words before any <c>&gt;</c>, and the redirect's file or <see langword="null"/>.</returns>
    public static (string[] Words, string? Redirect) Split(string arguments)
    {
        string[] words = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int redirect = Array.IndexOf(words, ">");
        return redirect < 0 ? (words, null) : (words[..redirect], words.ElementAtOrDefault(redirect + 1));
    }

    /// <summary>Writes the script's output to the redirect's file, if any, and leaves the result without it.</summary>
    /// <param name="result">What the script did.</param>
    /// <param name="redirect">The redirect's file, or <see langword="null"/>.</param>
    /// <returns><paramref name="result"/>, without its output when it went to the file.</returns>
    public static UpstreamPerlOneLinerResult Redirect(UpstreamPerlOneLinerResult result, string? redirect)
    {
        if (redirect is null)
        {
            return result;
        }

        File.WriteAllText(redirect, result.Output);
        return result with { Output = "" };
    }
}
