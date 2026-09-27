using Curl.Protocol.Abstractions;

namespace Curl.Core.Globbing;

/// <summary>
/// Pins how <see cref="UrlGlob" /> expands URL globs and substitutes <c>#N</c>. Every case
/// was measured against curl 8.21.0 (mingw, Schannel) on 2026-09-26 with
/// <c>curl -s -S -w '%{url}|%{filename_effective}\n' -o '&lt;name&gt;' '&lt;url&gt;'</c>
/// over <c>file:///n/...</c>, which fails each transfer with exit 37 but prints every
/// expanded URL and output file name.
/// </summary>
[TestClass]
public sealed class UrlGlobTests
{
    [TestMethod]
    [DataRow("file:///n/{a,b}x[1-2]", "file:///n/ax1|file:///n/ax2|file:///n/bx1|file:///n/bx2")]
    [DataRow("file:///n/[a-c][1-2]", "file:///n/a1|file:///n/a2|file:///n/b1|file:///n/b2|file:///n/c1|file:///n/c2")]
    [DataRow("file:///n/[01-10]", "file:///n/01|file:///n/02|file:///n/03|file:///n/04|file:///n/05|file:///n/06|file:///n/07|file:///n/08|file:///n/09|file:///n/10")]
    [DataRow("file:///n/[0-10]", "file:///n/0|file:///n/1|file:///n/2|file:///n/3|file:///n/4|file:///n/5|file:///n/6|file:///n/7|file:///n/8|file:///n/9|file:///n/10")]
    [DataRow("file:///n/[001-3]", "file:///n/001|file:///n/002|file:///n/003")]
    [DataRow("file:///n/[08-100:45]", "file:///n/08|file:///n/53|file:///n/98")]
    [DataRow("file:///n/[1-10:3]", "file:///n/1|file:///n/4|file:///n/7|file:///n/10")]
    [DataRow("file:///n/[1-3:2]", "file:///n/1|file:///n/3")]
    [DataRow("file:///n/[1- 3]", "file:///n/1|file:///n/2|file:///n/3")]
    [DataRow("file:///n/[1-\t3]", "file:///n/1|file:///n/2|file:///n/3")]
    [DataRow("file:///n/[5-5]", "file:///n/5")]
    [DataRow("file:///n/[4294967295-4294967297]", "file:///n/4294967295|file:///n/4294967296|file:///n/4294967297")]
    [DataRow("file:///n/[a-z:5]", "file:///n/a|file:///n/f|file:///n/k|file:///n/p|file:///n/u|file:///n/z")]
    [DataRow("file:///n/[a-z:25]", "file:///n/a|file:///n/z")]
    [DataRow("file:///n/[A-Z:25]", "file:///n/A|file:///n/Z")]
    [DataRow("file:///n/[a-a]", "file:///n/a")]
    [DataRow("{a}[Z-a]", "aZ|a[|a\\|a]|a^|a_|a`|aa")]
    [DataRow("file:///n/{a,}", "file:///n/a|file:///n/")]
    [DataRow("file:///n/{,}", "file:///n/|file:///n/")]
    [DataRow("file:///n/{a\\,b,c\\}d\\x}", "file:///n/a,b|file:///n/c}dx")]
    [DataRow("file:///n/\\{a\\}\\[\\]\\x[]", "file:///n/{a}[]\\x[]")]
    [DataRow("http://[::1]:1/[1-2]", "http://[::1]:1/1|http://[::1]:1/2")]
    [DataRow("http://[fe80::1%25eth0]:1/", "http://[fe80::1%25eth0]:1/")]
    [DataRow("http://[fe80::1%eth0]:1/{a}", "http://[fe80::1%eth0]:1/a")]
    [DataRow("", "")]
    public void Expand_WellFormedGlob_ProducesCurlsUrlsInCurlsOrder(string url, string expectedUrls)
    {
        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out TransferResult? failure));
        Assert.IsNull(failure);

        string[] expected = expectedUrls.Split('|');
        CollectionAssert.AreEqual(expected, glob.Expand().Select(match => match.Url).ToArray());
        Assert.AreEqual(expected.Length, glob.UrlCount);
    }

    [TestMethod]
    public void Expand_HundredSets_HasNoGlobLimit()
    {
        string url = "file:///" + string.Concat(Enumerable.Repeat("{a}", 100));

        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out _));

        Assert.AreEqual("file:///" + new string('a', 100), glob.Expand().Single().Url);
    }

    [TestMethod]
    public void Expand_NumberRangeToOneBelowMaximum_IsProducedLazily()
    {
        Assert.IsTrue(UrlGlob.TryParse("file:///n/[0-9223372036854775806]", out UrlGlob? glob, out _));

        Assert.AreEqual(long.MaxValue, glob.UrlCount);
        CollectionAssert.AreEqual(
            new[] { "file:///n/0", "file:///n/1", "file:///n/2" },
            glob.Expand().Take(3).Select(match => match.Url).ToArray());
    }

    [TestMethod]
    [DataRow("file:///n/{a,b}x[1-2]", "o_#1_#2.txt", "o_a_1.txt|o_a_2.txt|o_b_1.txt|o_b_2.txt")]
    [DataRow("file:///n/[01-10]", "o_#1", "o_01|o_02|o_03|o_04|o_05|o_06|o_07|o_08|o_09|o_10")]
    [DataRow("file:///n/{x}[1-2]", "o_#3#1#9", "o_#3x#9|o_#3x#9")]
    [DataRow("file:///n/[1-2]", "o_#01_#0_#1#", "o_1_#0_1#|o_2_#0_2#")]
    [DataRow("file:///n/[1-2]", "o_#1#2", "o_1#2|o_2#2")]
    [DataRow("file:///n/[1-2]", "#99999999999999999999", "#99999999999999999999|#99999999999999999999")]
    [DataRow("file:///n/\\{a\\}\\[\\]\\x[]", "o_#1", "o_#1")]
    public void SubstituteGlobValues_HashNumber_IsReplacedAsCurlReplacesIt(string url, string outputFileName, string expectedNames)
    {
        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out _));

        CollectionAssert.AreEqual(
            expectedNames.Split('|'),
            glob.Expand().Select(match => match.SubstituteGlobValues(outputFileName)).ToArray());
    }

    [TestMethod]
    public void Unglobbed_GlobOff_TakesUrlAndHashNumbersAsWritten()
    {
        UrlGlob glob = UrlGlob.Unglobbed("file:///n/[1-2]{a,b}");

        UrlGlobMatch match = glob.Expand().Single();

        Assert.AreEqual(1, glob.UrlCount);
        Assert.AreEqual("file:///n/[1-2]{a,b}", match.Url);
        Assert.IsEmpty(match.GlobValues);
        Assert.AreEqual("o_#1", match.SubstituteGlobValues("o_#1"));
    }

    [TestMethod]
    [DataRow("file:///n/{a", "unmatched brace", 13)]
    [DataRow("file:///n/{a\\", "unmatched brace", 14)]
    [DataRow("file:///n/{}", "empty string within braces", 12)]
    [DataRow("file:///n/{a,{b}}", "nested brace", 14)]
    [DataRow("file:///n/{a,[1]}", "nested brace", 14)]
    [DataRow("file:///n/{a]}", "unexpected close bracket", 13)]
    [DataRow("file:///n/]", "unmatched close brace/bracket", 11)]
    [DataRow("file:///n/}", "unmatched close brace/bracket", 11)]
    [DataRow("file:///n/[1-", "bad range", 14)]
    [DataRow("file:///n/[3-1]", "bad range", 16)]
    [DataRow("file:///n/[a-5]", "bad range", 16)]
    [DataRow("file:///n/[1-2:0]", "bad range", 18)]
    [DataRow("file:///n/[a-Z]", "bad range", 16)]
    [DataRow("file:///n/[A-z]", "bad range", 16)]
    [DataRow("file:///n/[1]", "bad range", 13)]
    [DataRow("file:///n/[a]", "bad range", 12)]
    [DataRow("file:///n/[1-2", "bad range", 15)]
    [DataRow("file:///n/[1-2:]", "bad range", 16)]
    [DataRow("file:///n/[1-2:x]", "bad range", 16)]
    [DataRow("file:///n/[a-c:]", "bad range", 16)]
    [DataRow("file:///n/[1 -2]", "bad range", 13)]
    [DataRow("file:///n/[a-cd]", "bad range", 12)]
    [DataRow("file:///n/[1-2x]", "bad range", 15)]
    [DataRow("file:///n/[1-3:5]", "bad range", 18)]
    [DataRow("file:///n/[a-a:2]", "bad range", 18)]
    [DataRow("file:///n/[a-z:300]", "bad range", 16)]
    [DataRow("file:///n/[a-z:256x]", "bad range", 19)]
    [DataRow("file:///n/[a-z:26]", "bad range", 19)]
    [DataRow("file:///n/[0-99999999999999999999]", "bad range", 14)]
    [DataRow("file:///n/[99999999999999999999-1]", "bad range", 12)]
    [DataRow("file:///n/[1-3:99999999999999999999]", "bad range", 16)]
    [DataRow("file:///n/[9-9223372036854775807:0]", "bad range", 36)]
    [DataRow("file:///n/[9223372036854775800-9223372036854775807:10]", "bad range", 55)]
    [DataRow("file:///n/[1-9223372036854775807:9223372036854775807]", "bad range", 54)]
    [DataRow("file:///n/[-1-2]", "bad range specification", 12)]
    [DataRow("file:///n/[ 1-2]", "bad range specification", 12)]
    [DataRow("file:///n/[0-9223372036854775807]", "range end/step overflow", 34)]
    [DataRow("file:///n/[1-9223372036854775807]", "range end/step overflow", 34)]
    [DataRow("file:///n/[9223372036854775807-9223372036854775807]", "range end/step overflow", 52)]
    [DataRow("file:///n/[9223372036854775790-9223372036854775800:10]", "range end/step overflow", 55)]
    [DataRow("file:///n/[0-999999999][0-999999999][0-999999999]", "range overflow", 50)]
    [DataRow("file:///n/{a,b,c,d,e,f,g,h,i,j}[0-999999999][0-999999999]", "range overflow", 57)]
    [DataRow("{a}{b}[1-", "bad range", 8)]
    [DataRow("{a}{b,}}", "unmatched close brace/bracket", 6)]
    [DataRow("file:///n/[0-999999999][0-999999999][a-z]", "range overflow", 42)]
    [DataRow("file:///n/[a-z][0-999999999][0-999999999][a-z]", "range overflow", 42)]
    public void TryParse_MalformedGlob_IsUrlMalformatWithCurlsPositionMessage(string url, string reason, int column)
    {
        Assert.IsFalse(UrlGlob.TryParse(url, out UrlGlob? glob, out TransferResult? failure));

        Assert.IsNull(glob);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual($"{reason} in position {column}:\n{url}\n{new string(' ', column - 1)}^", failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("{a}]", "unmatched close brace/bracket in position 3:\n{a}]\n  ^")]
    [DataRow("]x", "unmatched close brace/bracket in position 1:\n]x\n ^")]
    [DataRow("[", "bad range specification in position 2:\n[\n ^")]
    [DataRow("file:///n/[0-999999999][0-999999999]{a,b,c,d,e,f,g,h,i,j}", "range overflow")]
    public void TryParse_MalformedGlob_MessageMatchesCurlsEdgeFormatting(string url, string message)
    {
        Assert.IsFalse(UrlGlob.TryParse(url, out _, out TransferResult? failure));

        Assert.AreEqual(message, failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("http://[fe80::1%]/[1-2]", "bad range", 9)]
    [DataRow("http://[fe80::1%25]/", "bad range", 9)]
    [DataRow("http://[fe80::1%e/h]/", "bad range", 9)]
    [DataRow("http://[1.2.3.4]/", "bad range", 10)]
    [DataRow("http://[::1/64]/", "bad range specification", 9)]
    [DataRow("http://[::g]/", "bad range specification", 9)]
    public void TryParse_BracketThatIsNotAnIPv6Literal_IsReadAsARange(string url, string reason, int column)
    {
        Assert.IsFalse(UrlGlob.TryParse(url, out _, out TransferResult? failure));

        Assert.AreEqual($"{reason} in position {column}:\n{url}\n{new string(' ', column - 1)}^", failure.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_BracketLongerThanAnIPv6Literal_IsReadAsARange()
    {
        string url = "http://[" + new string(':', 126) + "]/";

        Assert.IsFalse(UrlGlob.TryParse(url, out _, out TransferResult? failure));

        StringAssert.StartsWith(failure.ErrorMessage, "bad range specification in position 9:");
    }

    [TestMethod]
    public void TryParse_NullUrl_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlGlob.TryParse(null!, out _, out _));

    [TestMethod]
    public void Unglobbed_NullUrl_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlGlob.Unglobbed(null!));

    [TestMethod]
    public void SubstituteGlobValues_NullName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlGlob.Unglobbed("x").Expand().Single().SubstituteGlobValues(null!));
}
