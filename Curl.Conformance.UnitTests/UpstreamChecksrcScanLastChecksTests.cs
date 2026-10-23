using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamChecksrcScanLastChecks"/> against checksrc.pl's <c>scanfile</c> lines 900-1268
/// (curl-8_22_0): each warning's text and column, the banned-function loop and the preprocessor lines (BL-3418).
/// Each test line runs the last checks alone; a line starting with <c>#</c> is a preprocessor line.
/// </summary>
[TestClass]
public sealed class UpstreamChecksrcScanLastChecksTests
{
    [TestMethod]
    [DataRow("f(a,b);", "1:5: warning: comma without following space (COMMANOSPACE)\n f(a,b);\n     ^\n")]
    [DataRow("f('a', b,c);", "1:10: warning: comma without following space (COMMANOSPACE)\n")]
    [DataRow("  a;\n  }", "2:3: warning: Suspicious close brace indentation (CLOSEBRACE)\n   a;\n   ^\n")]
    [DataRow("  } else {", "1:3: warning: else after closing brace on same line (BRACEELSE)\n")]
    [DataRow("if(a){", "1:6: warning: missing space after close paren (PARENBRACE)\n")]
    [DataRow("\n{", "2:1: warning: empty line before open brace (EMPTYLINEBRACE)\n {\n ^\n")]
    [DataRow("a = 1 ;", "1:6: warning: no space before semicolon (SPACESEMICOLON)\n")]
    [DataRow("  case 1 :", "1:9: warning: no space before colon of switch label (SPACESWITCHCOLON)\n")]
    [DataRow(" out:", "1:4: warning: no space before label (SPACEBEFORELABEL)\n")]
    [DataRow("  curlx_free(p);\n  p = NULL;", "2:3: warning: replace curlx_free() + NULL assignment with curlx_safefree() (USESAFEFREE)\n")]
    [DataRow("  curlx_free(p);\n0\n  p = NULL;", "3:3: warning: replace curlx_free() + NULL assignment with curlx_safefree() (USESAFEFREE)\n")]
    [DataRow("  curlx_free(p);\n\n  p = NULL;", "3:3: warning: replace curlx_free() + NULL assignment with curlx_safefree() (USESAFEFREE)\n")]
    [DataRow("  p = malloc(n) + strcpy(a, b);", "1:7: warning: use of malloc is banned (BANNEDFUNC)\n")]
    [DataRow("  p = malloc(n) + strcpy(a, b);", "1:19: warning: use of strcpy is banned (BANNEDFUNC)\n")]
    [DataRow("x = foo(free(p));", "1:9: warning: use of free is banned (BANNEDFUNC)\n")]
    [DataRow("  f = curlx_fopen(name, \"r\");", "1:7: warning: use of non-binary fopen without FOPEN_* macro: r (FOPENMODE)\n")]
    [DataRow("if(a)\n  {", "2:3: warning: badly placed open brace (BRACEPOS)\n")]
    [DataRow("  if(a) {\n  b;", "2:3: warning: not indented 2 steps (uses 0) (INDENTATION)\n")]
    [DataRow("if(f(a))\n    b;", "2:5: warning: not indented 2 steps (uses 4) (INDENTATION)\n")]
    [DataRow("char * name;", "1:7: warning: space after declarative asterisk (ASTERISKSPACE)\n")]
    [DataRow("char* name;", "1:5: warning: no space before asterisk (ASTERISKNOSPACE)\n")]
    [DataRow("static int f(void) {", "1:20: warning: wrongly placed open brace (BRACEPOS)\n")]
    [DataRow("a =b;", "1:4: warning: no space after equals sign (EQUALSNOSPACE)\n")]
    [DataRow("a= b;", "1:2: warning: no space before equals sign (NOSPACEEQUALS)\n")]
    [DataRow("a =  b;", "1:4: warning: more than one space after equals sign (EQUALSPACE)\n")]
    [DataRow("a = b +c;", "1:7: warning: no space after plus sign (PLUSNOSPACE)\n")]
    [DataRow("a = b+ c;", "1:6: warning: no space before plus sign (NOSPACEPLUS)\n")]
    [DataRow("a;b;", "1:3: warning: no space after semicolon (SEMINOSPACE)\n")]
    [DataRow("typedef struct a {", "1:2: warning: typedef'ed struct (TYPEDEFSTRUCT)\n")]
    [DataRow("if(! a)", "1:5: warning: space after exclamation mark (EXCLAMATIONSPACE)\n")]
    [DataRow("(void)!f();", "1:2: warning: exclamation after (void) is weird (VOIDEXCL)\n")]
    [DataRow("e = EINTR;", "1:5: warning: use of bare errno define EINTR, use SOCKEINTR (ERRNOVAR)\n")]
    [DataRow("if(a)  {", "1:7: warning: multiple spaces (MULTISPACE)\n")]
    [DataRow("#ifdef WIN32", "1:8: warning: use of WIN32 is banned from preprocessor lines, use _WIN32 instead (BANNEDPREPROC)\n")]
    public void Check_ALineBreakingARule_WarnsAtItsColumn(string lines, string expected) =>
        Assert.Contains("f.c:" + expected, Scan(lines));

