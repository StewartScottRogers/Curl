using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTestFileExpander"/> against the preprocessing <c>runtests.pl</c> does at
/// <c>curl-8_21_0</c> (<c>prepro</c> in <c>runner.pm</c>, <c>subvariables</c> in <c>servers.pm</c>,
/// <c>subchars</c> and <c>subbase64</c> in <c>testutil.pm</c>).
/// </summary>
[TestClass]
public sealed class UpstreamTestFileExpanderTests
{
    private static readonly Dictionary<string, string> NoVariables = [];

    private static readonly HashSet<string> NoFeatures = [];

    [TestMethod]
    [DataRow("%TESTNUMBER", "TESTNUMBER", "1500")]
    [DataRow("%LOGDIR", "LOGDIR", @"C:\temp\case1500")]
    [DataRow("%HOSTIP", "HOSTIP", "127.0.0.1")]
    [DataRow("%HTTPPORT", "HTTPPORT", "8990")]
    [DataRow("%FTPPORT", "FTPPORT", "8992")]
    [DataRow("%SRVDIR", "SRVDIR", "/srv")]
    [DataRow("%PWD", "PWD", "/work")]
    [DataRow("%CLIENTIP", "CLIENTIP", "127.0.0.2")]
    public void Expand_ReplacesEachVariableWithItsValue(string written, string name, string value)
    {
        UpstreamTestFileExpansion expansion = Expand($"a {written} b\n", new Dictionary<string, string> { [name] = value });

        Assert.AreEqual($"a {value} b\n", Text(expansion));
        Assert.IsEmpty(expansion.UnknownVariables);
    }

