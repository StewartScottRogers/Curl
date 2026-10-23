using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamChecksrcScanFirstChecks"/> against checksrc.pl's <c>scanfile</c> lines 532-900
/// (curl-8_22_0): each warning's text and column, the comment stripping and the preprocessor lines (BL-3417).
/// </summary>
[TestClass]
public sealed class UpstreamChecksrcScanFirstChecksTests
{
    [TestMethod]
    [DataRow("a\tb;", 1, "1:2: error: Contains TAB character (TABS)\n a\tb;\n  ^\n")]
    [DataRow("int a; ", 1, "1:7: warning: Trailing whitespace (TRAILINGSPACE)\n int a; \n       ^\n")]
    [DataRow("/*x */", 1, "1:3: warning: Missing space after comment start (COMMENTNOSPACESTART)\n /*x */\n   ^\n")]
    [DataRow("/* x*/", 1, "1:5: warning: Missing space before comment end (COMMENTNOSPACEEND)\n /* x*/\n     ^\n")]
    [DataRow("/* TODO */", 1, "1:4: warning: Avoid TODO comments. Add to documentation instead (FIXME)\n /* TODO */\n    ^\n")]
    [DataRow("a = 1; // c", 1, "1:8: warning: // comment (CPPCOMMENTS)\n a = 1; // c\n        ^\n")]
    [DataRow("#if defined(FOO)", 1, "1:13: warning: use ifdef/ifndef for single macro checks (IFDEFSINGLE)\n")]
    [DataRow("  #define X", 1, "1:1: warning: space before preprocessor hash (CPPSPACE)\n   #define X\n ^\n")]
    [DataRow("  if (a)", 1, "1:5: warning: if with space (SPACEBEFOREPAREN)\n   if (a)\n     ^\n")]
    [DataRow("  while (*p)", 1, "1:8: warning: while with space (SPACEBEFOREPAREN)\n")]
    [DataRow("x = foo (a);", 1, "1:8: warning:  foo with space (SPACEBEFOREPAREN)\n")]
    [DataRow("  if(a == NULL)", 1, "1:8: warning: we prefer !variable instead of \"== NULL\" comparisons (EQUALSNULL)\n")]
    [DataRow("  if(a != 0)", 1, "1:7: warning: we prefer if(rc) instead of \"rc != 0\" comparisons (NOTEQUALSZERO)\n")]
    [DataRow("  do{", 1, "1:5: warning: one space after do before brace (DOBRACE)\n")]
    [DataRow("  }while(a);", 1, "1:5: warning: one space between brace and while (BRACEWHILE)\n")]
    [DataRow("  if(a = b)", 1, "1:7: warning: assignment within conditional expression (ASSIGNWITHINCONDITION)\n")]
    [DataRow("  if(a) b();", 1, "1:12: warning: conditional block on the same line (ONELINECONDITION)\n")]
    [DataRow("f( a);", 1, "1:3: warning: space after open parenthesis (SPACEAFTERPAREN)\n")]
    [DataRow("a&&b;", 2, "1:2: warning: missing space before Logical AND (NOSPACEAND)\n")]
    [DataRow("a&&b;", 2, "1:4: warning: missing space after Logical AND (NOSPACEAND)\n")]
    [DataRow("x = a ? b: c;", 1, "1:10: warning: missing space before colon (NOSPACEC)\n")]
    [DataRow("x = a ? b :c;", 1, "1:12: warning: missing space after colon (NOSPACEC)\n")]
    [DataRow("x = a? b : c;", 1, "1:6: warning: missing space before question mark (NOSPACEQ)\n")]
    [DataRow("x = a ?b : c;", 1, "1:8: warning: missing space after question mark (NOSPACEQ)\n")]
    [DataRow("x = a< b;", 1, "1:6: warning: missing space before less or greater than (NOSPACETHAN)\n")]
    [DataRow("x = a <b;", 1, "1:7: warning: missing space after less or greater than (NOSPACETHAN)\n")]
    [DataRow("f(a );", 1, "1:5: warning: space before close parenthesis (SPACEBEFORECLOSE)\n")]
    [DataRow("f(a , b);", 1, "1:5: warning: space before comma (SPACEBEFORECOMMA)\n")]
    [DataRow(" return(a);", 1, "1:8: warning: return without space before paren (RETURNNOSPACE)\n")]
    [DataRow("  return (a);", 1, "1:10: warning: return with paren (RETURNPAREN)\n")]
    [DataRow("n = sizeof x;", 1, "1:11: warning: sizeof without parenthesis (SIZEOFNOPAREN)\n")]
    public void Check_ALineBreakingARule_WarnsAtItsColumn(string line, int warnings, string expected)
    {
        string output = Scan(line);

        Assert.Contains("f.c:" + expected, output);
        Assert.HasCount(warnings, Regex.Matches(output, @"f.c:[0-9]+:[0-9]+: (warning|error): "));
    }

