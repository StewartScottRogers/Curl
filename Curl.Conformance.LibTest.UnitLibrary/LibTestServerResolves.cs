using Libcurl.Misc.DatesAndLists;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The fake DNS entries and URLs <c>lib1506.c</c> and <c>lib1510.c</c> share: for handle <c>n</c> (from 1),
/// <c>server&lt;n&gt;.example.com</c> resolved to <c>libtest_arg2</c> on port <c>libtest_arg3</c>, and the URL
/// <c>http://server&lt;n&gt;.example.com:&lt;port&gt;/path/&lt;test&gt;&lt;nnnn&gt;</c>.
/// </summary>
public static class LibTestServerResolves
{
    /// <summary>
    /// Appends <c>server&lt;n&gt;.example.com:&lt;port&gt;:&lt;address&gt;</c> for n from 1 to <paramref name="count"/>
    /// to a new list, writing each entry and a newline on stdout first, as the programs do before <c>global_init</c>.
    /// </summary>
    /// <param name="context">The program's context: its <c>libtest_arg2</c>, <c>libtest_arg3</c> and stdout.</param>
    /// <param name="count">The number of entries.</param>
    /// <returns>The list, for <c>CURLOPT_RESOLVE</c>; <see langword="null"/> when <paramref name="count"/> is 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static CurlSlist? AppendAndWrite(LibTestContext context, int count)
    {
        ArgumentNullException.ThrowIfNull(context);
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        CurlSlist? list = null;
        for (int n = 1; n <= count; n++)
        {
            string entry = $"server{n}.example.com:{context.Argument3}:{context.Argument2}";
            output.Write($"{entry}\n");
            list = CurlSlist.Append(list, entry);
        }

        return list;
    }

    /// <summary>The URL of handle <paramref name="n"/>: <c>http://server&lt;n&gt;.example.com:&lt;port&gt;/path/&lt;test&gt;&lt;nnnn&gt;</c>.</summary>
    /// <param name="context">The program's context: its test number and <c>libtest_arg3</c>.</param>
    /// <param name="n">The handle's number, from 1.</param>
    /// <returns>The URL.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static string Url(LibTestContext context, int n)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"http://server{n}.example.com:{context.Argument3}/path/{context.TestNumber}{n:D4}";
    }
}