    [TestMethod]
    public void Expand_ReplacesEveryOccurrenceIncludingInAttributes()
    {
        Dictionary<string, string> variables = new() { ["HOSTIP"] = "127.0.0.1", ["HTTPPORT"] = "8990", ["TESTNUMBER"] = "7", ["LOGDIR"] = "log" };

        UpstreamTestFileExpansion expansion = Expand("<file name=\"%LOGDIR/out%TESTNUMBER\">\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n", variables);

        Assert.AreEqual("<file name=\"log/out7\">\nhttp://127.0.0.1:8990/7\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_PrefersTheLongestMatchingName()
    {
        Dictionary<string, string> variables = new() { ["CLIENT6IP"] = "[::1]", ["CLIENT6IP-NB"] = "::1" };

        UpstreamTestFileExpansion expansion = Expand("%CLIENT6IP-NB %CLIENT6IP\n", variables);

        Assert.AreEqual("::1 [::1]\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_LeavesPercentThatStartsNoVariableAlone()
    {
        UpstreamTestFileExpansion expansion = Expand("-w '%{http_code}' http://x/a%20b 100% %\n", NoVariables);

        Assert.AreEqual("-w '%{http_code}' http://x/a%20b 100% %\n", Text(expansion));
        Assert.IsEmpty(expansion.UnknownVariables);
    }

    [TestMethod]
    public void Expand_MatchesNamesCaseSensitively()
    {
        UpstreamTestFileExpansion expansion = Expand("%hostip\n", new Dictionary<string, string> { ["HOSTIP"] = "127.0.0.1" });

        Assert.AreEqual("%hostip\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_DoesNotExpandAValueAgain()
    {
        UpstreamTestFileExpansion expansion = Expand("%PWD\n", new Dictionary<string, string> { ["PWD"] = "%HOSTIP", ["HOSTIP"] = "127.0.0.1" });

        Assert.AreEqual("%HOSTIP\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_InsertsValuesAsUtf8()
    {
        UpstreamTestFileExpansion expansion = Expand("%PWD\n", new Dictionary<string, string> { ["PWD"] = "C:\\Ünï" });

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("C:\\Ünï\n"), expansion.File.ToArray());
    }

    [TestMethod]
    public void Expand_IgnoresAnEmptyVariableName()
    {
        UpstreamTestFileExpansion expansion = Expand("%x\n", new Dictionary<string, string> { [""] = "never" });

        Assert.AreEqual("%x\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_ListsUnknownUpstreamVariablesOnceInOrderAndLeavesThemAsWritten()
    {
        UpstreamTestFileExpansion expansion = Expand("%SSHPORT %HOSTIP %SSHPORT %PROXYPORT\n", new Dictionary<string, string> { ["HOSTIP"] = "h" });

        Assert.AreEqual("%SSHPORT h %SSHPORT %PROXYPORT\n", Text(expansion));
        CollectionAssert.AreEqual(new[] { "%SSHPORT", "%PROXYPORT" }, expansion.UnknownVariables.ToArray());
    }

    [TestMethod]
    public void Expand_DoesNotListVariablesInDroppedLines()
    {
        UpstreamTestFileExpansion expansion = Expand("%if missing\n%SSHPORT\n%endif\n", NoVariables);

        Assert.AreEqual(string.Empty, Text(expansion));
        Assert.IsEmpty(expansion.UnknownVariables);
    }

    [TestMethod]
    public void Expand_KeepsTheIfBranchWhenTheFeatureIsPresent()
    {
        UpstreamTestFileExpansion expansion = Expand("a\n%if brotli\nyes\n%else\nno\n%endif\nb\n", NoVariables, "brotli");

        Assert.AreEqual("a\nyes\nb\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_KeepsTheElseBranchWhenTheFeatureIsAbsent()
    {
        UpstreamTestFileExpansion expansion = Expand("a\n%if brotli\nyes\n%else\nno\n%endif\nb\n", NoVariables);

        Assert.AreEqual("a\nno\nb\n", Text(expansion));
    }

    [TestMethod]
    [DataRow(false, "absent\n")]
    [DataRow(true, "")]
    public void Expand_NegatedIfHoldsWhenTheFeatureIsAbsent(bool featurePresent, string expected)
    {
        UpstreamTestFileExpansion expansion = Expand("%if !brotli\nabsent\n%endif\n", NoVariables, featurePresent ? ["brotli"] : []);

        Assert.AreEqual(expected, Text(expansion));
    }

    [TestMethod]
    [DataRow(new[] { "outer", "inner" }, "1\n2\n5\n")]
    [DataRow(new[] { "outer" }, "1\n3\n5\n")]
    [DataRow(new[] { "inner" }, "4\n5\n")]
    [DataRow(new string[0], "4\n5\n")]
    public void Expand_ResolvesNestedIfBlocks(string[] features, string expected)
    {
        const string File = "%if outer\n1\n  %if inner\n2\n  %else\n3\n  %endif\n%else\n4\n%endif\n5\n";

        UpstreamTestFileExpansion expansion = Expand(File, NoVariables, features);

        Assert.AreEqual(expected, Text(expansion));
    }

    [TestMethod]
    public void Expand_ElseInsideADroppedBlockStaysDropped()
    {
        UpstreamTestFileExpansion expansion = Expand("%if !x\n%if y\n1\n%else\n2\n%endif\n%endif\n3\n", NoVariables, "x");

        Assert.AreEqual("3\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_ReadsTheFeatureNameUpToTheFirstCharacterOutsideItsAlphabet()
    {
        UpstreamTestFileExpansion expansion = Expand("%if HTTP-2_x # comment\nkept\n%endif\n", NoVariables, "HTTP-2_x");

        Assert.AreEqual("kept\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_WrapsWholePartsSoTheParserSeesOnlyTheKeptOne()
    {
        const string File = "<testcase>\n<reply>\n%if brotli\n<data>\nbrotli\n</data>\n%else\n<data>\nplain\n</data>\n%endif\n</reply>\n</testcase>\n";

        UpstreamTestCaseParseResult result = Expand(File, NoVariables).Parse();

        Assert.IsTrue(result.IsParsed);
        UpstreamTestSection data = result.TestCase.FindAll("reply", "data").Single();
        Assert.AreEqual("plain\n", Encoding.Latin1.GetString(data.Content.Span));
    }

    [TestMethod]
    public void Expand_TreatsAnIfWithoutASpaceAsAnOrdinaryLine()
    {
        UpstreamTestFileExpansion expansion = Expand("%ifdef x\n", NoVariables);

        Assert.AreEqual("%ifdef x\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_LeavesAnIfStillOpenAtTheEndWithoutAnError()
    {
        UpstreamTestFileExpansion expansion = Expand("%if x\nkept\n", NoVariables, "x");

        Assert.AreEqual("kept\n", Text(expansion));
        Assert.IsNull(expansion.ConditionError);
    }

    [TestMethod]
    [DataRow("%else", "%else on line 2 has no %if.")]
    [DataRow("%endif", "%endif on line 2 has no %if.")]
    public void Expand_StopsAtAStrayElseOrEndif(string directive, string error)
    {
        UpstreamTestFileExpansion expansion = Expand($"a\n{directive}\nb\n", NoVariables);

        Assert.AreEqual("a\n", Text(expansion));
        Assert.AreEqual(error, expansion.ConditionError);
    }

    [TestMethod]
    public void Expand_ReplacesTheCharacterMacros()
    {
        UpstreamTestFileExpansion expansion = Expand("a%SPb%TABc%CR%LTd%GT%AMP\n", NoVariables);

        Assert.AreEqual("a b\tc\r<d>&\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_EncodesBase64AfterSubstitutingVariablesAndPercentPairs()
    {
        UpstreamTestFileExpansion expansion = Expand("%b64[%HTTPPORT %9a]b64% %B64[x]B64%\n", new Dictionary<string, string> { ["HTTPPORT"] = "8990" });

        Assert.AreEqual($"{Convert.ToBase64String([.. "8990 "u8, 0x9a])} eA==\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_DecodesHexAndKeepsOtherCharactersAsWritten()
    {
        UpstreamTestFileExpansion expansion = Expand("%hex[%00%01%FFz%4]hex%\n", NoVariables);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0xFF, (byte)'z', (byte)'%', (byte)'4', (byte)'\n' }, expansion.File.ToArray());
    }

    [TestMethod]
    public void Expand_LeavesAnUnclosedInstructionAsWritten()
    {
        UpstreamTestFileExpansion expansion = Expand("%hex[%41\n", NoVariables);

        Assert.AreEqual("%hex[%41\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_RepeatsContent()
    {
        UpstreamTestFileExpansion expansion = Expand("[%repeat[3 x hi%21]%][%REPEAT[2 X a]%][%repeat[0 x a]%]\n", NoVariables);

        Assert.AreEqual("[hi!hi!hi!][aa][]\n", Text(expansion));
    }

    [TestMethod]
    [DataRow("%repeat[x x a]%")]
    [DataRow("%repeat[3 y a]%")]
    [DataRow("%repeat[3 x a]")]
    [DataRow("%repeat[99999999999 x a]%")]
    [DataRow("%repeat[3")]
    public void Expand_LeavesAMalformedRepeatAsWritten(string written)
    {
        UpstreamTestFileExpansion expansion = Expand(written + "\n", NoVariables);

        Assert.AreEqual(written + "\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_LeavesARepeatCutOffAtTheEndOfTheFileAsWritten()
    {
        UpstreamTestFileExpansion expansion = Expand("%repeat[12", NoVariables);

        Assert.AreEqual("%repeat[12", Text(expansion));
    }

    [TestMethod]
    public void Expand_RepeatsContentOnAFinalLineWithoutALineFeed()
    {
        UpstreamTestFileExpansion expansion = Expand("%repeat[2 x a]%", NoVariables);

        Assert.AreEqual("aa", Text(expansion));
    }

    [TestMethod]
    public void Expand_LeavesARepeatThatWouldCrossALineFeedFromHexAsWritten()
    {
        UpstreamTestFileExpansion expansion = Expand("%repeat[2 x %hex[%0a]hex%]%\n", NoVariables);

        Assert.AreEqual("%repeat[2 x \n]%\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_SkipsAMalformedRepeatToReachALaterOne()
    {
        UpstreamTestFileExpansion expansion = Expand("%repeat[q] %repeat[2 x b]%\n", NoVariables);

        Assert.AreEqual("%repeat[q] bb\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_ListsUnsupportedInstructionsOnceAndLeavesThemAsWritten()
    {
        const string File = "%days[400]\n%DAYS[1]\n%include log/x%\n%includetext log/y%\n%sha256b64file[a]sha256b64file%\n%strippemfile[b]strippemfile%\n";

        UpstreamTestFileExpansion expansion = Expand(File, NoVariables);

        Assert.AreEqual(File, Text(expansion));
        CollectionAssert.AreEqual(new[] { "%days", "%include", "%includetext", "%sha256b64file", "%strippemfile" }, expansion.UnsupportedInstructions.ToArray());
    }

    [TestMethod]
    public void Expand_MatchesIncludeCaseSensitively()
    {
        UpstreamTestFileExpansion expansion = Expand("%INCLUDE x%\n%INCLUDETEXT y%\n", NoVariables);

        Assert.IsEmpty(expansion.UnsupportedInstructions);
    }

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    public void Expand_IncludeIsReplacedByTheFileBytesAndConsumesTheLineBreak(string lineBreak)
    {
        Dictionary<string, byte[]> files = new() { ["log/5/a.txt"] = Encoding.Latin1.GetBytes("raw\r\nbytes\xff") };

        UpstreamTestFileExpansion expansion = ExpandWithFiles($"<data>\n%include %LOGDIR/a.txt%{lineBreak}</data>\n", files);

        Assert.AreEqual("<data>\nraw\r\nbytes\xff</data>\n", Text(expansion));
        Assert.IsEmpty(expansion.UnsupportedInstructions);
    }

    [TestMethod]
    public void Expand_IncludeTextTurnsCrlfIntoLfAndSubstitutesTheFileVariables()
    {
        Dictionary<string, byte[]> files = new() { ["log/5/t.txt"] = Encoding.Latin1.GetBytes("test %TESTNUMBER\r\nend\rx\r\n") };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("%includetext %LOGDIR/t.txt%\r\n", files);

        Assert.AreEqual("test 5\nend\rx\n", Text(expansion));
        Assert.IsEmpty(expansion.UnsupportedInstructions);
    }

    [TestMethod]
    public void Expand_IncludeRunsAfterMacrosAndBase64SoTheIncludedBytesAreNotExpanded()
    {
        Dictionary<string, byte[]> files = new() { ["m"] = Encoding.Latin1.GetBytes("%SP %b64[a]b64% %TESTNUMBER %include m%\n") };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("%b64[a]b64%%SP%include m%\n", files);

        Assert.AreEqual("YQ== %SP %b64[a]b64% %TESTNUMBER %include m%\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_IncludeTextBytesGoThroughMacrosButNotAnotherIncludeText()
    {
        Dictionary<string, byte[]> files = new() { ["t"] = Encoding.Latin1.GetBytes("a%SPb %includetext t%\n") };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("%includetext t%\n", files);

        Assert.AreEqual("a b %includetext t%\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_MissingFileIsIncludedAsNothing()
    {
        UpstreamTestFileExpansion expansion = ExpandWithFiles("a\n%include gone%\n%includetext gone%\nb\n", []);

        Assert.AreEqual("a\nb\n", Text(expansion));
        Assert.IsEmpty(expansion.UnsupportedInstructions);
    }

    [TestMethod]
    public void Expand_IncludeWithoutALineBreakAfterIt_IsLeftAsWritten()
    {
        Dictionary<string, byte[]> files = new() { ["x"] = "X"u8.ToArray(), ["y"] = "Y"u8.ToArray() };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("%include x% %include y%\r\r\n%include x%", files);

        Assert.AreEqual("%include x% Y%include x%", Text(expansion));
    }

    [TestMethod]
    public void Expand_IncludeWithNoClosingPercent_IsLeftAsWritten()
    {
        UpstreamTestFileExpansion expansion = ExpandWithFiles("%include x\n%includetext y\n", new() { ["x\n"] = "X"u8.ToArray() });

        Assert.AreEqual("%include x\n%includetext y\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_IncludePathIsGivenToTheReaderAsUtf8Text()
    {
        List<string> paths = [];

        UpstreamTestFileExpander.Expand(Encoding.Latin1.GetBytes("%include %LOGDIR/f%\n"), new Dictionary<string, string> { ["LOGDIR"] = "d\u00e9" }, NoFeatures, path =>
        {
            paths.Add(path);
            return null;
        });

        CollectionAssert.AreEqual(new[] { "d\u00e9/f" }, paths);
    }

    [TestMethod]
    public void Expand_WithoutAReader_ListsEachIncludeOnce()
    {
        UpstreamTestFileExpansion expansion = Expand("%includetext a%\n%include b%\n%includetext c% %include d%\n", NoVariables);

        CollectionAssert.AreEqual(new[] { "%includetext", "%include" }, expansion.UnsupportedInstructions.ToArray());
    }

    [TestMethod]
    public void Expand_Sha256b64fileIsTheBase64Sha256OfTheFileBytesInEitherCase()
    {
        // SHA-256 of "abc" is ba7816bf…15ad, standard base64 with no line breaks.
        Dictionary<string, byte[]> files = new() { ["log/5/k.pub"] = "abc"u8.ToArray() };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("pin %sha256b64file[%LOGDIR/k.pub]sha256b64file% %SHA256B64FILE[%LOGDIR/k.pub]SHA256B64FILE%\n", files);

        Assert.AreEqual("pin ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0= ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0=\n", Text(expansion));
        Assert.IsEmpty(expansion.UnsupportedInstructions);
    }

    [TestMethod]
    public void Expand_FileInstructionPathHasItsPercentPairsDecodedBeforeTheRead()
    {
        List<string> paths = [];

        UpstreamTestFileExpander.Expand(Encoding.Latin1.GetBytes("%sha256b64file[log%2F5/k%2epub]sha256b64file%%strippemfile[a%2Fb]strippemfile%\n"), NoVariables, NoFeatures, path =>
        {
            paths.Add(path);
            return null;
        });

        CollectionAssert.AreEqual(new[] { "log/5/k.pub", "a/b" }, paths);
    }

    [TestMethod]
    public void Expand_MissingFileForAFileInstructionCountsAsEmpty()
    {
        // SHA-256 of no bytes is e3b0c442…b855.
        UpstreamTestFileExpansion expansion = ExpandWithFiles("[%sha256b64file[gone]sha256b64file%][%strippemfile[gone]strippemfile%]\n", []);

        Assert.AreEqual("[47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=][]\n", Text(expansion));
    }

    // get_file_content over the LF file
    //   head\n-----BEGIN A-----\nAAA\n-----END A-----\nmid\n-----BEGIN B-----\nBBB\n-----END B-----\ntail\n
    // 1. s/(^|-----END .*?-----[\r\n]?)(.*?)(-----BEGIN .*?-----|$)/$1$3/gs drops "head\n" (from ^ to
    //    BEGIN A), "mid\n" (after "END A-----\n" to BEGIN B) and "tail" (after "END B-----\n" to the
    //    $ before the final \n), leaving
    //    -----BEGIN A-----\nAAA\n-----END A-----\n-----BEGIN B-----\nBBB\n-----END B-----\n\n
    // 2. CRLF to LF changes nothing; 3. chomp drops the last \n.
    // Over the same file with CRLF line ends, [\r\n]? takes only the \r after each END line, so
    // 1. leaves -----BEGIN A-----\r\nAAA\r\n-----END A-----\r-----BEGIN B-----\r\nBBB\r\n-----END B-----\r\n
    //    (the \r before the final \n, then that \n);
    // 2. turns each \r\n into \n, leaving the lone \r after END A; 3. chomp drops the last \n.
    [TestMethod]
    [DataRow("\n", "-----BEGIN A-----\nAAA\n-----END A-----\n-----BEGIN B-----\nBBB\n-----END B-----\n")]
    [DataRow("\r\n", "-----BEGIN A-----\nAAA\n-----END A-----\r-----BEGIN B-----\nBBB\n-----END B-----")]
    public void Expand_StrippemfileKeepsOnlyThePemBlocksAsGetFileContentDoes(string lineEnd, string expected)
    {
        string pem = string.Join(lineEnd, "head", "-----BEGIN A-----", "AAA", "-----END A-----", "mid", "-----BEGIN B-----", "BBB", "-----END B-----", "tail", string.Empty);
        Dictionary<string, byte[]> files = new() { ["log/5/c.pem"] = Encoding.Latin1.GetBytes(pem) };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("<%strippemfile[%LOGDIR/c.pem]strippemfile%>\n", files);

        Assert.AreEqual($"<{expected}>\n", Text(expansion));
        Assert.IsEmpty(expansion.UnsupportedInstructions);
    }

    [TestMethod]
    public void Expand_TwoStrippemfileInstructionsOnOneLineAreBothReplaced()
    {
        Dictionary<string, byte[]> files = new()
        {
            ["a"] = "x\n-----BEGIN K-----\nk\n-----END K-----\n"u8.ToArray(),
            ["b"] = "-----BEGIN C-----\nc\n-----END C-----\ny"u8.ToArray(),
        };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("%STRIPPEMFILE[a]STRIPPEMFILE%|%strippemfile[b]strippemfile%\n", files);

        Assert.AreEqual("-----BEGIN K-----\nk\n-----END K-----|-----BEGIN C-----\nc\n-----END C-----\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_FileInstructionsRunAfterIncludeSoIncludedOnesAreReplaced()
    {
        Dictionary<string, byte[]> files = new() { ["i"] = "%sha256b64file[k]sha256b64file%\n"u8.ToArray(), ["k"] = "abc"u8.ToArray() };

        UpstreamTestFileExpansion expansion = ExpandWithFiles("%include i%\n", files);

        Assert.AreEqual("ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0=\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_WithoutAReader_ListsEachFileInstructionOnceInEitherCase()
    {
        UpstreamTestFileExpansion expansion = Expand("%STRIPPEMFILE[a]STRIPPEMFILE%\n%Sha256B64File[b]sha256b64file% %strippemfile[c]strippemfile%\n", NoVariables);

        CollectionAssert.AreEqual(new[] { "%strippemfile", "%sha256b64file" }, expansion.UnsupportedInstructions.ToArray());
    }

    [TestMethod]
    public void Expand_KeepsAFinalLineWithoutALineFeed()
    {
        UpstreamTestFileExpansion expansion = Expand("a\nb", NoVariables);

        Assert.AreEqual("a\nb", Text(expansion));
    }

    [TestMethod]
    public void Expand_RejectsNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestFileExpander.Expand([], null!, NoFeatures));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestFileExpander.Expand([], NoVariables, null!));
    }

    [TestMethod]
    public void Expansion_RejectsNullLists()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamTestFileExpansion(Array.Empty<byte>(), null!, [], null));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamTestFileExpansion(Array.Empty<byte>(), [], null!, null));
    }

    private static UpstreamTestFileExpansion Expand(string file, IReadOnlyDictionary<string, string> variables, params string[] features) =>
        UpstreamTestFileExpander.Expand(Encoding.Latin1.GetBytes(file), variables, features.ToHashSet(StringComparer.Ordinal));

    private static UpstreamTestFileExpansion ExpandWithFiles(string file, Dictionary<string, byte[]> files) =>
        UpstreamTestFileExpander.Expand(
            Encoding.Latin1.GetBytes(file),
            new Dictionary<string, string> { ["LOGDIR"] = "log/5", ["TESTNUMBER"] = "5" },
            NoFeatures,
            path => files.GetValueOrDefault(path));

    private static string Text(UpstreamTestFileExpansion expansion) => Encoding.Latin1.GetString(expansion.File.Span);
}