    [TestMethod]
    [DataRow("x = '#',y;")]
    [DataRow("a(/*,b")]
    [DataRow("s(\"a,b\");")]
    [DataRow("c = ',';")]
    [DataRow("    a;\n  }")]
    [DataRow("  }\n  }")]
    [DataRow("\n}")]
    [DataRow("int f(void)\n{")]
    [DataRow(" default:")]
    [DataRow("a ?\n out:")]
    [DataRow("  curlx_free(p);\nx;\n  p = NULL;")]
    [DataRow("s->malloc(n);")]
    [DataRow("  f = curlx_fopen(name, \"rb\");")]
    [DataRow("#if f(a)\n  {")]
    [DataRow("#if(a) {\n b;")]
    [DataRow("if(a))\n    b;")]
    [DataRow("  else\n")]
    [DataRow("else\n  b;")]
    [DataRow("char * const p;")]
    [DataRow("struct a {")]
    [DataRow("a = b;")]
    [DataRow("a++;")]
    [DataRow("e = SOCKEINTR;")]
    [DataRow("  s(\"a\")  {")]
    [DataRow("#ifdef _WIN32")]
    [DataRow("x = WIN32;")]
    public void Check_ALineTheScriptAccepts_WarnsNothing(string lines) =>
        Assert.DoesNotContain("f.c:", Scan(lines));

    [TestMethod]
    public void Check_BannedFunctions_TheLineIsRestoredForTheLaterChecks()
    {
        UpstreamChecksrcRun run = new([]);
        UpstreamChecksrcRun.FileScan scan = new("f.c");
        scan.StartLine("  p = malloc(n);");

        run.CheckLineSecondHalf(scan);

        Assert.AreEqual("  p = malloc(n);", scan.Text);
    }

    [TestMethod]
    public void Check_StderrEnabledByTheLocalChecksrc_WarnsOutsidePreprocessorText()
    {
        string root = Path.Combine(Path.GetTempPath(), "checksrc-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, ".checksrc"), "enable STDERR\n", Encoding.Latin1);
            File.WriteAllText(Path.Combine(root, "code.c"), "  fputs(s, stderr);\nx;\n", Encoding.Latin1);
            UpstreamChecksrcRun run = new([$"-D{root}", "code.c"]);

            string first = run.Run().Output;
            UpstreamChecksrcRun.FileScan scan = new("g.c");
            scan.StartLine("# a(stderr);");
            run.CheckLineSecondHalf(scan);
            string second = run.Run().Output;

            Assert.Contains("code.c:1:12: warning: use of stderr is banned (use tool_stderr instead) (STDERR)\n", first);
            Assert.DoesNotContain("code.c:2:", first);
            Assert.DoesNotContain("g.c:", second);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Check_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamChecksrcScanLastChecks.Check(null!, new("f.c")));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamChecksrcScanLastChecks.Check(new([]), null!));
    }

    private static string Scan(string lines)
    {
        UpstreamChecksrcRun run = new([]);
        UpstreamChecksrcRun.FileScan scan = new("f.c");
        foreach (string line in lines.Split('\n'))
        {
            scan.StartLine(line);
            scan.Preprocessor = line.StartsWith('#');
            run.CheckLineSecondHalf(scan);
            scan.EndLine();
        }

        return run.Run().Output;
    }
}