    [TestMethod]
    [DataRow("  if(a) {")]
    [DataRow("  if(f(a) b")]
    [DataRow("  if(a)0")]
    [DataRow("  if(b) c('#');")]
    [DataRow("  do {")]
    [DataRow("  } while(a);")]
    [DataRow("  return (a ? b : c);")]
    [DataRow("  case (1):")]
    [DataRow("typedef int (*fn)(void);")]
    [DataRow("typedef int (fn)(void);")]
    [DataRow("a('#') foo (b);")]
    [DataRow("c = a ? b : ':';")]
    [DataRow("c = '?';")]
    [DataRow("c = '#'; return(a);")]
    [DataRow("c = '#'; n = sizeof x;")]
    [DataRow("n = sizeof(x);")]
    [DataRow("s = \"a?b a<b\";")]
    [DataRow("u = \" https://example.com/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\";")]
    public void Check_ALineTheScriptAccepts_WarnsNothing(string line) =>
        Assert.DoesNotContain("f.c:", Scan(line));

    [TestMethod]
    public void Check_ALongLine_WarnsAtItsEndWithoutACaret()
    {
        string line = "x = " + new string('a', 76) + ";";

        Assert.Contains($"f.c:1:82: warning: Longer than 79 columns (LONGLINE)\n {line}\n", Scan(line));
    }

    [TestMethod]
    public void Check_TheSameIncludeTwice_WarnsAtTheSecond()
    {
        string output = Scan("#include \"a.h\"", "#include <a.h>", "#include \"a.h\"", "#include \"b.h\"");

        Assert.Contains("f.c:3:10: warning: duplicated include (INCLUDEDUP)\n #include \"a.h\"\n          ^\n", output);
        Assert.DoesNotContain("f.c:2:", output);
    }

    [TestMethod]
    public void Check_APreprocessorLineEndingInABackslash_SkipsTheCodeChecksOfTheNextLine()
    {
        string output = Scan("#define A \\", "  foo (a)", "#define B", "  foo (b)");

        Assert.DoesNotContain("f.c:2:", output);
        Assert.Contains("f.c:4:6: warning:  foo with space (SPACEBEFOREPAREN)\n   foo (b)\n", output);
    }

    [TestMethod]
    public void Check_Comments_AreStrippedBeforeTheCodeChecks()
    {
        UpstreamChecksrcRun run = new([]);
        UpstreamChecksrcRun.FileScan scan = new("f.c");

        string[] texts = [.. new[] { "/* a */ x; /* b", "still", "c */ y; /* d */ z; /* e */", "/* f", "g */" }
            .Select(line => CheckLine(run, scan, line))];

        CollectionAssert.AreEqual(new[] { "  x; ", "", "", "", "" }, texts);
        Assert.IsFalse(scan.InComment);
    }

    [TestMethod]
    public void Check_CopyrightStatements_RecordTheirYears()
    {
        UpstreamChecksrcRun run = new([]);
        UpstreamChecksrcRun.FileScan scan = new("f.c");

        _ = CheckLine(run, scan, " * Copyright (C) 2020 - 2026 Name");
        _ = CheckLine(run, scan, " * copyright by Name");
        _ = CheckLine(run, scan, " * Copyright");

        CollectionAssert.AreEqual(
            new UpstreamChecksrcRun.CopyrightYear[]
            {
                new(2020, 1, 17, " * Copyright (C) 2020 - 2026 Name"),
                new(2026, 1, 24, " * Copyright (C) 2020 - 2026 Name"),
                new(-1, 2, 0, " * copyright by Name"),
            },
            scan.Copyright);
    }

    [TestMethod]
    public void Check_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamChecksrcScanFirstChecks.Check(null!, new("f.c")));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamChecksrcScanFirstChecks.Check(new([]), null!));
    }

    private static string Scan(params string[] lines)
    {
        UpstreamChecksrcRun run = new([]);
        UpstreamChecksrcRun.FileScan scan = new("f.c");
        foreach (string line in lines)
        {
            _ = CheckLine(run, scan, line);
        }

        return run.Run().Output;
    }

    private static string CheckLine(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string line)
    {
        scan.StartLine(line);
        run.CheckLineFirstHalf(scan);
        string text = scan.Text;
        scan.EndLine();
        return text;
    }
}
