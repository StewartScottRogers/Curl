using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest.UnitTests;

#pragma warning disable CS0618 // lib670 exercises the deprecated curl_formadd.

[TestClass]
public sealed class Lib666To695Tests
{
    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_FailsEasyInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 666, "http://x/");

        await Lib666.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib667.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib668.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await new Lib670(TimeProvider.System).TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib674.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib676.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib694.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib695.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(8, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }

    [TestMethod]
    [DataRow(0, 0, "")]
    [DataRow((int)CurlCode.BadFunctionArgument, (int)CurlCode.BadFunctionArgument, "curl_mime_name() failed\n")]
    public async Task Lib666_PostsTheBinaryPartUnlessAPartCallFails(int answer, int exitCode, string error)
    {
        var mime = new FakeMimeBuilder((CurlCode)answer);
        int performs = 0;
        var engine = new FakeCurlEngine { CreatingMime = () => mime, Performing = _ => { performs++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 666, "http://x/666");

        LibTestResult result = await new Lib666().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.AreEqual(answer == 0 ? 1 : 0, performs);
        Assert.AreEqual("upfile", answer == 0 ? mime.Parts[0].Name : "upfile");
        byte[] buffer = Lib666.TestBuffer();
        Assert.HasCount(17000, buffer);
        Assert.AreEqual((byte)'\n', buffer[76]);
        Assert.AreEqual((byte)'Z', buffer[77]);
        Assert.AreEqual("lib666", new Lib666().ProgramName);
    }

    [TestMethod]
    [DataRow((int)CurlCode.Ok, "")]
    [DataRow((int)CurlCode.RecvError, "curl_easy_perform() failed\n")]
    public async Task Lib667AndLib668_PostTheCallbackPartsAndNameAFailedTransfer(int answer, string error)
    {
        foreach ((ILibTest program, string[] calls) in new (ILibTest, string[])[]
        {
            (new Lib667(), ["SetName", "SetEncoder base64", "SetDataCallback -1"]),
            (new Lib668(), ["SetName", "SetDataCallback 5"]),
        })
        {
            var mime = new FakeMimeBuilder();
            var engine = new FakeCurlEngine { CreatingMime = () => mime, PerformResult = (CurlCode)answer };
            var run = new PortRun(engine, 667, "http://x/667", "file668.txt");

            LibTestResult result = await program.RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(answer, result.ExitCode);
            Assert.AreEqual(error, run.Error.ToString());
            CollectionAssert.AreEqual(calls, mime.Parts[0].Calls);
        }

        Assert.AreEqual("lib667", new Lib667().ProgramName);
        Assert.AreEqual("lib668", new Lib668().ProgramName);
    }

    [TestMethod]
    public async Task Lib668_BuildsTheKnownSizeTheChunkedAndTheFileParts()
    {
        var mime = new FakeMimeBuilder();
        var run = new PortRun(new FakeCurlEngine { CreatingMime = () => mime }, 668, "http://x/668", "file668.txt");

        _ = await new Lib668().RunAsync(run.Context, CancellationToken.None);

        Assert.HasCount(3, mime.Parts);
        CollectionAssert.AreEqual(new[] { "SetName", "SetDataCallback -1" }, mime.Parts[1].Calls);
        CollectionAssert.AreEqual(new[] { "SetName", "SetFileData file668.txt" }, mime.Parts[2].Calls);
        Assert.AreEqual("field3", mime.Parts[2].Name);
    }

    [TestMethod]
    public void Lib667ByteReader_GivesDummyOneByteACall()
    {
        var reader = new Lib667.ByteReader();
        byte[] buffer = new byte[4];
        var read = new List<byte>();

        Assert.AreEqual(0, reader.Read([]));
        while (reader.Read(buffer) == 1)
        {
            read.Add(buffer[0]);
        }

        Assert.AreEqual(Lib667.TestData, System.Text.Encoding.ASCII.GetString([.. read]));
    }

    [TestMethod]
    public void Lib668ChunkReader_GivesAsMuchAsFitsThenTheEnd()
    {
        var reader = new Lib668.ChunkReader();
        byte[] buffer = new byte[3];

        Assert.AreEqual(3, reader.Read(buffer));
        Assert.AreEqual("dum", System.Text.Encoding.ASCII.GetString(buffer));
        Assert.AreEqual(2, reader.Read(buffer));
        Assert.AreEqual("my", System.Text.Encoding.ASCII.GetString(buffer, 0, 2));
        Assert.AreEqual(0, reader.Read(buffer));
    }

    [TestMethod]
    [DataRow(670, true, 1)]
    [DataRow(671, false, 1)]
    [DataRow(672, true, 0)]
    [DataRow(673, false, 0)]
    public async Task Lib670_BindsTheMimeOrTheFormAndRunsOnTheMultiOrTheEasyInterface(int testNumber, bool usesMulti, int mimeParts)
    {
        var mime = new FakeMimeBuilder();
        var form = new FakeFormBuilder();
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            CreatingMime = () => mime,
            CreatingForm = () => form,
            Performing = o => { performs++; return o.Callbacks.TransferInfo is null ? CurlCode.RecvError : CurlCode.Ok; },
        };
        var run = new PortRun(engine, testNumber, "http://x/670");
        var lib670 = new Lib670(new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        LibTestResult result = await lib670.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(usesMulti ? 0 : 1, performs);
        Assert.HasCount(mimeParts, mime.Parts);
        Assert.HasCount(1 - mimeParts, form.Calls);
        Assert.AreEqual("lib670", lib670.ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib670(null!));
    }

    [TestMethod]
    public async Task Lib670_RefusedMimeNameOrFormAdd_WritesItAndTransfersNothing()
    {
        var mimeRun = new PortRun(new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder(CurlCode.OutOfMemory) }, 670, "http://x/670");
        var formRun = new PortRun(new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder(), CreatingForm = () => new FakeFormBuilder { FailingCall = 1 } }, 672, "http://x/672");

        LibTestResult mimeResult = await new Lib670(TimeProvider.System).RunAsync(mimeRun.Context, CancellationToken.None);
        LibTestResult formResult = await new Lib670(TimeProvider.System).RunAsync(formRun.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.OutOfMemory, mimeResult.ExitCode);
        Assert.AreEqual("Something went wrong when building the mime structure: 27\n", mimeRun.Error.ToString());
        Assert.AreEqual("curl_formadd() = 1\n", formRun.Error.ToString());
    }

    [TestMethod]
    public void Lib670ReadThis_GivesAThenPausesThenBOnceUnpausedLateEnough()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        using var error = new StringWriter();
        CurlEasy easy = CurlGlobal.Init(engine).EasyInit();
        var pooh = new Lib670.ReadThis(easy, clock, error);
        byte[] buffer = new byte[1];

        Assert.AreEqual(0, pooh.Read([]));
        Assert.IsFalse(pooh.UnpauseOrGiveUp());
        Assert.AreEqual(1, pooh.Read(buffer));
        Assert.AreEqual((byte)'A', buffer[0]);
        Assert.AreEqual(ReadCallbackResult.Pause, pooh.Read(buffer));
        Assert.AreEqual(0, pooh.TransferInfo(0, 0, 0, 0));
        Assert.IsEmpty(engine.Pauses);
        clock.Advance(TimeSpan.FromSeconds(Lib670.PauseSeconds));
        Assert.AreEqual(0, pooh.TransferInfo(0, 0, 0, 0));
        CollectionAssert.AreEqual(new[] { CurlPause.Continue }, engine.Pauses);
        Assert.AreEqual(1, pooh.Read(buffer));
        Assert.AreEqual((byte)'B', buffer[0]);
        Assert.AreEqual(0, pooh.Read(buffer));
        Assert.AreEqual(ReadCallbackResult.Abort, pooh.Read(buffer));
        Assert.AreEqual("Read callback called after EOF\n", error.ToString());
        clock.Advance(TimeSpan.FromSeconds(3 * Lib670.PauseSeconds));
        Assert.AreEqual((int)CurlCode.AbortedByCallback, pooh.TransferInfo(0, 0, 0, 0));
        StringAssert.EndsWith(error.ToString(), "unpausing failed: drain problem?\n");
    }

    [TestMethod]
    public void Lib670ReadThis_TooShortAPause_GivesAAgain()
    {
        CurlEasy easy = CurlGlobal.Init(new FakeCurlEngine()).EasyInit();
        var pooh = new Lib670.ReadThis(easy, new ManualTimeProvider(DateTimeOffset.UnixEpoch), new StringWriter());
        byte[] buffer = new byte[1];

        _ = pooh.Read(buffer);
        _ = pooh.Read(buffer);
        _ = pooh.Read(buffer);

        Assert.AreEqual((byte)'A', buffer[0]);
    }

    [TestMethod]
    public async Task Lib670MultiLoop_RunsUntilTheTransferEndsOrThePauseLastsTooLong()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit();
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());
        var pooh = new Lib670.ReadThis(easy, clock, new StringWriter());
        _ = pooh.Read(new byte[1]);
        _ = pooh.Read(new byte[1]);
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () => clock.Advance(TimeSpan.FromSeconds(Lib670.PauseSeconds * 2));

        await Lib670.MultiLoopAsync(steps, multi, easy, pooh, CancellationToken.None);

        Assert.AreEqual(CurlCode.OperationTimedOut, steps.Result);
        CollectionAssert.AreEqual(new[] { CurlPause.Continue }, engine.Pauses);
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.MultiLoopAsync(null!, multi, easy, pooh, CancellationToken.None).AsTask().GetAwaiter().GetResult());
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.MultiLoopAsync(steps, null!, easy, pooh, CancellationToken.None).AsTask().GetAwaiter().GetResult());
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.MultiLoopAsync(steps, multi, easy, null!, CancellationToken.None).AsTask().GetAwaiter().GetResult());
    }

    [TestMethod]
    [DataRow((int)CurlMultiCode.Ok, (int)CurlMultiCode.Ok, (int)CurlMultiCode.Ok, 0)]
    [DataRow((int)CurlMultiCode.BadHandle, (int)CurlMultiCode.Ok, (int)CurlMultiCode.Ok, 0)]
    [DataRow((int)CurlMultiCode.Ok, (int)CurlMultiCode.OutOfMemory, (int)CurlMultiCode.Ok, (int)CurlCode.RecvError)]
    [DataRow((int)CurlMultiCode.Ok, (int)CurlMultiCode.Ok, (int)CurlMultiCode.OutOfMemory, (int)CurlCode.RecvError)]
    public async Task Lib670MultiLoop_FailedMultiCall_TakesTheDoneCode(int addResult, int performResult, int waitResult, int result)
    {
        var engine = new FakeCurlEngine();
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit();
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());
        engine.Multi.AddResult = (CurlMultiCode)addResult;
        engine.Multi.PerformResults.Enqueue(((CurlMultiCode)performResult, 1));
        engine.Multi.WaitResult = ((CurlMultiCode)waitResult, 0);
        engine.Multi.DoneResult = CurlCode.RecvError;

        await Lib670.MultiLoopAsync(steps, multi, easy, new Lib670.ReadThis(easy, TimeProvider.System, new StringWriter()), CancellationToken.None);

        Assert.AreEqual((CurlCode)result, steps.Result);
    }

    [TestMethod]
    public async Task Lib670MultiLoop_AMessageNotDone_LeavesTheResult()
    {
        var engine = new FakeCurlEngine();
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit();
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.BadHandle, 1));
        engine.Multi.DoneResult = CurlCode.RecvError;
        engine.Multi.DoneMessageType = (CurlMultiMessageType)99;

        await Lib670.MultiLoopAsync(steps, multi, easy, new Lib670.ReadThis(easy, TimeProvider.System, new StringWriter()), CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public void Lib670Binders_NullArguments_Throw()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder() });
        CurlEasy easy = library.EasyInit();
        var run = new PortRun(null, 670, "http://x/");
        using CurlMime mime = library.MimeInit();
        var pooh = new Lib670.ReadThis(easy, TimeProvider.System, run.Error);
        CurlForm? form = null;

        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindMime(null!, run.Steps, easy, mime, pooh));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindMime(run.Context, null!, easy, mime, pooh));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindMime(run.Context, run.Steps, easy, null!, pooh));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindMime(run.Context, run.Steps, easy, mime, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindForm(null!, run.Steps, library, easy, ref form, pooh));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindForm(run.Context, null!, library, easy, ref form, pooh));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib670.BindForm(run.Context, run.Steps, library, easy, ref form, null!));
    }

    [TestMethod]
    [DataRow(0, 0, 0, 2, "")]
    [DataRow(0, (int)CurlCode.CouldntConnect, (int)CurlCode.CouldntConnect, 1, "curl_easy_perform() failed with code 7 (")]
    [DataRow((int)CurlUrlCode.MalformedInput, 0, 0, 0, "problem setting CURLUPART")]
    public async Task Lib674_TransfersTheUrlHandleThenItsDuplicate(int setResult, int performResult, int exitCode, int performs, string error)
    {
        var handles = new List<object?>();
        var engine = new FakeCurlEngine
        {
            UrlSetResult = (CurlUrlCode)setResult,
            Performing = o => { handles.Add(o.Network.UrlHandle); return (CurlCode)performResult; },
        };
        var run = new PortRun(engine, 674, "http://x/674");

        LibTestResult result = await new Lib674().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), error);
        Assert.HasCount(performs, handles);
        Assert.IsTrue(handles.TrueForAll(h => h is not null));
        Assert.AreEqual("lib674", new Lib674().ProgramName);
    }

    [TestMethod]
    [DataRow(0, 0, 2, "")]
    [DataRow(1, (int)CurlCode.RecvError, 1, "retrieve 1 failed\n")]
    [DataRow(2, (int)CurlCode.RecvError, 2, "retrieve 2 failed\n")]
    public async Task Lib676_TransfersWithTheCookiesThenWithoutThem(int failing, int exitCode, int performs, string error)
    {
        var cookieFiles = new List<string?>();
        var engine = new FakeCurlEngine
        {
            Performing = o => { cookieFiles.Add(o.Http.CookieFiles.Count > 0 ? o.Http.CookieFiles[0] : null); return cookieFiles.Count == failing ? CurlCode.RecvError : CurlCode.Ok; },
        };
        var run = new PortRun(engine, 676, "http://x/676", "cookies676.txt");

        LibTestResult result = await new Lib676().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.HasCount(performs, cookieFiles);
        Assert.AreEqual("cookies676.txt", cookieFiles[0]);
        Assert.AreEqual("lib676", new Lib676().ProgramName);
    }

    [TestMethod]
    [DataRow(Lib694.AuthNtlm, 0, 2, "")]
    [DataRow(1L, 0, 2, "CURLINFO_HTTPAUTH_USED did not say NTLM\nCURLINFO_HTTPAUTH_USED did not say NTLM\n")]
    [DataRow(Lib694.AuthNtlm, (int)CurlCode.LoginDenied, 1, "")]
    public async Task Lib694_TransfersTwiceAndChecksNtlmWasUsed(long used, int answer, int performs, string output)
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { HttpAuthUsed = used },
            Performing = o => { urls.Add(o.Network.Url); return (CurlCode)answer; },
        };
        var run = new PortRun(engine, 694, "http://x/694", "http://x/path/mine");

        LibTestResult result = await new Lib694().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(answer, result.ExitCode);
        Assert.HasCount(performs, urls);
        Assert.AreEqual("http://x/694", urls[0]);
        Assert.AreEqual(performs == 2 ? "http://x/path/mine" : "http://x/694", urls[^1]);
        Assert.AreEqual(output, run.OutputText);
        Assert.AreEqual("lib694", new Lib694().ProgramName);
    }

    [TestMethod]
    [DataRow(0, 0, 2, "")]
    [DataRow(1, (int)CurlCode.RecvError, 1, "curl_easy_perform() 1 failed: ")]
    [DataRow(2, (int)CurlCode.RecvError, 2, "curl_easy_perform() 2 failed: ")]
    public async Task Lib695_PostsTheStructureThenReusesItAsSubparts(int failing, int exitCode, int performs, string error)
    {
        var mimes = new List<FakeMimeBuilder>();
        int count = 0;
        var engine = new FakeCurlEngine
        {
            CreatingMime = () => { mimes.Add(new FakeMimeBuilder()); return mimes[^1]; },
            Performing = _ => ++count == failing ? CurlCode.RecvError : CurlCode.Ok,
        };
        var run = new PortRun(engine, 695, "http://x/695");

        LibTestResult result = await new Lib695().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(performs, count);
        StringAssert.StartsWith(run.Error.ToString(), error);
        CollectionAssert.AreEqual(new[] { "SetData", "SetType text/html", "SetName" }, mimes[0].Parts[0].Calls);
        CollectionAssert.AreEqual(failing == 1 ? Array.Empty<string>() : new[] { "SetSubParts" }, failing == 1 ? Array.Empty<string>() : mimes[1].Parts[0].Calls.ToArray());
        Assert.AreEqual("lib695", new Lib695().ProgramName);
    }

    [TestMethod]
    public async Task Lib695_RefusedSubparts_WritesItAndPostsOnce()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder(CurlCode.OutOfMemory), Performing = _ => { performs++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 695, "http://x/695");

        LibTestResult result = await new Lib695().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.OutOfMemory, result.ExitCode);
        Assert.AreEqual(1, performs);
        StringAssert.StartsWith(run.Error.ToString(), "curl_mime_subparts() failed: ");
        CurlLibrary library = CurlGlobal.Init(engine);
        using CurlMime mime = library.MimeInit();
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib695.Reuse(null!, run.Steps, library.EasyInit(), mime, mime));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib695.Reuse(run.Context, null!, library.EasyInit(), mime, mime));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib695.Reuse(run.Context, run.Steps, null!, mime, mime));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib695.Reuse(run.Context, run.Steps, library.EasyInit(), mime, null!));
    }

    [TestMethod]
    public async Task Lib678_Check_SetsTheBlobAndPassesWhenItIsTaken()
    {
        var run = new PortRun(new FakeCurlEngine(), 678, "check");
        using var output = new MemoryStream();

        LibTestResult result = await new Lib678().RunAsync(run.Context, CancellationToken.None);
        Lib678.ReportSupport(CurlCode.UnknownOption, output);
        Lib678.ReportSupport(CurlCode.Ok, output);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual("CURLOPT_CAINFO_BLOB is not supported\n", System.Text.Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("lib678", new Lib678().ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib678.ReportSupport(CurlCode.Ok, null!));
    }

    [TestMethod]
    public async Task Lib678_Transfer_TrustsTheFileAsABlobOrEndsOutOfMemoryWithoutIt()
    {
        string folder = Directory.CreateTempSubdirectory("lib678").FullName;
        try
        {
            string file = Path.Combine(folder, "ca.pem");
            await File.WriteAllTextAsync(file, "PEM");
            ReadOnlyMemory<byte>? blob = null;
            var engine = new FakeCurlEngine { Performing = o => { blob = o.Tls.Origin.CertificateAuthorityBlob; return CurlCode.Ok; } };
            var run = new PortRun(engine, 678, "https://x/678", file);
            var missing = new PortRun(engine, 678, "https://x/678", Path.Combine(folder, "none.pem"));

            LibTestResult result = await new Lib678().RunAsync(run.Context, CancellationToken.None);
            LibTestResult missingResult = await new Lib678().RunAsync(missing.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("PEM", System.Text.Encoding.ASCII.GetString(blob!.Value.Span));
            Assert.AreEqual((int)CurlCode.OutOfMemory, missingResult.ExitCode);
            Assert.IsNull(Lib678.LoadFile(null));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("check", 0, "")]
    [DataRow("https://x/678", (int)CurlCode.FailedInit, "curl_easy_init() failed\n")]
    public async Task Lib678_OnACleanedUpLibrary_EndsAsUpstreamWithoutAHandle(string url, int result, string error)
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 678, url);

        await Lib678.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual((CurlCode)result, run.Steps.Result);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib678.TestAsync(null!, run.Steps, library, CancellationToken.None).AsTask().GetAwaiter().GetResult());
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib678.TestAsync(run.Context, null!, library, CancellationToken.None).AsTask().GetAwaiter().GetResult());
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib678.TestAsync(run.Context, run.Steps, null!, CancellationToken.None).AsTask().GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task Lib677_ConnectsSendsTheCommandAndPrintsTheReply()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var connection = new FakeConnectOnlyConnection();
        connection.Sends.Enqueue((CurlCode.Again, 0));
        connection.Sends.Enqueue((CurlCode.Ok, 3));
        connection.Receives.Enqueue((CurlCode.Again, string.Empty));
        connection.Receives.Enqueue((CurlCode.Ok, "+ idling\r\n"));
        var engine = new FakeCurlEngine { ConnectOnly = connection, Info = new TransferInfo { ActiveSocket = 5 } };
        engine.Multi.DoneResult = CurlCode.Ok;
        engine.Multi.ExtraSocketEvents = CurlWaitEvents.PollIn | CurlWaitEvents.PollOut;
        engine.Multi.Waiting = () => clock.Advance(TimeSpan.FromMilliseconds(400));
        var run = new PortRun(engine, 677, "imap://x/677");
        var lib677 = new Lib677(clock);

        LibTestResult result = await lib677.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Lib677.TestCommand, connection.Sent.ToString());
        Assert.AreEqual("Connected fine, extracted socket. Moving on\n+ idling\r\n\n", run.OutputText);
        Assert.AreEqual("lib677", lib677.ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib677(null!));
    }

    [TestMethod]
    [DataRow(true, (int)CurlCode.SendError, "curl_easy_send() failed, with code 55 (")]
    [DataRow(false, (int)CurlCode.RecvError, "curl_easy_recv() failed, with code 56 (")]
    public async Task Lib677_FailingSendOrReceive_EndsWithItsCode(bool failSend, int code, string error)
    {
        var connection = new FakeConnectOnlyConnection();
        connection.Sends.Enqueue(failSend ? ((CurlCode)code, 0) : (CurlCode.Ok, Lib677.TestCommand.Length));
        connection.Receives.Enqueue(((CurlCode)code, string.Empty));
        var engine = new FakeCurlEngine { ConnectOnly = connection, Info = new TransferInfo { ActiveSocket = 5 } };
        engine.Multi.DoneResult = CurlCode.Ok;
        engine.Multi.ExtraSocketEvents = CurlWaitEvents.PollIn | CurlWaitEvents.PollOut;
        var run = new PortRun(engine, 677, "imap://x/677");

        LibTestResult result = await new Lib677(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(code, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), error);
        Assert.AreEqual("Connected fine, extracted socket. Moving on\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib677_ASendOfNothing_DropsTheSocketAndPrintsNoReply()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var connection = new FakeConnectOnlyConnection();
        connection.Sends.Enqueue((CurlCode.Ok, 0));
        var engine = new FakeCurlEngine { ConnectOnly = connection, Info = new TransferInfo { ActiveSocket = 5 } };
        engine.Multi.DoneResult = CurlCode.Ok;
        engine.Multi.ExtraSocketEvents = CurlWaitEvents.PollOut;
        engine.Multi.Waiting = () => clock.Advance(TimeSpan.FromSeconds(1));
        var run = new PortRun(engine, 677, "imap://x/677");

        LibTestResult result = await new Lib677(clock).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, connection.Sent.ToString());
        Assert.AreEqual("Connected fine, extracted socket. Moving on\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib677_NoSocketOrAFailedPerform_EndsWithoutAReply()
    {
        var noSocket = new FakeCurlEngine();
        noSocket.Multi.DoneResult = CurlCode.Ok;
        var failing = new FakeCurlEngine();
        failing.Multi.PerformResult = (CurlMultiCode.BadHandle, 0);
        var refused = new FakeCurlEngine();
        refused.Multi.AddResult = CurlMultiCode.BadHandle;
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);

        foreach (FakeCurlEngine engine in new[] { noSocket, failing, refused })
        {
            var run = new PortRun(engine, 677, "imap://x/677");

            _ = await new Lib677(clock).RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(string.Empty, run.OutputText);
        }
    }

    [TestMethod]
    public async Task Lib677_MultiOrEasyInitFails_EndsThere()
    {
        CurlLibrary cleanedUp = await PortRun.CleanedUpLibraryAsync();
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        holder.Library = CurlGlobal.Init(engine);
        var first = new PortRun(engine, 677, "imap://x/677");
        var second = new PortRun(engine, 677, "imap://x/677");
        var lib677 = new Lib677(TimeProvider.System);

        await lib677.TestAsync(first.Context, first.Steps, cleanedUp, CancellationToken.None);
        await lib677.TestAsync(second.Context, second.Steps, holder.Library, CancellationToken.None);

        Assert.Contains("curl_multi_init() failed", first.Error.ToString());
        Assert.Contains("curl_easy_init() failed", second.Error.ToString());
    }

    [TestMethod]
    public async Task Lib677Exchange_SkipsOtherMessagesAndStopsReadingAFullBuffer()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var connection = new FakeConnectOnlyConnection();
        connection.Receives.Enqueue((CurlCode.Ok, new string('x', 1024)));
        var engine = new FakeCurlEngine { ConnectOnly = connection, Info = new TransferInfo { ActiveSocket = 5 } };
        engine.Multi.ExtraSocketEvents = CurlWaitEvents.PollIn | CurlWaitEvents.PollOut;
        engine.Multi.Waiting = () => clock.Advance(TimeSpan.FromSeconds(1));
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        await using CurlEasy added = library.EasyInit();
        await using CurlEasy other = library.EasyInit();
        _ = multi.AddHandle(added);
        var run = new PortRun(engine, 677, "imap://x/677");
        engine.Multi.DoneResult = CurlCode.Ok;
        engine.Multi.DoneMessageType = (CurlMultiMessageType)99;
        var skipping = new Lib677.Exchange(run.Steps, multi, other, run.Context);

        Assert.IsTrue(await skipping.RunAsync(clock, CancellationToken.None));
        engine.Multi.DoneMessageType = CurlMultiMessageType.Done;
        engine.Multi.DoneResult = CurlCode.Ok;
        Assert.IsTrue(await skipping.RunAsync(clock, CancellationToken.None));
        engine.Multi.DoneResult = CurlCode.Ok;
        var reading = new Lib677.Exchange(run.Steps, multi, added, run.Context);
        Assert.IsTrue(await reading.RunAsync(clock, CancellationToken.None));
        skipping.PrintReply();

        Assert.AreEqual("Connected fine, extracted socket. Moving on\n", run.OutputText);
        Assert.ThrowsExactly<ArgumentNullException>(() => reading.RunAsync(null!, CancellationToken.None).AsTask().GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task EasyPerformOrWriteAsync_WritesTheLineOnlyWhenTheTransferFails()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.RecvError };
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit();
        using var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = await steps.EasyPerformOrWriteAsync(easy, "retrieve 1 failed", CancellationToken.None);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(CurlCode.RecvError, steps.Result);
        Assert.AreEqual("retrieve 1 failed\n", error.ToString());
    }
}
