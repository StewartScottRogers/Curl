using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1560ChecksTests
{
    private const string PlainParts = "http | [11] | [12] | [13] | h | [15] | /p | q | f";

    [TestMethod]
    public void GetParts_MatchingRowsOnTheEngine_AnswerZero()
    {
        var run = new Run();

        int errors = run.Checks.GetParts(
        [
            new("http://h/p?q#f", PlainParts, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
            new("http://[fe80::1%25eth0]/", "http | [11] | [12] | [13] | [fe80::1] eth0 | [15] | / | [16] | [17]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
            new("http://[::1]/", "http | [11] | [12] | [13] | [::1] | [15] | / | [16] | [17]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
            new("http://h:99999/", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.BadPortNumber),
        ]);

        Assert.AreEqual(0, errors);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public void GetParts_PartsDiffer_WritesWantedAndGot()
    {
        var run = new Run();

        int errors = run.Checks.GetParts([new("http://h/p?q#f", "x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);

        Assert.AreEqual(1, errors);
        Assert.AreEqual($"in: http://h/p?q#f\nwanted: x\ngot:    {PlainParts}\n", run.Error.ToString());
    }

    [TestMethod]
    public void GetParts_CodeDiffers_WritesGetParts()
    {
        var run = new Run();

        int errors = run.Checks.GetParts([new("http://h:99999/", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);

        Assert.AreEqual(1, errors);
        Assert.AreEqual("Get parts\nin: http://h:99999/\nreturned 4 (expected 0)\n", run.Error.ToString());
    }

    [TestMethod]
    public void GetParts_WindowsAndPunycodeRows_RunOnlyWhereUpstreamRunsThem()
    {
        Lib1560UrlCase windows = new("http://h/", "x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, WindowsOnly: true);
        Lib1560UrlCase punycode = new("http://h/", "x", CurlUrlFlags.None, CurlUrlFlags.Punycode, CurlUrlCode.Ok);
        Lib1560UrlCase toIdn = new("http://h/", "x", CurlUrlFlags.None, CurlUrlFlags.PunycodeToIdn, CurlUrlCode.Ok);

        int skipped = new Run(isWindows: false).Checks.GetParts([windows, punycode, toIdn]);
        int windowsErrors = new Run(isWindows: true).Checks.GetParts([windows]);
        int utf8Errors = new Run(hasUtf8: true).Checks.GetParts([toIdn]);

        Assert.AreEqual(0, skipped);
        Assert.AreEqual(1, windowsErrors);
        Assert.AreEqual(1, utf8Errors);
    }

    [TestMethod]
    public void CheckParts_OkWithNoText_WritesZeroInBrackets()
    {
        var run = new Run(new FakeCurlEngine { CreatingUrl = () => new ScriptedUrlHandle() });

        int errors = run.Checks.GetParts([new("x", "[0] | [0] | [0] | [0] | [0] | [0] | [0] | [0] | [0]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);

        Assert.AreEqual(0, errors);
    }

    [TestMethod]
    public void GetUrls_EachOutcome_CountsAsUpstream()
    {
        var run = new Run();

        int matching = run.Checks.GetUrls(
        [
            new("HTTP://h/a", "http://h/a", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
            new("http://h:99999/", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.BadPortNumber),
            new("http://h/", "x", CurlUrlFlags.None, CurlUrlFlags.Punycode, CurlUrlCode.Ok),
        ]);
        int differing = run.Checks.GetUrls([new("http://h/a", "x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);
        int setFails = run.Checks.GetUrls([new("http://h:99999/", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);

        Assert.AreEqual(0, matching);
        Assert.AreEqual(1, differing);
        Assert.AreEqual(1, setFails);
        Assert.AreEqual("Org:    http://h/a\nWanted: x\nGot   : http://h/a\nGet URL\nin: http://h:99999/\nreturned 4 (expected 0)\n", run.Error.ToString());
    }

    [TestMethod]
    public void GetUrls_GetFails_WritesTheLineAndTheCode()
    {
        var run = new Run(new FakeCurlEngine { UrlGetResult = CurlUrlCode.NoHost });

        int errors = run.Checks.GetUrls([new("http://h/", "http://h/", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);

        Assert.AreEqual(2, errors);
        StringAssert.StartsWith(run.Error.ToString(), "lib1560.c:1879 returned 14 (");
        StringAssert.EndsWith(run.Error.ToString(), ". URL: 'http://h/'\nGet URL\nin: http://h/\nreturned 14 (expected 0)\n");
    }

    [TestMethod]
    public void SetGetParts_EachOutcome_CountsAsUpstream()
    {
        var run = new Run();

        int tables = run.Checks.SetGetParts(Lib1560Tables.SetGetParts);
        int fromNothing = run.Checks.SetGetParts([new(null, "scheme=http,host=h,path=/p,query=q,fragment=f,", PlainParts, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);
        int skipped = run.Checks.SetGetParts([new(null, "host=h,", "x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.Punycode, CurlUrlCode.Ok)]);
        int setFails = run.Checks.SetGetParts([new("http://h:99999/", "host=h,", "x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);
        int partFails = run.Checks.SetGetParts([new("http://h/", "port=abc,", "x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok)]);

        Assert.AreEqual(0, tables + fromNothing + skipped);
        Assert.AreEqual(1, setFails);
        Assert.AreEqual(1, partFails);
        Assert.AreEqual("Set parts\nin: http://h:99999/\nreturned 4 (expected 0)\nupdateurl\nin: port=abc,\nreturned 4 (expected 0)\n", run.Error.ToString());
    }

    [TestMethod]
    public void SetParts_EachOutcome_CountsAsUpstream()
    {
        var run = new Run();

        int matching = run.Checks.SetParts(
        [
            new("http://h/", "path=/x,", "http://h/x", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
            new(null, "scheme=http,host=h,", "http://h/", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
            new("http://h:99999/", "host=h,", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.BadPortNumber, CurlUrlCode.Ok),
            new("http://h/", "port=abc,", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.BadPortNumber),
        ]);
        int setFails = run.Checks.SetParts([new("http://h:99999/", "host=h,", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);
        int partFails = run.Checks.SetParts([new("http://h/", "port=abc,", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);

        Assert.AreEqual(0, matching);
        Assert.AreEqual(1, setFails);
        Assert.AreEqual(1, partFails);
        Assert.AreEqual("Set parts\nin: http://h:99999/\nreturned 4 (expected 0)\nupdateurl\nin: port=abc,\nreturned 4 (expected 0)\n", run.Error.ToString());
    }

    [TestMethod]
    public void SetParts_GetFails_WritesTheLine()
    {
        var run = new Run(new FakeCurlEngine { UrlGetResult = CurlUrlCode.NoHost });

        int errors = run.Checks.SetParts([new("http://h/", "path=/x,", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);

        Assert.AreEqual(1, errors);
        StringAssert.StartsWith(run.Error.ToString(), "lib1560.c:1839 Get URL returned 14 (");
    }

    [TestMethod]
    public void SetUrls_EachOutcome_CountsAsUpstream()
    {
        var run = new Run();

        int matching = run.Checks.SetUrls(
        [
            new("http://h/a", "b", "http://h/b", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
            new("http://h:99999/", "b", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.BadPortNumber, CurlUrlCode.Ok),
        ]);
        int firstFails = run.Checks.SetUrls([new("http://h:99999/", "b", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);
        int secondFails = run.Checks.SetUrls([new("http://h/", "http://h:99999/", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);

        Assert.AreEqual(0, matching);
        Assert.AreEqual(2, firstFails + secondFails);
        Assert.AreEqual("Set URL\nin: http://h:99999/\nreturned 4 (expected 0)\nlib1560.c:1725 Set URL http://h:99999/ returned 4 (Port number was not a decimal number between 0 and 65535)\n", run.Error.ToString());
    }

    [TestMethod]
    public void Appends_EachOutcome_CountsAsUpstream()
    {
        int tables = new Run().Checks.Appends(Lib1560Tables.Appends);
        int firstFails = new Run().Checks.Appends([new("http://h:99999/", "a=b", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);
        var scripted = new Run(new FakeCurlEngine
        {
            CreatingUrl = () => new ScriptedUrlHandle { Setting = (part, _) => part == CurlUrlPart.Query ? CurlUrlCode.MalformedInput : CurlUrlCode.Ok },
        });
        int appendFailsAsWanted = scripted.Checks.Appends([new("http://h/", "a=b", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.MalformedInput, CurlUrlCode.Ok)]);
        int appendFails = scripted.Checks.Appends([new("http://h/", "a=b", "", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok)]);

        Assert.AreEqual(0, tables + appendFailsAsWanted);
        Assert.AreEqual(1, firstFails);
        Assert.AreEqual(1, appendFails);
        Assert.AreEqual("Append\nin: http://h/\nreturned 3 (expected 0)\n", scripted.Error.ToString());
    }

    [TestMethod]
    public void UpdateUrl_Commands_SetsEachNamedPartAndSkipsTheRest()
    {
        var handle = new ScriptedUrlHandle();
        var run = new Run(new FakeCurlEngine { CreatingUrl = () => handle });
        using Libcurl.Url.CurlUrlHandle url = run.Library.UrlInit();

        CurlUrlCode code = run.Checks.UpdateUrl(url, "=x,host=,noequals,host=NULL,query=\"\",bogus=1,path=/p,last=1", CurlUrlFlags.None);

        Assert.AreEqual(CurlUrlCode.Ok, code);
        CollectionAssert.AreEqual(
            new[] { (CurlUrlPart.Host, (string?)null), (CurlUrlPart.Query, string.Empty), (Lib1560Checks.UnknownPart, "1"), (CurlUrlPart.Path, "/p") },
            handle.Sets.ToArray());
        Assert.AreEqual("UNKNOWN part 'bogus'\n", run.Error.ToString());
    }

    [TestMethod]
    public void UpdateUrl_SetFails_StopsWithItsCode()
    {
        var handle = new ScriptedUrlHandle { Setting = (_, _) => CurlUrlCode.BadHostname };
        var run = new Run(new FakeCurlEngine { CreatingUrl = () => handle });
        using Libcurl.Url.CurlUrlHandle url = run.Library.UrlInit();

        CurlUrlCode code = run.Checks.UpdateUrl(url, "host=a,path=/p,", CurlUrlFlags.None);

        Assert.AreEqual(CurlUrlCode.BadHostname, code);
        Assert.HasCount(1, handle.Sets);
    }

    [TestMethod]
    public void PartId_KnownAndUnknownNames_AnswerTheirPart()
    {
        Assert.AreEqual(CurlUrlPart.ZoneId, Lib1560Checks.PartId("zoneid"));
        Assert.AreEqual(CurlUrlPart.Url, Lib1560Checks.PartId("url"));
        Assert.AreEqual(Lib1560Checks.UnknownPart, Lib1560Checks.PartId("bogus"));
    }

    [TestMethod]
    public void CheckUrl_NullTexts_WriteNull()
    {
        var run = new Run();

        int errors = run.Checks.CheckUrl(null, null, "x");

        Assert.AreEqual(1, errors);
        Assert.AreEqual("Org:    (null)\nWanted: x\nGot   : (null)\n", run.Error.ToString());
    }

    [TestMethod]
    public void HandleGroups_OnTheEngine_AnswerZero()
    {
        var run = new Run();

        Assert.AreEqual(0, run.Checks.ScopeId());
        Assert.AreEqual(0, run.Checks.GetNothing());
        Assert.AreEqual(0, run.Checks.ClearUrl(Lib1560Tables.ClearUrls));
        Assert.AreEqual(0, run.Checks.UrlDup());
        Assert.AreEqual(0, run.Checks.ApiErrors());
        Assert.AreEqual(0, run.Checks.SchemeGuess(Lib1560Tables.SchemeGuesses));
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public void HandleGroups_OnAFailingHandle_CountEachFailure()
    {
        var run = new Run(new FakeCurlEngine { UrlSetResult = CurlUrlCode.BadHandle });

        int scope = run.Checks.ScopeId();
        Assert.AreEqual(scope, run.Error.ToString().Split("lib1560.c:2").Length - 1);
        Assert.IsGreaterThan(4, scope);
        Assert.AreEqual(1, run.Checks.UrlDup());
        Assert.AreEqual(3, run.Checks.ApiErrors());
        Assert.AreEqual(3, run.Checks.ClearUrl(Lib1560Tables.ClearUrls));
        StringAssert.Contains(run.Error.ToString(), "lib1560.c:2014 curl_url_set returned 1 (An invalid CURLU pointer was passed as argument)\n");
        StringAssert.Contains(run.Error.ToString(), "unexpected return code line 2238\nunexpected return code line 2244\nunexpected return code line 2252\n");
    }

    [TestMethod]
    public void GetNothing_OnAFakeHandle_WritesEachUnexpectedCode()
    {
        var run = new Run(new FakeCurlEngine());

        int errors = run.Checks.GetNothing();

        Assert.AreEqual(8, errors);
        StringAssert.EndsWith(run.Error.ToString(), "unexpected return code 14 on line 2201\n");
    }

    [TestMethod]
    public void ClearUrl_PathDiffers_WritesTheLine()
    {
        var run = new Run(new FakeCurlEngine());

        int errors = run.Checks.ClearUrl([new(CurlUrlPart.Path, "/hello", "/", CurlUrlCode.Ok)]);

        Assert.AreEqual(1, errors);
        Assert.AreEqual("unexpected return code line 2252\n", run.Error.ToString());
    }

    [TestMethod]
    public void UrlDup_GetOrCopyDiffers_AnswersOne()
    {
        var getFails = new Run(new FakeCurlEngine { UrlGetResult = CurlUrlCode.NoHost });
        var copyFails = new Run(new FakeCurlEngine());
        var differs = new Run(new FakeCurlEngine
        {
            CreatingUrl = () => new ScriptedUrlHandle
            {
                Getting = _ => (CurlUrlCode.Ok, "a"),
                Duplicating = () => new ScriptedUrlHandle { Getting = _ => (CurlUrlCode.Ok, "b") },
            },
        });

        Assert.AreEqual(1, getFails.Checks.UrlDup());
        Assert.AreEqual(1, copyFails.Checks.UrlDup());
        Assert.AreEqual(1, differs.Checks.UrlDup());
        StringAssert.StartsWith(differs.Output.ToString(), "Original:  http://user:pwd@[2a04:4e42:e00::347%25eth0]:80/path?query#fraggie\nParsed:    a\nCopy:      b\n");
    }

    [TestMethod]
    public void Huge_EachOutcome_CountsAsUpstream()
    {
        var parsedScheme = new Run(new FakeCurlEngine
        {
            CreatingUrl = () => new ScriptedUrlHandle
            {
                Getting = part => (CurlUrlCode.Ok, part == CurlUrlPart.Path ? "/" + Lib1560Checks.BigPart : Lib1560Checks.BigPart),
            },
        });
        var refusedAll = new Run(new FakeCurlEngine { UrlSetResult = CurlUrlCode.BadScheme });
        var partsMissing = new Run(new FakeCurlEngine());

        Assert.AreEqual(1, parsedScheme.Checks.Huge());
        Assert.AreEqual(6, refusedAll.Checks.Huge());
        Assert.AreEqual(8, partsMissing.Checks.Huge());
        StringAssert.StartsWith(parsedScheme.Output.ToString(), "URL 0: failed to parse [");
        StringAssert.EndsWith(partsMissing.Output.ToString(), "URL 6 part 9: failure\n");
    }

    [TestMethod]
    public void SchemeGuess_SchemeDiffers_WritesEachMismatch()
    {
        var run = new Run();

        int none = run.Checks.SchemeGuess([new("https://example.com", false, CurlUrlFlags.None, null, CurlUrlFlags.None)]);
        int other = run.Checks.SchemeGuess([new("https://example.com", false, CurlUrlFlags.None, "ftp", CurlUrlFlags.None)]);
        int unparsed = run.Checks.SchemeGuess([new("example.com", false, CurlUrlFlags.None, "http", CurlUrlFlags.None)]);

        Assert.AreEqual(4, none + other + unparsed);
        Assert.AreEqual(
            "0: got scheme https when none was expected\n0: got scheme https when ftp was expected\n0: URL parsing failed unexpectedly\n0: got no scheme when http was expected\n",
            run.Error.ToString());
    }

    [TestMethod]
    public void SchemeGuess_CodeAndSchemeDisagree_WritesWhich()
    {
        var noScheme = new Run(new FakeCurlEngine { CreatingUrl = () => new ScriptedUrlHandle() });
        var schemeOnError = new Run(new FakeCurlEngine { CreatingUrl = () => new ScriptedUrlHandle { Getting = _ => (CurlUrlCode.NoScheme, "x") } });

        int first = noScheme.Checks.SchemeGuess([new("u", false, CurlUrlFlags.None, null, CurlUrlFlags.None)]);
        int second = schemeOnError.Checks.SchemeGuess([new("u", false, CurlUrlFlags.None, "x", CurlUrlFlags.None)]);

        Assert.AreEqual(2, first + second);
        Assert.AreEqual("0: returned success but no scheme\n", noScheme.Error.ToString());
        Assert.AreEqual("0: returned error but with scheme\n", schemeOnError.Error.ToString());
    }

    private sealed class Run
    {
        public Run(ICurlEngine? engine = null, bool isWindows = true, bool hasUtf8 = false)
        {
            Library = engine is null ? CurlGlobal.Init() : CurlGlobal.Init(engine);
            Checks = new Lib1560Checks(Library, Error, Output, isWindows, hasUtf8);
        }

        public CurlLibrary Library { get; }

        public StringWriter Error { get; } = new();

        public StringWriter Output { get; } = new();

        public Lib1560Checks Checks { get; }
    }
}
