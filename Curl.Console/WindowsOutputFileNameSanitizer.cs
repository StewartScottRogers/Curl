using System.Buffers;

namespace Curl.Console;

/// <summary>
/// Rewrites an <c>-o</c> / <c>--output</c> file name the way curl 8.21.0 does on Windows
/// before it uses the name: each <c>"</c>, <c>*</c>, <c>&lt;</c>, <c>&gt;</c>, <c>?</c>,
/// <c>|</c> and control character U+0001 to U+001F becomes <c>_</c>.
/// </summary>
/// <remarks>
/// <para>
/// Measured 2026-09-26 on Windows 11 with the local curl 8.21.0 (x86_64-w64-mingw32),
/// <c>curl --no-progress-meter -o &lt;name&gt; file:///C:/Windows/win.ini</c>, one name per
/// case, relative to an empty directory:
/// </para>
/// <list type="bullet">
/// <item><c>a?b</c>, <c>a*b</c>, <c>a&lt;b</c>, <c>a&gt;b</c>, <c>a|b</c>, <c>a"b</c>, and
/// <c>a</c>, U+0001 to U+001F (each one measured), <c>b</c>: exit 0, nothing on stderr, the
/// file <c>a_b</c> written.</item>
/// <item><c>C:/…/sub/x?y</c>: exit 0, <c>sub/x_y</c> written; <c>sub\a?b</c>: exit 0,
/// <c>sub/a_b</c> written. Drive letters, <c>/</c> and <c>\</c> are kept.</item>
/// <item><c>sub?/x</c> with no <c>sub_</c> directory: exit 23,
/// <c>Warning: Failed to open the file sub_/x: No such file or directory</c>; a directory
/// part is rewritten too, and the warning names the rewritten file.</item>
/// <item><c>ab:c</c>: exit 0, nothing on stderr, <c>ab</c> created empty with the body in
/// its NTFS <c>c</c> stream; <c>C:/…/sub/x:y</c> likewise. <c>:</c> is kept.</item>
/// <item><c>a</c>, U+007F, <c>b</c>: exit 0, the file written under that name; U+007F is kept.</item>
/// <item><c>con</c> and <c>nul</c>: exit 0, nothing on stderr or stdout, no file.
/// <c>prn</c>, <c>aux</c>, <c>com1</c>, <c>lpt1</c>: exit 23,
/// <c>Warning: Failed to open the file prn: No such file or directory</c> (each its own
/// name) then <c>curl: (23) client returned ERROR on write of 92 bytes</c>. The same names
/// with <c>.txt</c>: exit 0, the file written under that name. Reserved device names are
/// kept.</item>
/// </list>
/// <para>
/// So <c>-o "Z:/a&lt;b"</c> and <c>-o "Z:/a|b"</c> exit 0 printing nothing because they
/// write <c>Z:/a_b</c>, and <c>-o "C:/x?y"</c> fails on <c>C:/x_y</c>. A <c>-C -</c> resume
/// takes the size of the rewritten file (<c>-C - -o a?b</c> appended to <c>a_b</c>), and
/// <c>curl: cannot open</c> names it too (<c>-C 3 -o sub?</c> printed
/// <c>curl: cannot open 'sub_'</c>).
/// </para>
/// </remarks>
internal static class WindowsOutputFileNameSanitizer
{
    /// <summary>The characters curl 8.21.0 replaces with <c>_</c>.</summary>
    private static readonly SearchValues<char> ReplacedCharacters = SearchValues.Create(
        "\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F"
        + "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F"
        + "\"*<>?|");

    /// <summary>
    /// Replaces each character curl 8.21.0 does not keep in an <c>-o</c> name with <c>_</c>.
    /// </summary>
    /// <param name="fileName">The <c>-o</c> value, as typed.</param>
    /// <returns>The name curl opens.</returns>
    internal static string Sanitize(string fileName)
    {
        char[] characters = fileName.ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            if (ReplacedCharacters.Contains(characters[index]))
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }
}
