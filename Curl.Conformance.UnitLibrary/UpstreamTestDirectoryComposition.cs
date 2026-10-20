using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Rewrites the directory compositions of a test file that only make sense in
/// <c>runtests.pl</c>'s working directory, before the file is expanded (ADR-0458).
/// </summary>
/// <remarks>
/// <para>
/// <c>runtests.pl</c> runs in the <c>tests</c> folder with <c>%LOGDIR</c> as the relative
/// <c>log</c>, so <c>%PWD/%LOGDIR/x</c> names <c>tests/log/x</c>. The runner's <c>%LOGDIR</c> is
/// absolute, so cases can run in parallel, and no value of <c>%PWD</c> composes with it on every
/// platform; <c>%PWD/%LOGDIR</c> is rewritten to <c>%LOGDIR</c>, which names the same file.
/// <c>%PWD</c> anywhere else is left as written.
/// </para>
/// <para>
/// Upstream's <c>%FILE_PWD</c> is the tests folder as an absolute path starting with <c>/</c>
/// (<c>/C:/…</c> on Windows), so <c>file:%FILE_PWD/%LOGDIR/x</c> has one slash before the path.
/// <c>%FILE_PWD/%LOGDIR</c> is rewritten to <c>%FILE_PWD%LOGDIR</c>, and the runner gives
/// <c>%FILE_PWD</c> the value <see cref="FilePwdBefore"/> returns for its log directory.
/// </para>
/// <para>
/// <c>%SRCDIR/libtest/test610.pl</c> and <c>%SRCDIR/libtest/test613.pl</c> become
/// <c>./libtest/…</c>, as <c>runtests.pl</c>'s default <c>$srcdir</c> of <c>.</c> names them: those
/// are the two scripts the harness emulates (<see cref="UpstreamTest610Script"/>,
/// <see cref="UpstreamTest613Script"/>). Every other <c>%SRCDIR</c> is left as written, so the
/// case is skipped for a variable with no value rather than failing on a file not vendored.
/// </para>
/// <para>
/// <c>filename=%DEV_NULL</c> and <c>filename="%DEV_NULL"</c>, the faked file name of a form part
/// (test173), become <c>/dev/null</c> on every platform: that name is only text curl sends, and the
/// case's expected bytes hold the Unix name. <c>%DEV_NULL</c> anywhere else, such as a file curl
/// opens (<c>-o%DEV_NULL</c>, test3015), stays the platform's null device (ADR-0716).
/// </para>
/// </remarks>
public static class UpstreamTestDirectoryComposition
{
    private static readonly (string Written, string Composed)[] Rewrites =
    [
        ("%PWD/%LOGDIR", "%LOGDIR"),
        ("%FILE_PWD/%LOGDIR", "%FILE_PWD%LOGDIR"),
        ("%SRCDIR/libtest/test610.pl", "./libtest/test610.pl"),
        ("%SRCDIR/libtest/test613.pl", "./libtest/test613.pl"),
        ("filename=%DEV_NULL", "filename=/dev/null"),
        ("filename=\"%DEV_NULL\"", "filename=\"/dev/null\""),
    ];

    /// <summary>
    /// Returns the value of <c>%FILE_PWD</c> for a log directory, so that the rewritten
    /// <c>%FILE_PWD%LOGDIR</c> is an absolute path starting with one <c>/</c>, as upstream's
    /// <c>%FILE_PWD/%LOGDIR</c> is: <c>/</c> before a drive-letter directory (<c>C:/…</c>), and
    /// nothing before one that already starts with <c>/</c>, so <c>file:%FILE_PWD/%LOGDIR</c>
    /// never becomes <c>file://tmp/…</c>, a URL naming the host <c>tmp</c> (test203, BL-2258).
    /// </summary>
    /// <param name="logDirectory">The case's absolute log directory, with forward slashes.</param>
    /// <returns><c>/</c>, or the empty string when <paramref name="logDirectory"/> starts with <c>/</c>.</returns>
    public static string FilePwdBefore(string logDirectory) =>
        logDirectory.StartsWith('/') ? string.Empty : "/";

    /// <summary>Returns the test file with its directory compositions rewritten.</summary>
    /// <param name="testFile">The case's file, as vendored.</param>
    /// <returns>The file's bytes, every other byte as it was.</returns>
    public static byte[] Rewrite(ReadOnlySpan<byte> testFile)
    {
        // Latin-1 maps every byte to one char and back, so nothing else changes.
        string text = Encoding.Latin1.GetString(testFile);
        foreach ((string written, string composed) in Rewrites)
        {
            text = text.Replace(written, composed, StringComparison.Ordinal);
        }

        text = text.Replace("name=\"log/", "name=\"%LOGDIR/", StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(RewriteCommandLogPaths(text));
    }

    // Inside <command>, a word starting with log/ names runtests.pl's log folder (test5018's -T log/upload5018).
    private static string RewriteCommandLogPaths(string text)
    {
        const string Open = "<command";
        const string Close = "</command>";
        StringBuilder rewritten = new();
        int position = 0;
        while (text.IndexOf(Open, position, StringComparison.Ordinal) is var start and >= 0
            && text.IndexOf(Close, start, StringComparison.Ordinal) is var end and >= 0)
        {
            rewritten.Append(text, position, start - position);
            rewritten.Append(text[start..end].Replace(" log/", " %LOGDIR/", StringComparison.Ordinal).Replace("\"log/", "\"%LOGDIR/", StringComparison.Ordinal));
            position = end;
        }

        return rewritten.Append(text, position, text.Length - position).ToString();
    }
}
