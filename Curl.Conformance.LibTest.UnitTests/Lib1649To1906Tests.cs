using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Options.Authentication;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1649To1906Tests
{
    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1649, "http://x/", "1", "2");

        await Lib1662.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1686.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1901.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1902.TestAsync(run.Context, run.Steps, library);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib1905.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        await Lib1900.TestAsync(run.Context, run.Steps, library);
        await Lib1903.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1906.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib1649.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(4, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports = [new Lib1649(), new Lib1662(), new Lib1678(), new Lib1686(), new Lib1900(), new Lib1901(), new Lib1902(), new Lib1903(), new Lib1905(), new Lib1906()];

        CollectionAssert.AreEqual(
            new[] { "lib1649", "lib1662", "lib1678", "lib1686", "lib1900", "lib1901", "lib1902", "lib1903", "lib1905", "lib1906" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task Lib1649_SendsTheRefererThenNone()
    {
        var referers = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { referers.Add(o.Http.Referer); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1649, "http://x/");

        LibTestResult result = await new Lib1649().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { Lib1649.Referer, null }, referers);
    }

    [TestMethod]
    public async Task Lib1649_OnAFailingFirstPerform_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } };
        var run = new PortRun(engine, 1649, "http://x/");

        LibTestResult result = await new Lib1649().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task Lib1662_PostsOneCallbackPartAndAnswersThePerformsCode()
    {
        var mime = new FakeMimeBuilder();
        var engine = new FakeCurlEngine { CreatingMime = () => mime, PerformResult = CurlCode.SendError };
        var run = new PortRun(engine, 1662, "http://x/1662");

        LibTestResult result = await new Lib1662().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.AreEqual("content", mime.Parts.Single().Name);
        CollectionAssert.AreEqual(new[] { "SetDataCallback -1", "SetFileName poetry.txt" }, mime.Parts.Single().Calls.Take(2).ToArray());
    }

    [TestMethod]
    public void Lib1662_Reader_GivesTheDataOneByteACall()
    {
        var reader = new Lib1662.Reader();
        byte[] buffer = new byte[10];
        var read = new StringBuilder();

        long empty = reader.Read([]);
        while (reader.Read(buffer) == 1)
        {
            read.Append((char)buffer[0]);
        }

        Assert.AreEqual(0, empty);
        Assert.AreEqual(Lib1662.TestData, read.ToString());
    }

    [TestMethod]
    public async Task Lib1678_LibraryWithoutSslsExport_SucceedsWithoutImporting()
    {
        int imports = 0;
        var engine = new FakeCurlEngine { Importing = _ => { imports++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1678);

        LibTestResult result = await new Lib1678().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, imports);
        Assert.IsNull(engine.LastShare);
    }

    [TestMethod]
    public async Task Lib1678_LibraryWithSslsExport_ImportsThePacket64TimesOverASharedSessionCache()
    {
        var keys = new List<string?>();
        var engine = new FakeCurlEngine
        {
            VersionInfo = new CurlVersionInfo { Features = ["SSL", "SSLS-EXPORT"] },
            Importing = key => { keys.Add(key); return CurlCode.Ok; },
        };
        var run = new PortRun(engine, 1678);

        LibTestResult result = await new Lib1678().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(Lib1678.ImportCount, keys);
        Assert.IsTrue(keys.TrueForAll(key => key == "example.test:443"));
        Assert.IsNotNull(engine.LastShare);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1678_ThirdImportFails_WritesItsLineAndEndsWithItsCode()
    {
        int imports = 0;
        var engine = new FakeCurlEngine
        {
            VersionInfo = new CurlVersionInfo { Features = ["SSLS-EXPORT"] },
            Importing = _ => ++imports == 3 ? CurlCode.ReadError : CurlCode.Ok,
        };
        var run = new PortRun(engine, 1678);

        LibTestResult result = await new Lib1678().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(26, result.ExitCode);
        Assert.AreEqual(3, imports);
        Assert.AreEqual("import 2 failed: 26 (Failed to open/read local data from file/application)\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1678Test_ShareRefusesTheSessionCache_EndsWithFailedInitWithoutImporting()
    {
        int imports = 0;
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { ShareResult = CurlShareCode.NotBuiltIn, Importing = _ => { imports++; return CurlCode.Ok; } });
        var run = new PortRun(null, 1678);

        Lib1678.Test(run.Context, run.Steps, library);

        Assert.AreEqual(CurlCode.FailedInit, run.Steps.Result);
        Assert.AreEqual(0, imports);
    }

    [TestMethod]
    public async Task Lib1678Test_CleanedUpLibrary_EndsWithFailedInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(null, 1678);

        Lib1678.Test(run.Context, run.Steps, library);

        Assert.AreEqual(CurlCode.FailedInit, run.Steps.Result);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1678ImportOverShare_ShareOptionRefused_EndsWithItsCodeWithoutImporting()
    {
        int imports = 0;
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Importing = _ => { imports++; return CurlCode.Ok; } });
        using var easy = library.EasyInit();
        var run = new PortRun(null, 1678);

        CurlCode code = Lib1678.ImportOverShare(run.Context, easy, "not a share handle");

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
        Assert.AreEqual(0, imports);
    }

    [TestMethod]
    public void Lib1678Packet_IsUpstreamsPacketWithRepeatedTags()
    {
        IReadOnlyList<byte> packet = Lib1678.Packet;

        Assert.HasCount(1 + 3 + 4096 + 3 + 127 + 3 + 10 + 4 + 4 + 4, packet);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x04, 0x10, 0x00, (byte)'A' }, packet.Take(5).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x07, 0x00, 0x7F, (byte)'Q' }, packet.Skip(4100).Take(4).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00, 0x0A, (byte)'a' }, packet.Skip(4230).Take(4).ToArray());
        CollectionAssert.AreEqual(
            new byte[] { 0x04, 0x00, 0x01, (byte)'B', 0x05, 0x00, 0x01, (byte)'b', 0x07, 0x00, 0x01, (byte)'R' },
            packet.Skip(packet.Count - 12).ToArray());
    }

    [TestMethod]
    public async Task Lib1686_FetchesBothHostsThenSwitchesUser()
    {
        var requests = new List<string>();
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                requests.Add($"{o.Network.Url} {o.Authentication.UserPassword} {o.Authentication.HttpAuthentication} {string.Join('|', o.Network.Resolve)}");
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1686, "127.0.0.1", "8990");

        LibTestResult result = await new Lib1686().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        string resolve = "first.test:8990:127.0.0.1|second.test:8990:127.0.0.1";
        CollectionAssert.AreEqual(
            new[]
            {
                $"http://first.test:8990/api alice:bond {HttpAuthenticationMethods.Digest} {resolve}",
                $"http://second.test:8990/hook alice:bond {HttpAuthenticationMethods.Digest} {resolve}",
                $"http://second.test:8990/hook bob:secret {HttpAuthenticationMethods.Digest} {resolve}",
            },
            requests);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Lib1686_StopsAtTheFirstFailingPerform(int failing)
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == failing ? CurlCode.RecvError : CurlCode.Ok };
        var run = new PortRun(engine, 1686, "127.0.0.1", "8990");

        LibTestResult result = await new Lib1686().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual(failing, performs);
    }

    [TestMethod]
    public async Task Lib1900_SetsTheSecondHstsFileAndEndsOk()
    {
        var engine = new FakeCurlEngine { Performing = _ => throw new InvalidOperationException("no transfer") };
        var run = new PortRun(engine, 1900, "http://x/1900");

        LibTestResult result = await new Lib1900().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual(Lib1900.SecondHstsFile, engine.CreatedEasies[0].Options.Http.Hsts);
    }

    [TestMethod]
    public async Task Lib1901_PostsFromTheReadCallbackWithTheChunkedHeaders()
    {
        IReadOnlyList<string>? headers = null;
        bool post = false;
        long size = 0;
        var engine = new FakeCurlEngine { Performing = o => { headers = o.Http.HttpHeaders; post = o.Http.Post; size = o.Http.PostFieldSize; return CurlCode.PartialFile; } };
        var run = new PortRun(engine, 1901, "http://x/boom");

        LibTestResult result = await new Lib1901().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "Expect:", "Transfer-Encoding: chunked" }, headers!.ToArray());
        Assert.IsTrue(post);
        Assert.AreEqual(4, size);
    }

    [TestMethod]
    public void Lib1901_Reader_GivesEachChunkThenZero()
    {
        Func<Span<byte>, long> read = Lib1901.Reader();
        byte[] buffer = new byte[16];
        var chunks = new List<string>();

        for (long length = read(buffer); length > 0; length = read(buffer))
        {
            chunks.Add(Encoding.ASCII.GetString(buffer, 0, (int)length));
        }

        CollectionAssert.AreEqual(Lib1901.Chunks.ToArray(), chunks);
    }

    [TestMethod]
    public void Lib1901_Reader_IntoABufferTooSmall_CopiesNothingAndAnswersTheLength()
    {
        Func<Span<byte>, long> read = Lib1901.Reader();
        byte[] buffer = new byte[3];

        long length = read(buffer);

        Assert.AreEqual(3, length);
        CollectionAssert.AreEqual(new byte[3], buffer);
    }

    [TestMethod]
    public async Task Lib1902_SetsTheFileAsCookieFileAndJarWithoutATransfer()
    {
        var engine = new FakeCurlEngine { Performing = _ => throw new InvalidOperationException("no transfer") };
        var run = new PortRun(engine, 1902, "log/cookie1902");

        LibTestResult result = await new Lib1902().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1903_ReadsCookiesThenAfterTheResetWritesTheJar()
    {
        var transfers = new List<string>();
        var engine = new FakeCurlEngine { Performing = o => { transfers.Add($"{string.Join('|', o.Http.CookieFiles)} {o.Http.CookieJar ?? "-"}"); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1903, "http://x/we/want/1903", "in", "out");

        LibTestResult result = await new Lib1903().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "in -", "in out" }, transfers);
    }

    [TestMethod]
    public async Task Lib1903_OnAFailingFirstPerform_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.GotNothing; } };
        var run = new PortRun(engine, 1903, "http://x/we/want/1903", "in", "out");

        LibTestResult result = await new Lib1903().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.GotNothing, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    [DataRow(5L)]
    [DataRow(-1L)]
    public async Task Lib1905_LoopsUntilNoTransferIsLeftAndEndsOk(long timeout)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, timeout);
        int waits = 0;
        engine.Multi.Waiting = () => waits++;
        var run = new PortRun(engine, 1905, "http://x/we/want/1905", "cookies");

        LibTestResult result = await new Lib1905().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(2, waits);
        Assert.IsNotNull(engine.LastShare);
        Assert.IsNotNull(engine.Multi.Added);
    }

    [TestMethod]
    public async Task Lib1905_LoopAsync_NullMulti_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Lib1905.LoopAsync(null!, CancellationToken.None));

        Assert.AreEqual("multi", exception.ParamName);
    }

    [TestMethod]
    [DataRow(CurlCode.CouldntConnect)]
    [DataRow(CurlCode.OperationTimedOut)]
    public async Task Lib1906_AfterTheExpectedFailure_PerformsOnTheUrlsPortAndWritesTheUrlTwice(CurlCode firstResult)
    {
        var ports = new List<int>();
        var engine = new FakeCurlEngine { Performing = o => { ports.Add(o.Network.Port); return ports.Count == 1 ? firstResult : CurlCode.Ok; } };
        var run = new PortRun(engine, 1906, "http://x:8990/1906");

        LibTestResult result = await new Lib1906().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { 1, 0 }, ports);
        Assert.AreEqual("curlu now: <http://x:8990/1906>\ncurlu now: <http://x:8990/1906>\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1906_OnAFailingSecondPerform_WritesSuccessExpectedAndEndsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == 1 ? CurlCode.CouldntConnect : CurlCode.RecvError };
        var run = new PortRun(engine, 1906, "http://x:8990/1906");

        LibTestResult result = await new Lib1906().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), $"success expected, curl_easy_perform returned {(int)CurlCode.RecvError}: <");
    }

    [TestMethod]
    [DataRow(CurlCode.Ok, (int)LibTestErrorCodes.MajorBad)]
    [DataRow(CurlCode.SendError, (int)CurlCode.SendError)]
    public async Task Lib1906_OnAnUnexpectedFirstAnswer_WritesFailureExpectedAndStops(CurlCode firstResult, int exitCode)
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return firstResult; } };
        var run = new PortRun(engine, 1906, "http://x:8990/1906");

        LibTestResult result = await new Lib1906().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(1, performs);
        StringAssert.StartsWith(run.Error.ToString(), $"failure expected, curl_easy_perform returned {(int)firstResult}: <");
    }
}
