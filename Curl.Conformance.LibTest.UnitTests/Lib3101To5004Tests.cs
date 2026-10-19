using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib3101To5004Tests
{
    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib3101(), new Lib3102(), new Lib3103(), new Lib3104(), new Lib3105(), new Lib3207(),
            new Lib3208(TimeProvider.System), new Lib5000(), new Lib5004(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib3101", "lib3102", "lib3103", "lib3104", "lib3105", "lib3207", "lib3208", "lib5000", "lib5004" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task Arguments_WhenNull_Throw()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var run = new PortRun(new FakeCurlEngine(), 3207, "http://x/");

        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib3208(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib3102.IsChainInOrder(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib3102.IsChainInOrder(run.Context, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Lib3207.RunThreadAsync(run.Context, library, library.ShareInit()!, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 3101, "http://x/");

        await Lib3101.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib3102.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await new Lib5000().TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        await new Lib3103().TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.BadFunctionArgument, run.Steps.Result);
        await Lib3105.TestAsync(run.Context, run.Steps, library);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        await Lib3208.TestAsync(run.Context, run.Steps, library, new LibTestClock(TimeProvider.System), CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib3207.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);

        StringAssert.Contains(run.Error.ToString(), "curl_share_init() failed\n");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib3105And3208_WhenTheLibraryGoesAtTheMultiHandle_StopAtEasyInit()
    {
        foreach (Func<PortRun, CurlLibrary, ValueTask> test in new Func<PortRun, CurlLibrary, ValueTask>[]
        {
            (run, library) => Lib3105.TestAsync(run.Context, run.Steps, library),
            (run, library) => Lib3208.TestAsync(run.Context, run.Steps, library, new LibTestClock(TimeProvider.System), CancellationToken.None),
        })
        {
            var holder = new PortRun.LibraryHolder();
            var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
            var run = new PortRun(engine, 3105, "http://x/");
            holder.Library = CurlGlobal.Init(engine);

            await test(run, holder.Library);

            Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        }
    }

    [TestMethod]
    public async Task Lib3101_AsksWithAnyAuthenticationAndHttpsOnlyRedirects()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 3101, "http://x/3101");

        LibTestResult result = await new Lib3101().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(HttpAuthenticationMethods.Any, seen!.Authentication.HttpAuthentication);
        Assert.AreEqual("user", seen.Authentication.UserName);
        Assert.AreEqual("password", seen.Authentication.Password);
        Assert.AreEqual("https", seen.Network.RedirectProtocols);
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    public async Task Lib3101_WhenTheTransferFails_EndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.LoginDenied }, 3101, "http://x/3101");

        LibTestResult result = await new Lib3101().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    [TestMethod]
    [DataRow(CurlCode.Ok)]
    [DataRow(CurlCode.GotNothing)]
    public async Task Lib3102_WithAChainInOrder_SucceedsAndWritesEachCertificate(CurlCode performResult)
    {
        IReadOnlyList<string>[] chain = [["Subject:leaf", "Issuer:ca"], ["Subject:ca", "Issuer:root"]];
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Info = new TransferInfo { CertInfo = chain }, Performing = o => { seen = o; return performResult; } };
        var run = new PortRun(engine, 3102, "https://x/3102");

        LibTestResult result = await new Lib3102().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("cert 0\n  subject: leaf\n  issuer: ca\ncert 1\n  subject: ca\n  issuer: root\ncertificate chain is in order\n", run.OutputText);
        Assert.IsTrue(seen!.Tls.GatherCertificateInfo);
    }

    [TestMethod]
    public async Task Lib3102_WithAChainOutOfOrder_SaysSoAndEndsWithFailure()
    {
        IReadOnlyList<string>[] chain = [["Subject:leaf", "Issuer:ca"], ["Subject:other", "Issuer:root"]];
        var run = new PortRun(new FakeCurlEngine { Info = new TransferInfo { CertInfo = chain } }, 3102, "https://x/3102");

        LibTestResult result = await new Lib3102().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual("cert 0 issuer does not match cert 1 subject\ncertificate chain is not in order\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib3102_WhenTheTransferFails_EndsWithItsCodeAndChecksNoChain()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.SslConnectError }, 3102, "https://x/3102");

        LibTestResult result = await new Lib3102().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void Lib3102_IsChainInOrder_WithOneCertificate_IsInOrderWithoutALine()
    {
        var run = new PortRun(null, 3102);

        bool inOrder = Lib3102.IsChainInOrder(run.Context, [["Subject:a"]]);

        Assert.IsTrue(inOrder);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void Lib3102_IsChainInOrder_SkipsACertificateMissingASubjectOrAnIssuer()
    {
        var run = new PortRun(null, 3102);
        IReadOnlyList<string>[] chain = [["Version: 3", "Subject:a", "Issuer:b", "Other:c"], ["Issuer:x"], ["Subject:y"], ["Subject:z", "Issuer:w"]];

        bool inOrder = Lib3102.IsChainInOrder(run.Context, chain);

        Assert.IsTrue(inOrder);
        Assert.AreEqual("cert 0\n  subject: a\n  issuer: b\ncert 3\n  subject: z\n  issuer: w\ncertificate chain is in order\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie()
    {
        foreach (LibSharedCookieThroughProxy port in new LibSharedCookieThroughProxy[] { new Lib3103(), new Lib3104() })
        {
            EasyOptions? seen = null;
            var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } };
            var run = new PortRun(engine, 3103, "http://proxy/");

            LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("http://proxy/", seen!.Proxy.Proxy);
            Assert.AreEqual(port.SiteUrl, seen.Network.Url);
            CollectionAssert.AreEqual(new[] { port.CookieLine }, seen.Http.CookieList.ToArray());
            Assert.IsTrue(seen.Behaviour.IncludeHeader);
            Assert.IsNotNull(engine.LastShare);
        }
    }

    [TestMethod]
    public async Task Lib3103_WhenTheTransferFails_SaysSoAndEndsWithItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntResolveProxy }, 3103, "http://proxy/");

        LibTestResult result = await new Lib3103().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntResolveProxy, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "curl_easy_perform() failed: ");
    }

    [TestMethod]
    public async Task Lib3105_WhenBothRemovalsSucceed_EndsOk()
    {
        var run = new PortRun(new FakeCurlEngine(), 3105, "-");

        LibTestResult result = await new Lib3105().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib3105_WhenARemovalFails_SaysSoAndEndsFailedInit()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.RemoveResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 3105, "-");

        LibTestResult result = await new Lib3105().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.FailedInit, result.ExitCode);
        Assert.AreEqual($"{(int)CurlMultiCode.BadEasyHandle} was unexpected\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib3105_WhenTheAddFails_EndsWithTheMultiCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadHandle;
        var run = new PortRun(engine, 3105, "-");

        LibTestResult result = await new Lib3105().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib3207_WritesEachThreadsBodiesAndEndsWithAFailedThreadsCode()
    {
        FakeCurlEngine engine = null!;
        int performs = 0;
        engine = new FakeCurlEngine
        {
            CreatingEasy = () => { engine.LastShare!.Lock?.Invoke(null, CurlLockData.SslSession, CurlLockAccess.Shared); engine.LastShare.Unlock?.Invoke(null, CurlLockData.SslSession); },
            Performing = o =>
            {
                _ = o.Callbacks.Write!("x"u8);
                return Interlocked.Increment(ref performs) == 1 ? CurlCode.RecvError : CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 3207, "https://x/3207", "ca.crt");

        LibTestResult result = await new Lib3207().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual(((Lib3207.ThreadCount - 1) * Lib3207.TransfersPerThread), run.OutputText.Length);
        Assert.AreEqual($"curl_easy_perform() failed: {CurlErrorText.Easy(CurlCode.RecvError)}\n", run.Error.ToString());
        Assert.IsNull(engine.LastShare!.Lock);
    }

    [TestMethod]
    public async Task Lib3207_RunThreadAsync_OnALibraryGivingNoHandle_SkipsEveryLap()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var share = library.ShareInit()!;
        await library.DisposeAsync();
        var run = new PortRun(null, 3207, "https://x/3207");
        var thread = new Lib3207.ThreadContext();

        await Lib3207.RunThreadAsync(run.Context, library, share, thread, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, thread.Result);
        Assert.IsEmpty(thread.Contents);
    }

    [TestMethod]
    public async Task Lib3208_PerformsEasyThenMultiThenEasyAndEndsWithTheMultiTransfersCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } };
        engine.Multi.DoneResult = CurlCode.PartialFile;
        var run = new PortRun(engine, 3208, "https://x/3208");

        LibTestResult result = await new Lib3208(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);
        Assert.AreEqual(2, performs);
    }

    [TestMethod]
    public async Task Lib3208_WithNoMessage_EndsWithFailure()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 3208, "https://x/3208");

        LibTestResult result = await new Lib3208(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib3208_WhenTheWaitFails_SaysSoAndEndsMajorBad()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        var run = new PortRun(engine, 3208, "https://x/3208");

        LibTestResult result = await new Lib3208(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual($"curl_multi_wait() returned {(int)CurlMultiCode.BadHandle}\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib3208_WhenTheAddFails_EndsWithTheMultiCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadHandle;
        var run = new PortRun(engine, 3208, "https://x/3208");

        LibTestResult result = await new Lib3208(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    [DataRow(5000, "example.com:9000:127.0.0.1:8990")]
    [DataRow(5004, null)]
    public async Task Lib5000And5004_SetTheSigningOptionsOnTheTransfer(int program, string? connectTo)
    {
        LibHttpSignatureRequest port = program == 5000 ? new Lib5000() : new Lib5004();
        EasyOptions? seen = null;
        string[] arguments = connectTo is null ? ["http://example.com/5004/foo"] : ["http://example.com:9000/5000/resource", connectTo];
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, program, arguments);

        LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("ed25519", seen!.Authentication.HttpSignatureAlgorithm);
        Assert.IsNotNull(seen.Authentication.HttpSignatureKey);
        Assert.IsNotNull(seen.Authentication.HttpSignatureKeyId);
    }
}
