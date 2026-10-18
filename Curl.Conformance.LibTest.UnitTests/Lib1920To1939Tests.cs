using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Libcurl.Global;
using Libcurl.Url;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1920To1939Tests
{
    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib1920(), new Lib1921(), new Lib1922(), LibAwsSigV4Request.Lib1933, LibAwsSigV4Request.Lib1934,
            LibAwsSigV4Request.Lib1935, LibAwsSigV4Request.Lib1936, LibAwsSigV4Request.Lib1937, LibAwsSigV4Request.Lib1938, new Lib1939(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib1920", "lib1921", "lib1922", "lib1933", "lib1934", "lib1935", "lib1936", "lib1937", "lib1938", "lib1939" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1920, "http://x/", "h", "1", "2");

        await Lib1920.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1939.TestAsync(run.Context, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib1921.TestAsync(run.Context, run.Steps, library);
        Assert.AreEqual(CurlCode.FailedInit, run.Steps.Result);
        await Lib1922.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await LibAwsSigV4Request.Lib1933.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        StringAssert.Contains(run.Error.ToString(), "curl_easy_init() failed");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1920_AfterASucceedingPerform_ResetsAndAnswersOk()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1920, "http://x/", "log/cookie1920");

        LibTestResult result = await new Lib1920().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("log/cookie1920", seen!.Http.CookieJar);
        CollectionAssert.AreEqual(new[] { "log/cookie1920" }, seen.Http.CookieFiles.ToArray());
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    public async Task Lib1920_OnAFailingPerform_AnswersItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.GotNothing }, 1920, "http://x/", "jar");

        LibTestResult result = await new Lib1920().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.GotNothing, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1921_WritesTheRedirectedUrl()
    {
        var run = new PortRun(new FakeCurlEngine(), 1921, "-");

        LibTestResult result = await new Lib1921().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("URL: /newpath\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1921_OnAFailingSet_AnswersBadFunctionArgument()
    {
        var run = new PortRun(new FakeCurlEngine { UrlSetResult = CurlUrlCode.BadHostname }, 1921, "-");

        LibTestResult result = await new Lib1921().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void Lib1921_RunChain_StopsAtTheFirstFailure()
    {
        int calls = 0;

        CurlUrlCode code = Lib1921.RunChain([() => { calls++; return CurlUrlCode.Ok; }, () => { calls++; return CurlUrlCode.NoHost; }, () => { calls++; return CurlUrlCode.Ok; }]);

        Assert.AreEqual(CurlUrlCode.NoHost, code);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task Lib1921_WriteUrl_OnAFailingGet_WritesNothing()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using (library)
        {
            using CurlUrlHandle url = library.UrlInit();
            var output = new StringWriter();

            CurlUrlCode code = Lib1921.WriteUrl(url, output);

            Assert.AreEqual(CurlUrlCode.NoHost, code);
            Assert.AreEqual(string.Empty, output.ToString());
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Lib1922_WithoutItsThreeArguments_WritesItsUsage(int argumentCount)
    {
        var run = new PortRun(new FakeCurlEngine(), 1922, [.. new[] { "-", "h", "1", "2" }.Take(argumentCount)]);

        LibTestResult result = await new Lib1922().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual(Lib1922.UsageLine, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1922_WhenTheCopyIsUpgraded_WritesBothLinesAndAnswersCouldntConnect()
    {
        var seen = new List<EasyOptions>();
        var engine = new FakeCurlEngine
        {
            Performing = o => { seen.Add(o); return seen.Count == 1 ? CurlCode.Ok : CurlCode.CouldntConnect; },
            Info = new TransferInfo { EffectiveUrl = "https://hsts.example.com/1922" },
        };
        var run = new PortRun(engine, 1922, "-", "127.0.0.1", "8990", "8991");

        LibTestResult result = await new Lib1922().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("First request: HSTS cache populated\nDup effective URL: https://hsts.example.com/1922\n", run.OutputText);
        CollectionAssert.AreEqual(new[] { "hsts.example.com:8990:127.0.0.1" }, seen[0].Network.Resolve.ToArray());
        Assert.AreEqual(HstsControl.Enable, seen[0].Http.HstsControl);
        Assert.AreEqual("http://127.0.0.1:8991", seen[1].Proxy.Proxy);
        Assert.AreEqual(HstsControl.Enable, seen[1].Http.HstsControl);
    }

    [TestMethod]
    public async Task Lib1922_WithNoEffectiveUrl_WritesOnlyTheFirstLine()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == 1 ? CurlCode.Ok : CurlCode.CouldntConnect, Informing = () => null! };
        var run = new PortRun(engine, 1922, "-", "h", "1", "2");

        LibTestResult result = await new Lib1922().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        StringAssert.StartsWith(run.OutputText, "First request: HSTS cache populated\n");
    }

    [TestMethod]
    public async Task Lib1922_OnAFailingFirstPerform_WritesItsFailure()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.GotNothing }, 1922, "-", "h", "1", "2");

        LibTestResult result = await new Lib1922().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.GotNothing, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "First perform failed: 52 (");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1922_WhenTheCopySucceeds_WritesTheUnexpectedResult()
    {
        var run = new PortRun(new FakeCurlEngine(), 1922, "-", "h", "1", "2");

        LibTestResult result = await new Lib1922().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "Dup perform unexpected result: 0 (");
        Assert.AreEqual("First request: HSTS cache populated\n", run.OutputText);
    }

    [TestMethod]
    [DataRow("lib1933", "xxx", null, false, null)]
    [DataRow("lib1934", "xxx:yyy", "xxx:yyy", false, null)]
    [DataRow("lib1935", "xxx:yyy:rrr", "xxx:yyy", false, null)]
    [DataRow("lib1936", "xxx:yyy:rrr:sss", "xxx:yyy", false, null)]
    [DataRow("lib1937", "provider1:provider2:region:service", "keyId:SecretKey", true, "postData")]
    [DataRow("lib1938", "provider1:provider2:region:service", "keyId:SecretKey", true, "post\0Data")]
    public async Task LibAwsSigV4Request_SignsThroughTheConnectToEntry(string program, string provider, string? credentials, bool post, string? body)
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { PerformResult = CurlCode.Ok, Performing = o => { seen = o; return CurlCode.RecvError; } };
        var run = new PortRun(engine, 1933, "http://127.0.0.1:9000/1933/testapi/test", "127.0.0.1:9000:127.0.0.1:8990");
        ILibTest port = LibTestPrograms.Ported.Find(program)!;

        LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual(provider, seen!.Authentication.AwsSigV4);
        Assert.AreEqual(credentials, seen.Authentication.UserPassword);
        Assert.AreEqual(post, seen.Http.Post);
        Assert.AreEqual(body, seen.Http.PostFields is { } fields ? Encoding.ASCII.GetString(fields.Span) : null);
        CollectionAssert.AreEqual(new[] { "127.0.0.1:9000:127.0.0.1:8990" }, seen.Network.ConnectTo.ToArray());
        CollectionAssert.AreEqual(new[] { "Content-Type: application/json" }, seen.Http.HttpHeaders.ToArray());
        Assert.IsTrue(seen.Behaviour.Verbose);
        Assert.IsFalse(seen.Behaviour.IncludeHeader);
    }

    [TestMethod]
    public async Task LibAwsSigV4Request_WithoutTheConnectToArgument_SetsNoEntry()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 1934, "http://x/1934");

        LibTestResult result = await LibAwsSigV4Request.Lib1934.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, seen!.Network.ConnectTo.Count);
    }

    [TestMethod]
    public async Task Lib1939_StartsTheTransferThroughTheProxyAndAnswersOk()
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, 1939, "https://127.0.0.1:8990/1939", "http://127.0.0.1:8991");

        LibTestResult result = await new Lib1939().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("http://127.0.0.1:8991", ((IEasyTransfer)engine.Multi.Added!).Options.Proxy.Proxy);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1939_OnAFailingAdd_WritesTheFailureAndAnswersOk()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 1939, "https://x/1939", "http://p");

        LibTestResult result = await new Lib1939().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("curl_multi_perform failed\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1939_OnAFailingPerform_WritesTheFailure()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.OutOfMemory, 0);
        var run = new PortRun(engine, 1939, "https://x/1939", "http://p");

        _ = await new Lib1939().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual("curl_multi_perform failed\n", run.Error.ToString());
    }
}
