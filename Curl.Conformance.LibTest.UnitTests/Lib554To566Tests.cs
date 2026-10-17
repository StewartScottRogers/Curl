using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Info;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

#pragma warning disable CS0618 // Lib554 exercises the deprecated curl_formadd.
[TestClass]
public sealed class Lib554Tests
{
    [TestMethod]
    public async Task RunAsync_FirstPostSucceeds_BuildsBothFormsAndPostsTwice()
    {
        var builders = new List<FakeFormBuilder>();
        int performs = 0;
        var run = new PortRun(
            new FakeCurlEngine { CreatingForm = () => Add(builders, new FakeFormBuilder()), Performing = _ => ++performs == 1 ? CurlCode.Ok : CurlCode.SendError },
            554,
            "http://x/554");

        LibTestResult result = await new Lib554().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.AreEqual(2, performs);
        Assert.HasCount(2, builders);
        Assert.AreEqual("sendfile", builders[0].Calls[0][0].Value);
        Assert.AreEqual(CurlFormOption.ContentsLength, builders[0].Calls[0][2].Option);
        Assert.AreEqual("sendfile alternative", builders[1].Calls[0][0].Value);
        Assert.AreEqual(CurlFormOption.ContentLength, builders[1].Calls[0][2].Option);
        Assert.HasCount(5, builders[1].Calls);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual("lib554", new Lib554().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_FirstPostFails_StopsThere()
    {
        int performs = 0;
        var run = new PortRun(
            new FakeCurlEngine { CreatingForm = () => new FakeFormBuilder(), Performing = _ => { performs++; return CurlCode.AbortedByCallback; } },
            587,
            "http://x/587");

        LibTestResult result = await new Lib554().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task RunAsync_EveryFormaddFails_PrintsEachCode()
    {
        var run = new PortRun(new FakeCurlEngine { CreatingForm = () => new FakeFormBuilder { AddResult = CurlFormAddCode.Memory } }, 554, "http://x/554");

        await new Lib554().RunAsync(run.Context, CancellationToken.None);

        StringAssert.StartsWith(run.OutputText, "curl_formadd(1) = 1\ncurl_formadd(2) = 1\ncurl_formadd(3) = 1\ncurl_formadd(4) = 1\ncurl_formadd(5) = 1\n");
    }

    [TestMethod]
    public async Task TestOnceAsync_EasyInitFails_AnswersMajorBad()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(new FakeCurlEngine { CreatingForm = () => CleaningUp(holder) }, 554, "http://x/554");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        bool succeeded = await Lib554.TestOnceAsync(run.Context, run.Steps, holder.Library, oldStyle: true, CancellationToken.None);

        Assert.IsFalse(succeeded);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }

    [TestMethod]
    public void StreamReader_GivesOneByteACallFromEachStreamInTurn()
    {
        var reader = new Lib554.StreamReader();
        reader.AddStream();
        reader.AddStream();
        byte[] buffer = new byte[10];
        var read = new List<byte>();

        while (reader.Read(buffer) == 1)
        {
            read.Add(buffer[0]);
        }

        Assert.AreEqual(Lib554.TestData + Lib554.TestData, System.Text.Encoding.ASCII.GetString([.. read]));
        Assert.AreEqual(0, reader.Read(Span<byte>.Empty));
        Assert.AreEqual(0x10000000L, Lib554.Abort(buffer));
    }

    private static FakeFormBuilder Add(List<FakeFormBuilder> builders, FakeFormBuilder builder)
    {
        builders.Add(builder);
        return builder;
    }

    private static FakeFormBuilder CleaningUp(PortRun.LibraryHolder holder)
    {
        PortRun.CleanUpWhen(holder, true);
        return new FakeFormBuilder();
    }
}
#pragma warning restore CS0618

[TestClass]
public sealed class Lib555Tests
{
    [TestMethod]
    public async Task RunAsync_StopsAtTheIoctlOption()
    {
        var run = new PortRun(new FakeCurlEngine(), 555, "http://x/555", "http://proxy", "u:p");

        LibTestResult result = await new Lib555(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual("lib555", new Lib555(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib555(null!));
}

[TestClass]
public sealed class Lib556Tests
{
    [TestMethod]
    public async Task RunAsync_Case696_ExchangesTwiceAndPrintsTheReplies()
    {
        var connection = new FakeConnectOnlyConnection();
        connection.Sends.Enqueue((CurlCode.Again, 0));
        connection.Sends.Enqueue((CurlCode.Ok, 5));
        connection.Receives.Enqueue((CurlCode.Again, string.Empty));
        connection.Receives.Enqueue((CurlCode.Ok, "one"));
        connection.Receives.Enqueue((CurlCode.Again, string.Empty));
        connection.Receives.Enqueue((CurlCode.Ok, string.Empty));
        connection.Receives.Enqueue((CurlCode.Ok, "two"));
        var run = new PortRun(new FakeCurlEngine { ConnectOnly = connection }, 696, "http://x/696");

        LibTestResult result = await new Lib556().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("onetwo", run.OutputText);
        Assert.AreEqual("GET /696 HTTP/1.1\r\nHost: ninja\r\n\r\nGET /696 HTTP/1.1\r\nHost: ninja\r\n\r\n", connection.Sent.ToString());
        Assert.AreEqual("lib556", new Lib556().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_Case556_ExchangesOnce()
    {
        var connection = new FakeConnectOnlyConnection();
        var run = new PortRun(new FakeCurlEngine { ConnectOnly = connection }, 556, "http://x/556");

        LibTestResult result = await new Lib556().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("GET /556 HTTP/1.1\r\nHost: ninja\r\n\r\n", connection.Sent.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ConnectFailsOrTheSecondConnectFails_AnswersItsCode()
    {
        int performs = 0;
        var refused = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 556, "http://x/556");
        var secondRefused = new PortRun(
            new FakeCurlEngine { ConnectOnly = new FakeConnectOnlyConnection(), Performing = _ => ++performs == 1 ? CurlCode.Ok : CurlCode.CouldntConnect },
            696,
            "http://x/696");

        LibTestResult result = await new Lib556().RunAsync(refused.Context, CancellationToken.None);
        LibTestResult secondResult = await new Lib556().RunAsync(secondRefused.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual((int)CurlCode.CouldntConnect, secondResult.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_SendOrReceiveFails_AnswersFailure()
    {
        var sendFails = new FakeConnectOnlyConnection();
        sendFails.Sends.Enqueue((CurlCode.SendError, 0));
        var receiveFails = new FakeConnectOnlyConnection();
        receiveFails.Receives.Enqueue((CurlCode.RecvError, string.Empty));
        var sending = new PortRun(new FakeCurlEngine { ConnectOnly = sendFails }, 696, "http://x/696");
        var receiving = new PortRun(new FakeCurlEngine { ConnectOnly = receiveFails }, 696, "http://x/696");

        LibTestResult sendResult = await new Lib556().RunAsync(sending.Context, CancellationToken.None);
        LibTestResult receiveResult = await new Lib556().RunAsync(receiving.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, sendResult.ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.Failure, receiveResult.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 556, "http://x/");

        await Lib556.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib558And559Tests
{
    [TestMethod]
    public async Task RunAsync_Lib558_EscapesAndPrintsNothing()
    {
        var run = new PortRun(new FakeCurlEngine(), 558, "http://x/558");

        LibTestResult result = await new Lib558().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual("lib558", new Lib558().ProgramName);
        Assert.AreEqual(14, Lib558.Bytes.Length);
    }

    [TestMethod]
    public async Task RunAsync_Lib559_SetsABufferSizeAndAnswersTheTransfersCode()
    {
        long bufferSize = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = options => Capture(options.Transfer.BufferSize, ref bufferSize) }, 559, "http://x/559");

        LibTestResult result = await new Lib559().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);
        Assert.IsGreaterThan(0L, bufferSize);
        Assert.AreEqual("lib559", new Lib559().ProgramName);
    }

    [TestMethod]
    public async Task TestAsync_Lib559OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 559, "http://x/");

        await Lib559.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }

    private static CurlCode Capture(long value, ref long captured)
    {
        captured = value;
        return CurlCode.PartialFile;
    }
}

[TestClass]
public sealed class Lib560And564Tests
{
    [TestMethod]
    public async Task RunAsync_Lib560_RunsTheMultiLoopToTheEnd()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((Curl.Api.Codes.CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 560, "https://x/560");

        LibTestResult result = await new Lib560(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib560", new Lib560(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_Lib564_SaysWhereItStartsAndRunsTheLoop()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((Curl.Api.Codes.CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 564, "http://x/564", "127.0.0.1:1080");

        LibTestResult result = await new Lib564(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Start at URL 0\n", run.Error.ToString());
        Assert.AreEqual("lib564", new Lib564(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_Lib564AddHandleFails_SaysNothing()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = Curl.Api.Codes.CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 564, "http://x/564", "127.0.0.1:1080");

        LibTestResult result = await new Lib564(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(run.Error.ToString().Contains("Start at URL", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TestAsync_Lib564OnACleanedUpLibrary_FailsEasyInitThenMultiInit()
    {
        var easyFails = new PortRun(null, 564, "http://x/");
        var holder = new PortRun.LibraryHolder();
        var multiFails = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), 564, "http://x/");
        holder.Library = CurlGlobal.Init(multiFails.Context.Engine!);
        var clock = new LibTestClock(TimeProvider.System);

        await Lib564.TestAsync(easyFails.Context, easyFails.Steps, await PortRun.CleanedUpLibraryAsync(), clock, TimeProvider.System, CancellationToken.None);
        await Lib564.TestAsync(multiFails.Context, multiFails.Steps, holder.Library, clock, TimeProvider.System, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, easyFails.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, multiFails.Steps.Result);
    }

    [TestMethod]
    public void Constructors_NullTimeProvider_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib560(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib564(null!));
    }
}

[TestClass]
public sealed class Lib562Tests
{
    [TestMethod]
    public async Task RunAsync_APort_SetsItAndAnswersTheTransfersCode()
    {
        long port = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = options => Capture(options.Network.Port, ref port) }, 562, "ftp://x/562;type=A", "8993");

        LibTestResult result = await new Lib562().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.FtpCouldntRetrFile, result.ExitCode);
        Assert.AreEqual(8993, port);
        Assert.AreEqual("lib562", new Lib562().ProgramName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("x1")]
    [DataRow("65536")]
    [DataRow("99999999999999999999")]
    public async Task RunAsync_NoPortNumber_AnswersMajorBad(string? argument)
    {
        var run = argument is null ? new PortRun(new FakeCurlEngine(), 562, "ftp://x/") : new PortRun(new FakeCurlEngine(), 562, "ftp://x/", argument);

        LibTestResult result = await new Lib562().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    [TestMethod]
    public void TryParsePort_TrailingText_ReadsTheLeadingDigits()
    {
        Assert.IsTrue(Lib562.TryParsePort("65535abc", out long port));
        Assert.AreEqual(65535, port);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 562, "ftp://x/");

        await Lib562.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), 21, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }

    private static CurlCode Capture(long value, ref long captured)
    {
        captured = value;
        return CurlCode.FtpCouldntRetrFile;
    }
}

[TestClass]
public sealed class Lib566Tests
{
    [TestMethod]
    public async Task RunAsync_TransferSucceeds_WritesTheContentLength()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lib566-{Guid.NewGuid():N}");
        try
        {
            var run = new PortRun(new FakeCurlEngine { Info = new TransferInfo { ContentLengthDownload = -1 } }, 566, "http://x/566", path);

            LibTestResult result = await new Lib566().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("CL -1\n", await File.ReadAllTextAsync(path));
            Assert.AreEqual("lib566", new Lib566().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task RunAsync_TransferFails_WritesNothing()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.PartialFile }, 566, "http://x/566");

        LibTestResult result = await new Lib566().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);
    }

    [TestMethod]
    public async Task WriteContentLengthAsync_NoFileOrAFolder_WritesNothing()
    {
        await Lib566.WriteContentLengthAsync(null, 3, CancellationToken.None);
        await Lib566.WriteContentLengthAsync(Path.GetTempPath(), 3, CancellationToken.None);

        Assert.IsTrue(Directory.Exists(Path.GetTempPath()));
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 566, "http://x/");

        await Lib566.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}
