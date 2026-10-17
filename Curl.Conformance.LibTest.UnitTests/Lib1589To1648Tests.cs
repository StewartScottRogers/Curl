using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1589To1648Tests
{
    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1589, "http://x/", "1", "2", "3");

        await Lib1599.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib1592.TestAsync(run.Context, run.Steps, library, new LibTestClock(TimeProvider.System), CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        await Lib1593.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1594.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1597.TestAsync(run.Context, run.Steps, library);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib1589.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1591.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1598.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1647.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1648.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(1, run.Error.ToString().Split("curl_multi_init() failed").Length - 1);
        Assert.AreEqual(8, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws()
    {
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new Lib1592(null!)).ParamName);
    }

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports = [new Lib1589(), new Lib1591(), new Lib1592(TimeProvider.System), new Lib1593(), new Lib1594(), new Lib1597(), new Lib1598(), new Lib1599(), new Lib1647(), new Lib1648()];

        CollectionAssert.AreEqual(
            new[] { "lib1589", "lib1591", "lib1592", "lib1593", "lib1594", "lib1597", "lib1598", "lib1599", "lib1647", "lib1648" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task Lib1589_RequestsThroughEachProxyPortWithTheLineBetween()
    {
        var proxies = new List<string>();
        var engine = new FakeCurlEngine { Performing = o => { proxies.Add($"{o.Proxy.Proxy}:{o.Proxy.ProxyPort} {o.Authentication.ProxyUserPassword} {o.Authentication.ProxyAuthentication}"); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1589, "http://x/1589", "127.0.0.1", "8990", "8991", "silly:person");

        LibTestResult result = await new Lib1589().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"127.0.0.1:8990 silly:person {HttpAuthenticationMethods.Digest}", $"127.0.0.1:8991 silly:person {HttpAuthenticationMethods.Digest}" },
            proxies);
        Assert.AreEqual(Lib1589.AgainLine, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1589_OnAFailingFirstRequest_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntResolveProxy; } };
        var run = new PortRun(engine, 1589, "http://x/1589", "127.0.0.1", "8990", "8991");

        LibTestResult result = await new Lib1589().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual(1, performs);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1589_WithTooFewArguments_EndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 1589, "http://x/1589", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1589().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    [TestMethod]
    [DataRow("8990", 8990L)]
    [DataRow("  42abc", 42L)]
    [DataRow("-7", -7L)]
    [DataRow("+5", 5L)]
    [DataRow("abc", 0L)]
    [DataRow(null, 0L)]
    public void Lib1589_ParseInteger_ReadsTheLeadingNumberAsAtoiDoes(string? text, long expected)
    {
        Assert.AreEqual(expected, Lib1589.ParseInteger(text));
    }

    [TestMethod]
    [DataRow(1591)]
    [DataRow(1598)]
    public async Task Lib1591And1598_SendTheTrailerHeaderAndCallbackAndAnswerTheirCode(int testNumber)
    {
        IReadOnlyList<string>? headers = null;
        TrailerCallback? trailer = null;
        var engine = new FakeCurlEngine { Performing = o => { headers = o.Http.HttpHeaders; trailer = o.Callbacks.Trailer; return CurlCode.SendError; } };
        var run = new PortRun(engine, testNumber, "http://x/bzz/" + testNumber);
        ILibTest port = testNumber == 1591 ? new Lib1591() : new Lib1598();

        LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.AreEqual(Lib1591.TrailerHeader, headers![0]);
        Assert.IsNotNull(trailer);
    }

    [TestMethod]
    public void Lib1591_Reader_CopiesTheBodyFromItsStartUntilAllOfItIsConsumed()
    {
        ReadCallback read = Lib1591.Reader();
        byte[] small = new byte[5];
        byte[] large = new byte[100];

        long first = read(small);
        long second = read(large);
        long third = read(large);

        Assert.AreEqual(5, first);
        Assert.AreEqual("Hello", Encoding.ASCII.GetString(small));
        Assert.AreEqual(Lib1591.Body.Length - 5, second);
        Assert.AreEqual("Hello Clo", Encoding.ASCII.GetString(large, 0, (int)second));
        Assert.AreEqual(0, third);
    }

    [TestMethod]
    public void Lib1591_AddTrailers_AddsTheTwoTrailers()
    {
        var trailers = new List<string>();

        TrailerResult result = Lib1591.AddTrailers(trailers);

        Assert.AreEqual(TrailerResult.Ok, result);
        CollectionAssert.AreEqual(new[] { "my-super-awesome-trailer: trail1", "my-other-awesome-trailer: trail2" }, trailers);
        Assert.AreEqual("trailers", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1591.AddTrailers(null!)).ParamName);
    }

    [TestMethod]
    public async Task Lib1592_PerformsOnceThenRemovesTheHandle_WritingEachStep()
    {
        var run = new PortRun(new FakeCurlEngine(), 1592, "http://a-site-never-accessed.example.org/1592");

        LibTestResult result = await new Lib1592(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(
            "curl_multi_perform()...\ncurl_multi_perform() succeeded\ncurl_multi_remove_handle()...\ncurl_multi_remove_handle() succeeded\n",
            run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1592_OnAFailingRemoval_WritesItsCodeAndEndsWithMulti()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.RemoveResult = CurlMultiCode.BadHandle;
        var run = new PortRun(engine, 1592, "http://x/1592");

        LibTestResult result = await new Lib1592(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        StringAssert.EndsWith(run.Error.ToString(), $"curl_multi_remove_handle() failed, with code {(int)CurlMultiCode.BadHandle}\n");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Lib1592_OnAFailingAddOrPerform_EndsWithMulti(int failing)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = failing == 1 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.PerformResult = failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 0);
        var run = new PortRun(engine, 1592, "http://x/1592");

        LibTestResult result = await new Lib1592(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        Assert.DoesNotContain("curl_multi_remove_handle()...", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1592_WhenTheEasyInitFailsAfterTheMultiInit_EndsWithEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        var run = new PortRun(engine, 1592, "http://x/1592");
        holder.Library = CurlGlobal.Init(engine);

        await Lib1592.TestAsync(run.Context, run.Steps, holder.Library, new LibTestClock(TimeProvider.System), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public void Lib1592_TimeoutFor_IsTwiceTheHangTimeoutWithDnsServersElseHalfAfterTheLine()
    {
        var error = new StringWriter();

        long cancellable = Lib1592.TimeoutFor(CurlCode.Ok, error);
        Assert.AreEqual(string.Empty, error.ToString());
        long blocking = Lib1592.TimeoutFor(CurlCode.NotBuiltIn, error);

        Assert.AreEqual(LibTestClock.TestHangTimeoutMilliseconds * 2, cancellable);
        Assert.AreEqual(LibTestClock.TestHangTimeoutMilliseconds / 2, blocking);
        Assert.AreEqual(Lib1592.NoDnsServersLine, error.ToString());
        Assert.AreEqual("error", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1592.TimeoutFor(CurlCode.Ok, null!)).ParamName);
    }

    [TestMethod]
    [DataRow(1L, 0)]
    [DataRow(0L, (int)LibTestErrorCodes.Failure)]
    public async Task Lib1593_HoldsTheConditionUnmetToOne(long unmet, int expected)
    {
        TimeCondition? condition = null;
        var engine = new FakeCurlEngine { Info = new TransferInfo { ConditionUnmet = unmet }, Performing = o => { condition = o.Transfer.TimeCondition; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1593, "http://x/1593");

        LibTestResult result = await new Lib1593().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual(TimeCondition.IfModifiedSince, condition);
    }

    [TestMethod]
    public async Task Lib1593_OnAFailingTransfer_AnswersItsCode()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect };
        var run = new PortRun(engine, 1593, "http://x/1593");

        LibTestResult result = await new Lib1593().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1594_WritesTheRetryAfterSeconds()
    {
        var engine = new FakeCurlEngine { Info = new TransferInfo { RetryAfter = TimeSpan.FromSeconds(22) } };
        var run = new PortRun(engine, 1594, "http://x/1594");

        LibTestResult result = await new Lib1594().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Retry-After 22\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1594_OnAFailingTransfer_AnswersItsCodeAndWritesNothing()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect };
        var run = new PortRun(engine, 1594, "http://x/1594");

        LibTestResult result = await new Lib1594().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1597_RunsTheStringsThroughTheHandleAndWritesTheCount()
    {
        var run = new PortRun(new FakeCurlEngine(), 1597);

        LibTestResult result = await new Lib1597().RunAsync(run.Context, CancellationToken.None);

        var expected = new StringWriter();
        CurlCode code = Lib1597.TestStrings([], _ => CurlCode.BadFunctionArgument, expected);
        Assert.AreEqual((int)code, result.ExitCode);
        Assert.AreEqual(expected.ToString(), run.OutputText);
    }

    [TestMethod]
    public void Lib1597_TestStrings_WhenEveryAnswerIsExpected_TestsThemAll()
    {
        string[] protocols = ["http", "https", "ftp"];
        var seen = new List<string>();
        var output = new StringWriter();

        CurlCode result = Lib1597.TestStrings(protocols, value => { seen.Add(value); return Lib1597.CodeFor(Lib1597.Cases[seen.Count - 1].Expected, protocols); }, output);

        Assert.AreEqual(CurlCode.Ok, result);
        Assert.AreEqual($"Tested {Lib1597.Cases.Count} strings\n", output.ToString());
        Assert.AreEqual(",http,https,ftp", seen[13]);
    }

    [TestMethod]
    public void Lib1597_TestStrings_OnAnUnexpectedAnswer_WritesItAndStops()
    {
        var output = new StringWriter();

        CurlCode result = Lib1597.TestStrings([], _ => CurlCode.BadFunctionArgument, output);

        Assert.AreEqual(CurlCode.BadFunctionArgument, result);
        Assert.AreEqual($"unexpectedly 'goobar' returned {(int)CurlCode.BadFunctionArgument}\nTested 0 strings\n", output.ToString());
    }

    [TestMethod]
    public void Lib1597_TestStrings_NullArguments_Throw()
    {
        Assert.AreEqual("protocols", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1597.TestStrings(null!, _ => CurlCode.Ok, TextWriter.Null)).ParamName);
        Assert.AreEqual("setProtocols", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1597.TestStrings([], null!, TextWriter.Null)).ParamName);
        Assert.AreEqual("output", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1597.TestStrings([], _ => CurlCode.Ok, null!)).ParamName);
    }

    [TestMethod]
    [DataRow(Lib1597Expectation.Ok, CurlCode.Ok)]
    [DataRow(Lib1597Expectation.BadArgument, CurlCode.BadFunctionArgument)]
    [DataRow(Lib1597Expectation.Unsupported, CurlCode.UnsupportedProtocol)]
    [DataRow(Lib1597Expectation.Http, CurlCode.Ok)]
    [DataRow(Lib1597Expectation.Https, CurlCode.UnsupportedProtocol)]
    [DataRow((Lib1597Expectation)99, CurlCode.UnsupportedProtocol)]
    public void Lib1597_CodeFor_FollowsTheEnabledProtocols(Lib1597Expectation expected, CurlCode code)
    {
        Assert.AreEqual(code, Lib1597.CodeFor(expected, ["HTTP"]));
    }

    [TestMethod]
    public async Task Lib1599_RequiresTheNetrcFileAndAnswersThePerform()
    {
        NetrcUsage? usage = null;
        string? file = null;
        var engine = new FakeCurlEngine { Performing = o => { usage = o.Authentication.Netrc; file = o.Authentication.NetrcFile; return CurlCode.LoginDenied; } };
        var run = new PortRun(engine, 1599, "ftp://x/1599", "log/netrc1599");

        LibTestResult result = await new Lib1599().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(NetrcUsage.Required, usage);
        Assert.AreEqual("log/netrc1599", file);
    }

    [TestMethod]
    public async Task Lib1647_GetsBothUrlsWithTheirCredentials()
    {
        var gets = new List<string>();
        var engine = new FakeCurlEngine { Performing = o => { gets.Add($"{o.Network.Url} {o.Authentication.ProxyUserPassword}"); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1647, "http://x/1647", "https://y/", "daniel:monkey123", "another:bump456");

        LibTestResult result = await new Lib1647().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "http://x/1647 daniel:monkey123", "https://y/ another:bump456" }, gets);
        Assert.AreEqual("--- First get 'http://x/1647'\n--- Then get 'https://y/'\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1647_OnAFailingFirstGet_StopsWithItsCode()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.CouldntResolveProxy };
        var run = new PortRun(engine, 1647);

        LibTestResult result = await new Lib1647().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("--- First get '(nil)'\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1647_WithNoSecondUrl_NamesItNil()
    {
        var run = new PortRun(new FakeCurlEngine(), 1647, "http://x/1647");

        LibTestResult result = await new Lib1647().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("--- First get 'http://x/1647'\n--- Then get '(nil)'\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1648_GetsThroughEachProxyClearingTheCredentialsBetween()
    {
        var gets = new List<string>();
        var engine = new FakeCurlEngine { Performing = o => { gets.Add($"{o.Network.Url} {o.Proxy.Proxy} {o.Authentication.ProxyUserPassword ?? "-"}"); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1648, "127.0.0.1", "8990");

        LibTestResult result = await new Lib1648().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "http://example.com/ firstproxy:8990 victim:secret", "http://example.com/ secondproxy:8990 -" }, gets);
        Assert.AreEqual("--- First get over firstproxy:8990\n--- Then over 'secondproxy:8990'\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1648_OnAFailingFirstGet_StopsWithItsCode()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.CouldntResolveProxy };
        var run = new PortRun(engine, 1648);

        LibTestResult result = await new Lib1648().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("--- First get over firstproxy:(nil)\n", run.OutputText);
    }
}
