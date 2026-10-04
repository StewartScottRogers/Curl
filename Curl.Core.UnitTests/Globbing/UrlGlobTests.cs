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
    [DataRow("http://testingthis/", "{a}b", 128, 403)]
    [DataRow("http://testingthis/", "{a}b", 201, 403)]
    [DataRow("http://t/", "{a}b", 128, 393)]
    [DataRow("http://testingthis/", "[1-1]b", 128, 787)]
    [DataRow("http://x/", "b{a}", 128, 394)]
    public void TryParse_256thPiece_IsTooManySetsAtCurlsMeasuredPosition(string prefix, string repeated, int copies, int column)
    {
        string url = prefix + string.Concat(Enumerable.Repeat(repeated, copies));

        Assert.IsFalse(UrlGlob.TryParse(url, out UrlGlob? glob, out TransferResult? failure));

        Assert.IsNull(glob);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        string message = $"too many {{}} sets in position {column}:\n{url}\n{new string(' ', column - 1)}^";
        Assert.AreEqual(message[..511], failure.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_UrlPastCurlsMessageBuffer_IsCutTo511CharactersAsUpstreamTest761Expects()
    {
        string url = "http://testingthis/" + string.Concat(Enumerable.Repeat("{a}b", 201));

        Assert.IsFalse(UrlGlob.TryParse(url, out _, out TransferResult? failure));

        string expected = "too many {} sets in position 403:\nhttp://testingthis/" + string.Concat(Enumerable.Repeat("{a}b", 114)) + "{a";
        Assert.AreEqual(expected, failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("http://testingthis/", "{a}b", 127, "http://testingthis/")]
    [DataRow("http://t/", "{a}", 130, "http://t/")]
    public void TryParse_255PiecesOrFewer_Parses(string prefix, string repeated, int copies, string expectedPrefix)
    {
        string url = prefix + string.Concat(Enumerable.Repeat(repeated, copies));

        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out _));

        string expanded = glob.Expand().Single().Url;
        Assert.StartsWith(expectedPrefix, expanded);
        Assert.DoesNotContain("{", expanded);
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
    [DataRow("file:///n/{a?b,c*d,e:f,g\"h,i<j,k>l,m|n,q/r}", "o_#1", "o_a_b|o_c_d|o_e:f|o_g_h|o_i_j|o_k_l|o_m_n|o_q/r")]
    [DataRow("file:///n/{CON,a.,b%20,a%3Fb}", "o_#1", "o_CON|o_a.|o_b%20|o_a%3Fb")]
    [DataRow("file:///n/{a,b}", "o?_#1", "o__a|o__b")]
    [DataRow("file:///n/a", "o?x", "o_x")]
    [DataRow("file:///n/a", "o\u0001\u001Fx", "o__x")]
    [DataRow("file:///n/{a}", "o\t\u007Fx\u00E9 #1", "o_\u007Fx\u00E9 a")]
    [DataRow("file:///n/a", "\\\\?\\C:\\tmp\\a?b", "\\\\?\\C:\\tmp\\a_b")]
    [DataRow("file:///n/a", "\\\\srv\\a?b", "\\\\srv\\a_b")]
    [DataRow("file:///n/a", "a\\\\b\\c", "a\\\\b\\c")]
    public void ResolveOutputFileName_OnWindows_IsSanitizedAsCurlSanitizesIt(string url, string outputFileName, string expectedNames)
    {
        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out _));

        CollectionAssert.AreEqual(
            expectedNames.Split('|'),
            glob.Expand().Select(match => match.ResolveOutputFileName(outputFileName, sanitizesForWindows: true)).ToArray());
    }

    [TestMethod]
    public void ResolveOutputFileName_OnWindows_HasNoLengthLimit()
    {
        string name = new('a', 40000);
        Assert.IsTrue(UrlGlob.TryParse("file:///n/a", out UrlGlob? glob, out _));

        Assert.AreEqual(name, glob.Expand().Single().ResolveOutputFileName(name, sanitizesForWindows: true));
    }

    [TestMethod]
    public void ResolveOutputFileName_OffWindows_IsOnlySubstituted()
    {
        Assert.IsTrue(UrlGlob.TryParse("file:///n/{a?b}", out UrlGlob? glob, out _));

        Assert.AreEqual("o?_a?b", glob.Expand().Single().ResolveOutputFileName("o?_#1", sanitizesForWindows: false));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ResolveOutputFileName_GlobOff_IsTheNameAsWritten(bool sanitizesForWindows)
    {
        UrlGlobMatch match = UrlGlob.Unglobbed("file:///n/[1-2]").Expand().Single();

        Assert.AreEqual("o?x#1", match.ResolveOutputFileName("o?x#1", sanitizesForWindows));
    }

    [TestMethod]
    public void ResolveOutputFileName_NullName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlGlob.Unglobbed("x").Expand().Single().ResolveOutputFileName(null!, sanitizesForWindows: true));

    [TestMethod]
    public void SubstituteGlobValues_NullName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlGlob.Unglobbed("x").Expand().Single().SubstituteGlobValues(null!));

    [TestMethod]
    public void Expand_NamedGlobs_ExpandLikeUnnamedOnes()
    {
        Assert.IsTrue(UrlGlob.TryParse("http://h/{<a>x,y}[<b>1-2]", out UrlGlob? named, out _));
        Assert.IsTrue(UrlGlob.TryParse("http://h/{x,y}[1-2]", out UrlGlob? unnamed, out _));

        CollectionAssert.AreEqual(
            unnamed.Expand().Select(match => match.Url).ToArray(),
            named.Expand().Select(match => match.Url).ToArray());
        Assert.AreEqual(4, named.UrlCount);
    }

    [TestMethod]
    [DataRow("#<a>-#<b>")]
    [DataRow("#1-#2")]
    [DataRow("#<a>-#2")]
    public void SubstituteGlobValues_NamedOrNumberedReference_IsTheGlobsValue(string outputFileName)
    {
        Assert.IsTrue(UrlGlob.TryParse("http://h/{<a>x,y}[<b>1-2]", out UrlGlob? glob, out _));

        Assert.AreEqual("x-1", glob.Expand().First().SubstituteGlobValues(outputFileName));
    }

    [TestMethod]
    [DataRow("http://h/{<bad x,y}", "http://h/<bad x|http://h/y")]
    [DataRow("http://h/{<>x,y}", "http://h/x|http://h/y")]
    public void Expand_BrokenOrEmptyGlobName_IsReadAsCurlReadsIt(string url, string expectedUrls)
    {
        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out _));

        CollectionAssert.AreEqual(expectedUrls.Split('|'), glob.Expand().Select(match => match.Url).ToArray());
    }

    [TestMethod]
    public void Expand_GlobNameOverSixtyFourCharacters_IsSetContent()
    {
        string name = new('n', 65);
        Assert.IsTrue(UrlGlob.TryParse($"http://h/{{<{name}>x,y}}", out UrlGlob? glob, out _));

        CollectionAssert.AreEqual(
            new[] { $"http://h/<{name}>x", "http://h/y" },
            glob.Expand().Select(match => match.Url).ToArray());
    }

    [TestMethod]
    public void SubstituteGlobValues_SixtyFourCharacterName_IsResolved()
    {
        string name = new('n', 64);
        Assert.IsTrue(UrlGlob.TryParse($"http://h/{{<{name}>x,y}}", out UrlGlob? glob, out _));

        CollectionAssert.AreEqual(
            new[] { "http://h/x", "http://h/y" },
            glob.Expand().Select(match => match.Url).ToArray());
        Assert.AreEqual("o_x", glob.Expand().First().SubstituteGlobValues($"o_#<{name}>"));
    }

    [TestMethod]
    public void TryParse_DuplicateGlobName_IsUrlMalformatAtCurlsPosition()
    {
        const string url = "https://dummy.example/{<test>A,B}{<test>C,D}";

        Assert.IsFalse(UrlGlob.TryParse(url, out _, out TransferResult? failure));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual($"Duplicate glob name in position 40:\n{url}\n{new string(' ', 39)}^", failure.ErrorMessage);
    }

    [TestMethod]
    public void TryResolveOutputFileName_UnknownGlobName_IsBadFunctionArgumentAtCurlsPosition()
    {
        Assert.IsTrue(UrlGlob.TryParse("http://h/{<a>x}", out UrlGlob? glob, out _));

        Assert.IsFalse(glob.Expand().Single().TryResolveOutputFileName(
            "somewhere/#<foo>", sanitizesForWindows: false, out string? fileName, out TransferResult? failure));

        Assert.IsNull(fileName);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure.ExitCode);
        Assert.AreEqual(
            $"no glob exists with this name in position 16:\nsomewhere/#<foo>\n{new string(' ', 15)}^",
            failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("somewhere/#<foo", false, "somewhere/#<foo")]
    [DataRow("o_#<a>_#1_#9", false, "o_x_x_#9")]
    [DataRow("o?_#<a>", true, "o__x")]
    [DataRow("o_#<" + "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + ">", false, "o_#<nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn>")]
    public void TryResolveOutputFileName_KnownOrMalformedReference_IsResolved(string outputFileName, bool sanitizesForWindows, string expected)
    {
        Assert.IsTrue(UrlGlob.TryParse("http://h/{<a>x}", out UrlGlob? glob, out _));

        Assert.IsTrue(glob.Expand().Single().TryResolveOutputFileName(
            outputFileName, sanitizesForWindows, out string? fileName, out TransferResult? failure));

        Assert.IsNull(failure);
        Assert.AreEqual(expected, fileName);
    }

    [TestMethod]
    public void SubstituteGlobValues_UnknownGlobName_IsLeftAsWritten()
    {
        Assert.IsTrue(UrlGlob.TryParse("http://h/{<a>x}", out UrlGlob? glob, out _));

        Assert.AreEqual("o_#<foo>_x", glob.Expand().Single().SubstituteGlobValues("o_#<foo>_#<a>"));
    }

    [TestMethod]
    public void TryResolveOutputFileName_GlobOff_IsTheNameAsWritten()
    {
        UrlGlobMatch match = UrlGlob.Unglobbed("http://h/{<a>x}").Expand().Single();

        Assert.IsTrue(match.TryResolveOutputFileName("o?#<foo>", sanitizesForWindows: true, out string? fileName, out TransferResult? failure));

        Assert.IsNull(failure);
        Assert.AreEqual("o?#<foo>", fileName);
    }

    [TestMethod]
    public void TryResolveOutputFileName_NullName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlGlob.Unglobbed("x").Expand().Single().TryResolveOutputFileName(null!, sanitizesForWindows: true, out _, out _));
}
