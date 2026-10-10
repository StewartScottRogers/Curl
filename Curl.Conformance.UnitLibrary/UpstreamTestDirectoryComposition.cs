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
/// <c>%SRCDIR/libtest/test610.pl</c> and <c>%SRCDIR/libtest/test613.pl</c> become
/// <c>./libtest/…</c>, as <c>runtests.pl</c>'s default <c>$srcdir</c> of <c>.</c> names them: those
/// are the two scripts the harness emulates (<see cref="UpstreamTest610Script"/>,
/// <see cref="UpstreamTest613Script"/>). Every other <c>%SRCDIR</c> is left as written, so the
/// case is skipped for a variable with no value rather than failing on a file not vendored.
/// </para>
/// </remarks>
public static class UpstreamTestDirectoryComposition
{
    private static readonly (string Written, string Composed)[] Rewrites =
    [
        ("%PWD/%LOGDIR", "%LOGDIR"),
        ("%SRCDIR/libtest/test610.pl", "./libtest/test610.pl"),
        ("%SRCDIR/libtest/test613.pl", "./libtest/test613.pl"),
    ];

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

        return Encoding.Latin1.GetBytes(text);
    }
}
