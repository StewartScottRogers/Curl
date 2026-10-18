namespace Curl.Conformance;

/// <summary>
/// Reads a precheck line that runs a libtest program through <c>%LIBTESTS</c>, such as test678's
/// <c>./libtest/libtests lib678 check</c> (BL-2634): <c>libtests</c> takes the program's name, then the
/// words <c>first.c</c> hands it as its <c>URL</c> and <c>libtest_arg2</c> onwards.
/// </summary>
internal static class UpstreamLibTestCheckLine
{
    /// <summary>Finds the libtest program and its arguments a check line runs.</summary>
    /// <param name="line">An expanded precheck line.</param>
    /// <returns>
    /// The program's name (<c>libNNN</c>) and the words after it, or <see langword="null"/> when the line does
    /// not run <see cref="UpstreamCaseRunner.LibTestsProgram"/> with a libtest program's name.
    /// </returns>
    public static (string Program, string[] Arguments)? Parse(string line)
    {
        string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 1 && words[0] == UpstreamCaseRunner.LibTestsProgram && IsLibTestName(words[1])
            ? (words[1], words[2..])
            : null;
    }

    private static bool IsLibTestName(string word) =>
        word.Length > 3 && word.StartsWith("lib", StringComparison.Ordinal) && word[3..].All(char.IsAsciiDigit);
}
