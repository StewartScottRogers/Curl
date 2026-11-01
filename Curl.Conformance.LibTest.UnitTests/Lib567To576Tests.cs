using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Protocols;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestRtspRequestsTests
{
    [TestMethod]
    public async Task SetNextStreamUri_NumbersEachRequestAndFailsMajorBadWithoutAUrl()
    {
        var uris = new List<string>();
        var run = new PortRun(new FakeCurlEngine { Performing = o => Record(uris, o) }, 568, "rtsp://x/568");
        await using CurlLibrary library = CurlGlobal.Init(run.Context.Engine!);
        await using Libcurl.Easy.CurlEasy easy = library.EasyInit();
        var requests = new LibTestRtspRequests(run.Steps, easy, "rtsp://x/568");

        Assert.IsTrue(await requests.PerformNextAsync([], CancellationToken.None));
        Assert.IsTrue(await requests.PerformNextAsync([], CancellationToken.None));
        Assert.IsFalse(new LibTestRtspRequests(run.Steps, easy, null).SetNextStreamUri());

        CollectionAssert.AreEqual(new[] { "rtsp://x/5680001", "rtsp://x/5680002" }, uris);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestRtspRequests(null!, easy, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestRtspRequests(run.Steps, null!, null));
    }

    [TestMethod]
    public async Task UploadAsync_CoversEveryOutcome()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "abc");
        try
        {
            int performs = 0;
            var run = new PortRun(new FakeCurlEngine { Performing = o => ++performs == 1 ? CurlCode.Ok : CurlCode.SendError }, 568);
            await using CurlLibrary library = CurlGlobal.Init(run.Context.Engine!);
            await using Libcurl.Easy.CurlEasy easy = library.EasyInit();
            var requests = new LibTestRtspRequests(run.Steps, easy, "rtsp://x/");

            Assert.IsTrue(await requests.UploadAsync(path, run.Error, RtspRequest.Announce, [], CancellationToken.None));
            Assert.IsFalse(await requests.UploadAsync(path, run.Error, RtspRequest.Announce, [], CancellationToken.None));
            Assert.AreEqual(CurlCode.SendError, run.Steps.Result);
            Assert.IsFalse(await requests.UploadAsync(null, run.Error, RtspRequest.Announce, [], CancellationToken.None));
            Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
            StringAssert.Contains(run.Error.ToString(), "Error opening file ''\n");
            Assert.IsFalse(await new LibTestRtspRequests(run.Steps, easy, null).UploadAsync(path, run.Error, RtspRequest.Announce, [], CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await requests.UploadAsync(path, null!, RtspRequest.Announce, [], CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await requests.UploadAsync(path, run.Error, RtspRequest.Announce, null!, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CreateOutput_NoPathGivesNoFile()
    {
        Assert.IsNull(LibTestRtspRequests.CreateOutput(null));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using (FileStream? file = LibTestRtspRequests.CreateOutput(path))
        {
            Assert.IsNotNull(file);
        }

        File.Delete(path);
    }

    private static CurlCode Record(List<string> uris, EasyOptions options)
    {
        uris.Add(options.Protocols.Rtsp.StreamUri);
        return CurlCode.Ok;
    }
}

[TestClass]
public sealed class Lib567To572Tests
{
    [TestMethod]
    public async Task Lib567_RunsOneOptionsRequest()
    {
        RtspRequest? request = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { request = o.Protocols.Rtsp.Request; return CurlCode.Ok; } }, 567, "rtsp://x/567");

        LibTestResult result = await new Lib567().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(RtspRequest.Options, request);
        Assert.AreEqual("lib567", new Lib567().ProgramName);
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_FailsMajorBad()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 567, "rtsp://x/");
        using var stream = new MemoryStream();

        await Lib567.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib568.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib569.TestAsync(run.Context, run.Steps, library, stream, CancellationToken.None);
        await Lib570.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib571.TestAsync(run.Context, run.Steps, library, stream, CancellationToken.None);
        await Lib572.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib574.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(7, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, (int)CurlCode.SendError)]
    [DataRow(2, (int)CurlCode.SendError)]
    [DataRow(3, (int)CurlCode.SendError)]
    [DataRow(4, (int)CurlCode.SendError)]
    public async Task Lib568_StopsAtTheFirstFailingRequest(int failingPerform, int exitCode)
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "v=0\n");
        try
        {
            var requests = new List<RtspRequest>();
            var run = new PortRun(new FakeCurlEngine { Performing = FailingAt(failingPerform, requests) }, 568, "rtsp://x/568", path);

            LibTestResult result = await new Lib568().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(exitCode, result.ExitCode);
            Assert.HasCount(failingPerform == 0 ? 4 : failingPerform, requests);
            Assert.AreEqual("lib568", new Lib568().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Lib568_NoUrlOrNoFile_FailsMajorBad()
    {
        var noUrl = new PortRun(new FakeCurlEngine(), 568);
        var noFile = new PortRun(new FakeCurlEngine(), 568, "rtsp://x/568", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await new Lib568().RunAsync(noUrl.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await new Lib568().RunAsync(noFile.Context, CancellationToken.None)).ExitCode);
        StringAssert.StartsWith(noFile.Error.ToString(), "open() failed with error ");

        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using Libcurl.Easy.CurlEasy easy = library.EasyInit();
        Assert.IsFalse(await Lib568.AnnounceFieldsAsync(noUrl.Steps, easy, new LibTestRtspRequests(noUrl.Steps, easy, null), CancellationToken.None));
    }

    [TestMethod]
    public async Task Lib569_WritesThreeSessionIds()
    {
        string path = Path.GetTempFileName();
        try
        {
            int performs = 0;
            var run = new PortRun(
                new FakeCurlEngine { Info = new TransferInfo { RtspSessionId = "abc" }, Performing = _ => ++performs == 1 ? CurlCode.BadFunctionArgument : CurlCode.Ok },
                569,
                "rtsp://x/569",
                path);

            LibTestResult result = await new Lib569().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(7, performs);
            Assert.AreEqual(string.Concat(Enumerable.Repeat("Got Session ID: [abc]\n", 3)), await File.ReadAllTextAsync(path));
            Assert.AreEqual("lib569", new Lib569().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(1, (int)CurlCode.Ok, (int)LibTestErrorCodes.MajorBad)]
    [DataRow(2, (int)CurlCode.RecvError, (int)CurlCode.RecvError)]
    [DataRow(3, (int)CurlCode.RecvError, (int)CurlCode.RecvError)]
    public async Task Lib569_FailingStep_AnswersItsCode(int failingPerform, int code, int exitCode)
    {
        int performs = 0;
        var run = new PortRun(
            new FakeCurlEngine { Performing = _ => ++performs == failingPerform ? (CurlCode)code : performs == 1 ? CurlCode.BadFunctionArgument : CurlCode.Ok },
            569,
            "rtsp://x/569",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        using var idFile = new MemoryStream();
        await using CurlLibrary library = CurlGlobal.Init(run.Context.Engine!);

        await Lib569.TestAsync(run.Context, run.Steps, library, idFile, CancellationToken.None);

        Assert.AreEqual(exitCode, (int)run.Steps.Result);
        if (failingPerform == 3)
        {
            Assert.AreEqual("Got Session ID: [(nil)]\n", Encoding.UTF8.GetString(idFile.ToArray()));
        }
    }

    [TestMethod]
    public async Task Lib569AndLib571_NoOutputFile_FailMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 569, "rtsp://x/");

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await new Lib569().RunAsync(run.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await new Lib571().RunAsync(run.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual("Could not open the Session ID File\nCould not open the protocol dump file\n", run.Error.ToString());
    }

    [TestMethod]
    [DataRow(new[] { 0 }, (int)LibTestErrorCodes.MajorBad, "Failed to detect CSeq mismatch")]
    [DataRow(new[] { (int)CurlCode.RtspCseqError, (int)CurlCode.SendError }, (int)CurlCode.SendError, "")]
    [DataRow(new[] { (int)CurlCode.RtspCseqError, 0, (int)CurlCode.RtspSessionError }, 0, "")]
    [DataRow(new[] { (int)CurlCode.RtspCseqError, 0, 0 }, (int)LibTestErrorCodes.Failure, "Failed to detect a Session ID mismatch")]
    public async Task Lib570_ChecksTheCSeqAndSessionErrors(int[] codes, int exitCode, string error)
    {
        var queue = new Queue<int>(codes);
        var run = new PortRun(new FakeCurlEngine { Performing = _ => (CurlCode)queue.Dequeue() }, 570, "rtsp://x/570");

        LibTestResult result = await new Lib570().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.AreEqual("lib570", new Lib570().ProgramName);
    }

    [TestMethod]
    public async Task Lib570_NoUrl_FailsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 570);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await new Lib570().RunAsync(run.Context, CancellationToken.None)).ExitCode);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public async Task Lib571_StopsAtTheFirstFailingRequestOrAfterTheReceives(int failingPerform)
    {
        string path = Path.GetTempFileName();
        try
        {
            int performs = 0;
            var run = new PortRun(
                new FakeCurlEngine { Performing = o => SendPacket(o, ++performs == failingPerform ? CurlCode.RecvError : CurlCode.Ok) },
                571,
                "rtsp://x/571",
                path);

            LibTestResult result = await new Lib571().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
            Assert.AreEqual(failingPerform == 5, run.Error.ToString().Contains("PLAY COMPLETE\nLOOPY LOOP!\n", StringComparison.Ordinal));
            Assert.AreEqual("lib571", new Lib571().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Lib571_ReceiveRestAsync_StopsAt19Packets()
    {
        var run = new PortRun(new FakeCurlEngine(), 571);
        var rtp = new Lib571.RtpWriter(run.Output, run.Error);
        for (int i = 0; i < 18; i++)
        {
            rtp.Write(Packet(Lib571.RtpWriter.RtpData.ToArray()));
        }

        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Performing = o => SendPacket(o, CurlCode.Ok) });
        await using Libcurl.Easy.CurlEasy easy = library.EasyInit();
        easy.SetOption(Libcurl.Options.CurlOption.Interleavefunction, (InterleaveCallback)rtp.Write);

        await Lib571.ReceiveRestAsync(run.Context, run.Steps, easy, rtp, CancellationToken.None);

        Assert.AreEqual(19, rtp.PacketCount);
        Assert.AreEqual(1, run.Error.ToString().Split("LOOPY LOOP!").Length - 1);
    }

    [TestMethod]
    public void Lib571_RtpWriter_ChecksSizeAndPayload()
    {
        var output = new MemoryStream();
        var error = new StringWriter();
        var rtp = new Lib571.RtpWriter(output, error);
        byte[] good = [.. Lib571.RtpWriter.RtpData, .. "$_12"u8];
        byte[] badMiddle = [.. "X_1234\n\0Rsdf"u8, .. "$_12"u8];
        byte[] badEnd = [.. Lib571.RtpWriter.RtpData, .. "$_1X"u8];

        Assert.AreEqual(20, rtp.Write(Packet(good)));
        Assert.AreEqual(20, rtp.Write(Packet(badMiddle)));
        Assert.AreEqual(20, rtp.Write(Packet(badEnd)));
        Assert.AreEqual(0, rtp.Write([(byte)'$', 1, 0, 9, 1]));

        Assert.AreEqual(
            "RTP: message size 16, channel 0\nRTP: message size 16, channel 0\nRTP PAYLOAD CORRUPTED [X_1234\n]\n"
                + "RTP: message size 16, channel 0\nRTP PAYLOAD END CORRUPTED (4), [$_1X]\nRTP: message size 1, channel 1\n"
                + "RTP embedded size (9) does not match the write size (1).\n",
            Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(3, rtp.PacketCount);
        Assert.AreEqual("packet count is 1\npacket count is 2\npacket count is 3\n", error.ToString());
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, (int)CurlCode.SendError)]
    [DataRow(2, (int)CurlCode.SendError)]
    [DataRow(3, (int)CurlCode.SendError)]
    [DataRow(4, (int)CurlCode.SendError)]
    [DataRow(5, (int)CurlCode.SendError)]
    public async Task Lib572_StopsAtTheFirstFailingRequest(int failingPerform, int exitCode)
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "packets_received\n");
        try
        {
            var requests = new List<RtspRequest>();
            var run = new PortRun(new FakeCurlEngine { Performing = FailingAt(failingPerform, requests) }, 572, "rtsp://x/572", path);

            LibTestResult result = await new Lib572().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(exitCode, result.ExitCode);
            Assert.HasCount(failingPerform == 0 ? 5 : failingPerform, requests);
            Assert.AreEqual("lib572", new Lib572().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Func<EasyOptions, CurlCode> FailingAt(int failingPerform, List<RtspRequest> requests) => o =>
    {
        requests.Add(o.Protocols.Rtsp.Request);
        return requests.Count == failingPerform ? CurlCode.SendError : CurlCode.Ok;
    };

    private static CurlCode SendPacket(EasyOptions options, CurlCode code)
    {
        options.Callbacks.Interleave?.Invoke(Packet(Lib571.RtpWriter.RtpData.ToArray()));
        return code;
    }

    private static byte[] Packet(byte[] payload) => [(byte)'$', 0, (byte)(payload.Length >> 8), (byte)payload.Length, .. payload];
}

[TestClass]
public sealed class Lib573To576Tests
{
    [TestMethod]
    public async Task Lib573_ZeroConnectTime_FailsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 573, "http://x/573");

        LibTestResult result = await new Lib573(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("connect time 0.000000e+00 is < epsilon 2.220446e-16\n", run.Error.ToString());
        Assert.AreEqual("lib573", new Lib573(TimeProvider.System).ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib573(null!));
    }

    [TestMethod]
    public async Task Lib573_ConnectTimeOrFailingLoop_AnswersWithoutTheCheck()
    {
        var timed = new PortRun(new FakeCurlEngine { Info = new TransferInfo { ConnectTime = TimeSpan.FromMilliseconds(1) } }, 573, "http://x/573");
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.OutOfMemory, 0);
        var failing = new PortRun(engine, 573, "http://x/573");

        Assert.AreEqual(0, (await new Lib573(TimeProvider.System).RunAsync(timed.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.Multi, (await new Lib573(TimeProvider.System).RunAsync(failing.Context, CancellationToken.None)).ExitCode);
        Assert.DoesNotContain("connect time", failing.Error.ToString());
    }

    [TestMethod]
    [DataRow(0, 0, 2)]
    [DataRow(1, (int)CurlCode.LoginDenied, 1)]
    [DataRow(2, (int)CurlCode.LoginDenied, 2)]
    public async Task Lib574_PerformsTwiceAndMatchesEveryFile(int failingPerform, int exitCode, int performCount)
    {
        int performs = 0;
        var run = new PortRun(
            new FakeCurlEngine { Performing = o => ++performs == failingPerform ? CurlCode.LoginDenied : Matching(o) },
            574,
            "ftp://x/*.txt");

        LibTestResult result = await new Lib574().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(performCount, performs);
        StringAssert.Contains(run.Error.ToString(), failingPerform == 0 ? "lib574: match string 'a.txt' against pattern '*.txt'\n" : "curl_easy_perform() failed 67\n");
        Assert.AreEqual("lib574", new Lib574().ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib574.Match(null!, "*", "a"));
    }

    [TestMethod]
    [DataRow(0, 0, 0, 3)]
    [DataRow(1, (int)CurlCode.LoginDenied, 0, 1)]
    [DataRow(2, (int)CurlCode.LoginDenied, 0, 2)]
    [DataRow(0, (int)LibTestErrorCodes.Multi, 1, 2)]
    public async Task Lib575_DownloadsThreeTimes(int failingPerform, int exitCode, int addFails, int performCount)
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == failingPerform ? CurlCode.LoginDenied : CurlCode.Ok };
        engine.Multi.AddResult = addFails == 1 ? CurlMultiCode.BadHandle : CurlMultiCode.Ok;
        engine.Multi.Performing = () => ++performs;
        var run = new PortRun(engine, 575, "ftp://x/*");

        LibTestResult result = await new Lib575(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(performCount, performs);
        Assert.AreEqual("lib575", new Lib575(TimeProvider.System).ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib575(null!));
    }

    [TestMethod]
    public async Task Lib575_InitFailures_AnswerTheirCodes()
    {
        var run = new PortRun(new FakeCurlEngine(), 575, "ftp://x/*");
        await Lib575.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), new LibTestClock(TimeProvider.System), CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);

        var holder = new PortRun.LibraryHolder();
        int performs = 0;
        var late = new PortRun(new FakeCurlEngine { Performing = _ => { PortRun.CleanUpWhen(holder, ++performs == 2); return CurlCode.Ok; } }, 575, "ftp://x/*");
        holder.Library = CurlGlobal.Init(late.Context.Engine!);
        await Lib575.TestAsync(late.Context, late.Steps, holder.Library, new LibTestClock(TimeProvider.System), CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, late.Steps.Result);
    }

    [TestMethod]
    public async Task Lib576_PrintsEachChunk()
    {
        var run = new PortRun(new FakeCurlEngine { Performing = Chunks }, 576, "ftp://x/*");

        LibTestResult result = await new Lib576().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        string rule = new string('=', 61) + "\n";
        string content = new string('-', 61) + "\n";
        Assert.AreEqual(
            rule + "Remains:      2\nFilename:     a.txt\nPermissions:  -rw-r--r-- (parsed => 644)\nSize:         35B\nUser:         u\nGroup:        g\nTime:         Apr 27  5:12\n"
                + "Filetype:     regular file\nContent:\n" + content + content
                + rule + "Remains:      1\nFilename:     someothertext.txt\nPermissions:  x\nSize:         0B\nFiletype:     symlink\nTarget:       (nil)\n"
                + "# THIS CONTENT WAS SKIPPED IN CHUNK_BGN CALLBACK #\n" + rule,
            run.OutputText);
        Assert.AreEqual("lib576", new Lib576().ProgramName);
    }

    [TestMethod]
    public async Task Lib576_CleanedUpLibrary_AnswersOutOfMemory()
    {
        var run = new PortRun(new FakeCurlEngine(), 576);

        await Lib576.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(CurlCode.OutOfMemory, run.Steps.Result);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(CurlFileType.Directory, "directory\n")]
    [DataRow(CurlFileType.NamedPipe, "other type\n")]
    [DataRow(CurlFileType.SymbolicLink, "symlink\nTarget:       t\n")]
    public void Lib576_ChunkPrinter_NamesEachFileType(CurlFileType type, string text)
    {
        var output = new MemoryStream();
        var printer = new Lib576.ChunkPrinter(output);

        Assert.AreEqual(ChunkBeginResult.Ok, printer.Begin(new CurlFileInfo { FileName = "d", FileType = type, Target = "t" }, 3));
        Assert.AreEqual(ChunkEndResult.Ok, printer.End());

        StringAssert.EndsWith(Encoding.UTF8.GetString(output.ToArray()), "Filetype:     " + text);
        Assert.ThrowsExactly<ArgumentNullException>(() => printer.Begin(null!, 1));
    }

    private static CurlCode Matching(EasyOptions options)
    {
        options.Callbacks.FileNameMatch?.Invoke("*.txt", "a.txt");
        return CurlCode.Ok;
    }

    private static CurlCode Chunks(EasyOptions options)
    {
        var file = new CurlFileInfo
        {
            FileName = "a.txt",
            FileType = CurlFileType.File,
            PermissionsText = "-rw-r--r--",
            Permissions = 420,
            Size = 35,
            User = "u",
            Group = "g",
            TimeText = "Apr 27  5:12",
        };
        options.Callbacks.ChunkBegin!(file, 2);
        options.Callbacks.ChunkEnd!();
        options.Callbacks.ChunkBegin!(new CurlFileInfo { FileName = "someothertext.txt", FileType = CurlFileType.SymbolicLink, PermissionsText = "x" }, 1);
        options.Callbacks.ChunkEnd!();
        return CurlCode.Ok;
    }
}
