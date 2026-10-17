using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1512To1523Tests
{
    [TestMethod]
    public async Task EveryEasyFirstPort_OnACleanedUpLibrary_StopsAtEasyInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1512, "http://x/", "127.0.0.1", "8990");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1512.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1513.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1514.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1522.TestAsync(run.Context, run.Steps, library, TimeProvider.System, CancellationToken.None);
        await Lib1523.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib1517.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1518.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1520.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(8, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
    }

    [TestMethod]
    public void TimedPorts_WithoutATimeProvider_Throw()
    {
        Action[] constructors =
        [
            () => _ = new Lib1515(null!),
            () => _ = new Lib1517(null!),
            () => _ = new Lib1522(null!),
        ];

        foreach (Action construct in constructors)
        {
            Assert.ThrowsExactly<ArgumentNullException>(construct);
        }
    }

    [TestMethod]
    public async Task Lib1512_WritesTheEntryAndPerformsBothHandlesTheFirstWithIt()
    {
        var transfers = new List<(string? Url, int ResolveCount)>();
        var engine = new FakeCurlEngine { Performing = o => { transfers.Add((o.Network.Url, o.Network.Resolve.Count)); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1512, "-", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1512().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("server.example.curl:8990:127.0.0.1\n", run.OutputText);
        CollectionAssert.AreEqual(new[] { ("http://server.example.curl:8990/path/15120001", 1), ("http://server.example.curl:8990/path/15120002", 0) }, transfers);
        Assert.AreEqual("lib1512", new Lib1512().ProgramName);
    }

    [TestMethod]
    public async Task Lib1512_OnAFailingFirstTransfer_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } };
        var run = new PortRun(engine, 1512, "-", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1512().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task Lib1512_WhenTheLibraryGoesAtTheFirstHandle_StopsAtTheSecondEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), 1512, "-", "127.0.0.1", "8990");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib1512.TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib1513_SetsAProgressCallbackThatWritesAndAborts()
    {
        int? progress = null;
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                progress = o.Callbacks.Progress!(0, 0, 0, 0);
                return CurlCode.AbortedByCallback;
            },
        };
        var run = new PortRun(engine, 1513, "http://x/1513");

        LibTestResult result = await new Lib1513().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual(1, progress);
        Assert.AreEqual("PROGRESSFUNCTION called\n", run.OutputText);
        Assert.AreEqual("lib1513", new Lib1513().ProgramName);
    }

    [TestMethod]
    [DataRow(1514, 0L)]
    [DataRow(1539, Lib1514.HttpVersion10)]
    public async Task Lib1514_PostsThroughTheReaderAndSpeaksHttp10OnlyFor1539(int testNumber, long expectedVersion)
    {
        long version = -1;
        var engine = new FakeCurlEngine { Performing = o => { version = (long)o.Http.HttpVersion; return CurlCode.Ok; } };
        var run = new PortRun(engine, testNumber, "http://x/");

        LibTestResult result = await new Lib1514().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(expectedVersion, version);
        Assert.AreEqual("lib1514", new Lib1514().ProgramName);
    }

    [TestMethod]
    public void Lib1514OneByteReader_GivesOneByteACallThenNothing()
    {
        var reader = new Lib1514.OneByteReader("ab"u8.ToArray());
        byte[] buffer = new byte[4];

        long empty = reader.Read(Span<byte>.Empty);
        long first = reader.Read(buffer);
        byte firstByte = buffer[0];
        long second = reader.Read(buffer);
        long end = reader.Read(buffer);

        Assert.AreEqual(0L, empty);
        Assert.AreEqual(1L, first);
        Assert.AreEqual((byte)'a', firstByte);
        Assert.AreEqual(1L, second);
        Assert.AreEqual((byte)'b', buffer[0]);
        Assert.AreEqual(0L, end);
    }

    [TestMethod]
    public async Task Lib1515_MakesTwoTracedRequestsWithTheEntryAndAnswersTheFirstsDoneCode()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine();
        engine.Multi.DoneResult = CurlCode.Ok;
        var run = new PortRun(engine, 1515, "path/1515", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1515(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("http://testserver.example.com:8990/path/15150002", engine.Multi.Added!.Options.Network.Url);
        CollectionAssert.AreEqual(new[] { "testserver.example.com:8990:127.0.0.1" }, engine.Multi.Added.Options.Network.Resolve.ToArray());
        Assert.AreEqual(Lib1515.DnsTimeoutSeconds, (long)engine.Multi.Added.Options.Network.DnsCacheTimeout.TotalSeconds);
        Assert.IsNotNull(engine.Multi.Added.Options.Callbacks.Debug);
        Assert.AreEqual("lib1515", new Lib1515(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task Lib1515_OnAFailingTransfer_WritesTheRequestAndStopsWithItsCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.DoneResult = CurlCode.CouldntConnect;
        var run = new PortRun(engine, 1515, "path/1515", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1515(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("request http://testserver.example.com:8990/path/15150001 failed with 7\n", run.Error.ToString());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Lib1515_OnAFailingMultiCall_StopsWithMulti(int failing)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = failing == 1 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.PerformResult = failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 0);
        var run = new PortRun(engine, 1515, "path/1515", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1515(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        StringAssert.EndsWith(run.Error.ToString(), $"request http://testserver.example.com:8990/path/15150001 failed with {(int)LibTestErrorCodes.Multi}\n");
    }

    [TestMethod]
    public async Task Lib1515_OnACleanedUpLibrary_StopsAtMultiInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1515, "path/1515", "127.0.0.1", "8990");

        await Lib1515.TestAsync(run.Context, run.Steps, library, new LibTestClock(TimeProvider.System), TimeProvider.System, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib1515_WhenTheLibraryGoesAtTheMultiHandle_StopsAtEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        var run = new PortRun(engine, 1515, "path/1515", "127.0.0.1", "8990");
        holder.Library = CurlGlobal.Init(engine);

        await Lib1515.TestAsync(run.Context, run.Steps, holder.Library, new LibTestClock(TimeProvider.System), TimeProvider.System, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib1515DoneResult_AnswersOnlyAMessageForTheHandle()
    {
        var engine = new FakeCurlEngine();
        CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        await using CurlEasy added = library.EasyInit();
        await using CurlEasy other = library.EasyInit();
        _ = multi.AddHandle(added);

        engine.Multi.DoneResult = CurlCode.CouldntConnect;
        CurlCode forOther = Lib1515.DoneResult(multi, other, CurlCode.Ok);
        engine.Multi.DoneResult = CurlCode.CouldntConnect;
        CurlCode forAdded = Lib1515.DoneResult(multi, added, CurlCode.Ok);

        Assert.AreEqual(CurlCode.Ok, forOther);
        Assert.AreEqual(CurlCode.CouldntConnect, forAdded);
    }

    [TestMethod]
    public async Task Lib1517_PostsThroughAReaderThatWaitsASecondEachCall()
    {
        long read = -1;
        byte[] buffer = new byte[64];
        var engine = new FakeCurlEngine { Performing = o => { read = o.Callbacks.Read!(buffer); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1517, "http://x/1517");
        long start = TimeProvider.System.GetTimestamp();

        LibTestResult result = await new Lib1517(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900), TimeProvider.System.GetElapsedTime(start));
        Assert.AreEqual(Lib1517.PostData.Length, read);
        Assert.AreEqual("this is what we post to the silly web server\n", Encoding.ASCII.GetString(buffer, 0, (int)read));
        Assert.AreEqual("lib1517", new Lib1517(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public void Lib1517SlowReader_WaitsThenCopiesWhatFitsThenNothing()
    {
        int waits = 0;
        var reader = new Lib1517.SlowReader("abc"u8.ToArray(), () => waits++);
        byte[] buffer = new byte[2];

        long first = reader.Read(buffer);
        long second = reader.Read(buffer);
        long end = reader.Read(buffer);

        Assert.AreEqual(2L, first);
        Assert.AreEqual(1L, second);
        Assert.AreEqual((byte)'c', buffer[0]);
        Assert.AreEqual(0L, end);
        Assert.AreEqual(3, waits);
    }

    [TestMethod]
    [DataRow(null, "blank")]
    [DataRow("http://h/next", "http://h/next")]
    public async Task Lib1518_WritesTheTransfersInfoWithoutFollowing(string? redirectUrl, string expectedRedirect)
    {
        bool? follows = null;
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { ResponseCode = 302, RedirectCount = 0, EffectiveUrl = "http://x/1518", RedirectUrl = redirectUrl },
            Performing = o => { follows = o.Http.FollowLocation != Curl.Api.Options.Http.FollowLocation.None; return CurlCode.Ok; },
        };
        var run = new PortRun(engine, 1518, "http://x/1518");

        LibTestResult result = await new Lib1518().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsFalse(follows);
        Assert.AreEqual($"result 0\nstatus 302\nredirects 0\neffectiveurl http://x/1518\nredirecturl {expectedRedirect}\n", run.OutputText);
        Assert.AreEqual("lib1518", new Lib1518().ProgramName);
    }

    [TestMethod]
    public async Task Lib1518_For1543_HandsTheUrlHandleOverAndFollows()
    {
        bool? follows = null;
        var engine = new FakeCurlEngine { Performing = o => { follows = o.Http.FollowLocation != Curl.Api.Options.Http.FollowLocation.None; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1543, "http://x/ /with/ space/ /file");

        LibTestResult result = await new Lib1518().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(follows);
        StringAssert.StartsWith(run.OutputText, "result 0\n");
    }

    [TestMethod]
    public async Task Lib1518_For1543_WhenTheUrlDoesNotParse_EndsAtOnceWithZero()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { UrlSetResult = CurlUrlCode.MalformedInput, Performing = _ => { performs++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1543, "bad");

        LibTestResult result = await new Lib1518().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, performs);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1518_OnAFailingTransfer_EndsWithItsCodeAndWritesNothing()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1518, "http://x/1518");

        LibTestResult result = await new Lib1518().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1520_UploadsThePayloadFromTheSenderToTheRecipient()
    {
        string? from = null;
        var engine = new FakeCurlEngine { Performing = o => { from = o.Protocols.Smtp.MailFrom; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1520, "smtp://x/1520");

        LibTestResult result = await new Lib1520().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("<sender@example.com>", from);
        Assert.AreEqual("lib1520", new Lib1520().ProgramName);
    }

    [TestMethod]
    public void Lib1520LineReader_GivesALineACallThenNothing()
    {
        var reader = new Lib1520.LineReader();
        byte[] buffer = new byte[64];
        var payload = new StringBuilder();

        long empty = reader.Read(Span<byte>.Empty);
        long read;
        while ((read = reader.Read(buffer)) > 0)
        {
            payload.Append(Encoding.ASCII.GetString(buffer, 0, (int)read));
        }

        Assert.AreEqual(0L, empty);
        Assert.AreEqual("From: different\r\nTo: another\r\n\r\n\r\n.\r\n.\r\n\r\n.\r\n\r\nbody", payload.ToString());
    }

    [TestMethod]
    [DataRow(Lib1522.PostSize, "uploadSize = 40960\n!!!!!!!!!! PASS\n")]
    [DataRow(4096, "uploadSize = 4096\nsent 40960, libcurl says 4096\n")]
    public async Task Lib1522_WritesTheUploadSizeAndItsVerdict(int uploadSize, string expected)
    {
        SocketOptionResult? socketOption = null;
        string? expect = null;
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { SizeUpload = uploadSize },
            Performing = o =>
            {
                socketOption = o.Callbacks.SocketOption!(new CurlSocket(1), CurlSocketType.IpConnection);
                expect = o.Http.HttpHeaders.Single();
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1522, "http://x/1522");

        LibTestResult result = await new Lib1522(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(SocketOptionResult.Ok, socketOption);
        Assert.AreEqual("Expect:", expect);
        Assert.AreEqual(expected, run.OutputText);
        Assert.AreEqual("lib1522", new Lib1522(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task Lib1522_OnAFailingTransfer_WritesItAndEndsWithItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1522, "http://x/1522");

        LibTestResult result = await new Lib1522(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("curl_easy_perform() failed. e = 7\n", run.OutputText);
    }

    [TestMethod]
    [DataRow((int)CurlCode.Ok, (int)CurlCode.OperationTimedOut, 0, "")]
    [DataRow((int)CurlCode.CouldntConnect, (int)CurlCode.OperationTimedOut, 0, "error (7) \n")]
    [DataRow((int)CurlCode.Ok, (int)CurlCode.Ok, 0, "error (0) \n")]
    [DataRow((int)CurlCode.Ok, (int)CurlCode.CouldntConnect, (int)CurlCode.CouldntConnect, "error (7) \n")]
    public async Task Lib1523_RunsTwiceAndAnswersZeroOnlyWhenTheSecondTimesOutOrSucceeds(int first, int second, int expected, string expectedError)
    {
        var limits = new List<long>();
        int? progress = null;
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                limits.Add(o.Transfer.LowSpeedLimit);
                progress = o.Callbacks.TransferInfo!(0, 0, 0, 0);
                return (CurlCode)(limits.Count == 1 ? first : second);
            },
        };
        var run = new PortRun(engine, 1523, "http://x/1523");

        LibTestResult result = await new Lib1523().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
        CollectionAssert.AreEqual(new[] { 1L, 12000L }, limits);
        Assert.AreEqual(0, progress);
        Assert.AreEqual(expectedError, run.Error.ToString());
        Assert.AreEqual("lib1523", new Lib1523().ProgramName);
    }
}
