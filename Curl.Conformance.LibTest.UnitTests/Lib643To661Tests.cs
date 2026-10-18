using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Protocols;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Misc.ErrorText;

namespace Curl.Conformance.LibTest.UnitTests;

#pragma warning disable CS0618 // lib650 exercises the deprecated curl_formadd.

[TestClass]
public sealed class LibTestMimeCallsTests
{
    [TestMethod]
    public void FirstFailure_StopsAtTheFirstFailingCall()
    {
        var ran = new List<int>();

        CurlCode none = LibTestMimeCalls.FirstFailure([() => { ran.Add(0); return CurlCode.Ok; }], out int noneFailed);
        CurlCode second = LibTestMimeCalls.FirstFailure([() => CurlCode.Ok, () => CurlCode.OutOfMemory, () => { ran.Add(2); return CurlCode.Ok; }], out int secondFailed);

        Assert.AreEqual(CurlCode.Ok, none);
        Assert.AreEqual(-1, noneFailed);
        Assert.AreEqual(CurlCode.OutOfMemory, second);
        Assert.AreEqual(1, secondFailed);
        CollectionAssert.AreEqual(new[] { 0 }, ran);
        Assert.ThrowsExactly<ArgumentNullException>(() => LibTestMimeCalls.FirstFailure(null!, out _));
    }
}

[TestClass]
public sealed class Lib643To661Tests
{
    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_FailsEasyInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 643, "http://x/");

        await Lib643.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib652.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib654.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await new Lib655(TimeProvider.System).TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib661.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib653.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib658.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib659.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        string error = run.Error.ToString();
        Assert.AreEqual(8, error.Split("curl_easy_init() failed").Length - 1);
        Assert.Contains($"test encountered error {(int)LibTestErrorCodes.MajorBad}\n", error);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task FormPorts_EasyInitFailsAfterTheForm_StopThere()
    {
        foreach (Func<PortRun, CurlLibrary, ValueTask> test in new Func<PortRun, CurlLibrary, ValueTask>[]
        {
            (r, l) => Lib650.TestAsync(r.Context, r.Steps, l, CancellationToken.None),
            (r, l) => Lib651.TestAsync(r.Context, r.Steps, l, CancellationToken.None),
        })
        {
            var holder = new PortRun.LibraryHolder();
            var engine = new FakeCurlEngine { CreatingForm = () => { PortRun.CleanUpWhen(holder, true); return new FakeFormBuilder(); } };
            holder.Library = CurlGlobal.Init(engine);
            var run = new PortRun(engine, 650, "http://x/650", "file");

            await test(run, holder.Library);

            Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
            Assert.Contains("curl_easy_init() failed", run.Error.ToString());
        }
    }

    [TestMethod]
    [DataRow(643, 6L, "sendfile")]
    [DataRow(645, -1L, "sendfile")]
    public async Task Lib643_PostsBothStylesThenFindsTheCycleNotRefused(int testNumber, long size, string firstName)
    {
        var mimes = new List<FakeMimeBuilder>();
        var engine = new FakeCurlEngine { CreatingMime = () => Add(mimes, new FakeMimeBuilder()) };
        var run = new PortRun(engine, testNumber, "http://x/643");

        LibTestResult result = await new Lib643().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.HasCount(3, mimes);
        Assert.AreEqual(firstName, mimes[0].Parts[0].Name);
        Assert.AreEqual("sendfile alternative", mimes[1].Parts[0].Name);
        CollectionAssert.AreEqual(new[] { "SetName", $"SetDataCallback {size}", "SetFileName filename 2 " }, mimes[1].Parts[0].Calls);
        CollectionAssert.AreEqual(new[] { "SetName", "SetFileName somefile.txt", "SetData" }, mimes[0].Parts[4].Calls);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual("lib643", new Lib643().ProgramName);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    public async Task Lib643_FailingPost_StopsBeforeTheNext(int failing, int posts)
    {
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            CreatingMime = () => new FakeMimeBuilder(),
            Performing = _ => ++performs == failing ? CurlCode.RecvError : CurlCode.Ok,
        };
        var run = new PortRun(engine, 643, "http://x/643");

        LibTestResult result = await new Lib643().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual(posts, performs);
    }

    [TestMethod]
    public async Task Lib643_FailingPartCallsAndARefusedCycle_PrintsEachPartAndPasses()
    {
        var engine = new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder(CurlCode.BadFunctionArgument) };
        await using CurlLibrary library = CurlGlobal.Init(engine);
        using var output = new MemoryStream();
        using CurlMime mime = library.MimeInit();

        Lib643.BuildParts(mime, output, -1, oldStyle: false);

        Assert.AreEqual(CurlCode.Ok, Lib643.CyclicAdd(library));
        string text = System.Text.Encoding.ASCII.GetString(output.ToArray());
        Assert.AreEqual(5, text.Split($"= {CurlErrorText.Easy(CurlCode.BadFunctionArgument)}\n").Length - 1);
        StringAssert.StartsWith(text, "curl_mime_xxx(1) = ");
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib643.CyclicAdd(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib643.BuildParts(null!, output, 1, true));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib643.BuildParts(mime, null!, 1, true));
    }

    [TestMethod]
    public void Lib643_ByteReader_GivesTheDataOneByteACall()
    {
        var reader = new Lib643.ByteReader();
        byte[] buffer = new byte[4];
        var read = new List<byte>();

        Assert.AreEqual(0, reader.Read([]));
        while (reader.Read(buffer) == 1)
        {
            read.Add(buffer[0]);
        }

        Assert.AreEqual(Lib643.TestData, System.Text.Encoding.ASCII.GetString([.. read]));
    }

    [TestMethod]
    [DataRow(0, 0, 6)]
    [DataRow(1, (int)LibTestErrorCodes.MajorBad, 1)]
    [DataRow(2, (int)LibTestErrorCodes.MajorBad, 2)]
    [DataRow(3, (int)LibTestErrorCodes.MajorBad, 3)]
    [DataRow(4, (int)LibTestErrorCodes.MajorBad, 4)]
    [DataRow(5, (int)LibTestErrorCodes.MajorBad, 5)]
    [DataRow(6, (int)LibTestErrorCodes.MajorBad, 6)]
    public async Task Lib650_AddsTheSixFieldsAndPostsOnlyWhenAllSucceed(int failingCall, int exitCode, int calls)
    {
        var form = new FakeFormBuilder { FailingCall = failingCall };
        int performs = 0;
        var engine = new FakeCurlEngine { CreatingForm = () => form, Performing = _ => { performs++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 650, "http://x/650", "data.txt");

        LibTestResult result = await new Lib650().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.HasCount(calls, form.Calls);
        Assert.AreEqual(failingCall == 0 ? 1 : 0, performs);
        Assert.AreEqual(failingCall == 0 ? string.Empty : $"curl_formadd({failingCall}) = 1\n", run.OutputText);
        if (calls >= 3)
        {
            var array = (CurlFormArgument[])form.Calls[1][2].Value!;
            Assert.AreEqual((byte)'u', ((byte[])array[0].Value!)[0]);
        }

        if (failingCall == 0)
        {
            Assert.AreEqual("3", form.Calls[4][1].Value);
            Assert.AreEqual("-", form.Calls[5][1].Value);
        }

        Assert.AreEqual("lib650", new Lib650().ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => { CurlForm? f = null; Lib650.BuildForm(CurlGlobal.Init(engine), ref f, null!, "x"); });
    }

    [TestMethod]
    [DataRow(0, "")]
    [DataRow(1, "curl_formadd(1) = 1\n")]
    public async Task Lib651_PostsTheLongFieldEvenWhenTheAddFails(int failingCall, string output)
    {
        var form = new FakeFormBuilder { FailingCall = failingCall };
        int performs = 0;
        var engine = new FakeCurlEngine { CreatingForm = () => form, Performing = _ => { performs++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 651, "http://x/651");

        LibTestResult result = await new Lib651().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(1, performs);
        Assert.AreEqual(output, run.OutputText);
        Assert.AreEqual(Lib651.TestBuffer, form.Calls[0][1].Value);
        Assert.HasCount(16999, Lib651.TestBuffer);
        Assert.AreEqual(new string('Q', 999), Lib651.TestBuffer[16000..]);
        Assert.AreEqual("lib651", new Lib651().ProgramName);
    }

    [TestMethod]
    [DataRow(0, 0, "")]
    [DataRow((int)CurlCode.BadFunctionArgument, (int)CurlCode.BadFunctionArgument, "curl_mime_filename() failed\n")]
    public async Task Lib652_MailsTheEncodedPartUnlessAPartCallFails(int answer, int exitCode, string error)
    {
        var mime = new FakeMimeBuilder((CurlCode)answer);
        string? from = null;
        var engine = new FakeCurlEngine { CreatingMime = () => mime, Performing = o => { from = o.Protocols.Smtp.MailFrom; return CurlCode.Ok; } };
        var run = new PortRun(engine, 652, "smtp://x/652");

        LibTestResult result = await new Lib652().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.AreEqual(answer == 0 ? "somebody@example.com" : null, from);
        Assert.AreEqual(answer == 0 ? 4 : 1, mime.Parts[0].Calls.Count);
        byte[] buffer = Lib652.TestBuffer();
        Assert.HasCount(17000, buffer);
        Assert.AreEqual((byte)'B', buffer[10]);
        Assert.AreEqual((byte)'A', buffer[260]);
        Assert.AreEqual("lib652", new Lib652().ProgramName);
    }

    [TestMethod]
    [DataRow(0, 2, 0)]
    [DataRow((int)CurlCode.RecvError, 1, (int)CurlCode.RecvError)]
    public async Task Lib653_PostsAgainWithTheLongerValue(int firstResult, int performs, int exitCode)
    {
        var mime = new FakeMimeBuilder();
        var sent = new List<string>();
        var engine = new FakeCurlEngine
        {
            CreatingMime = () => mime,
            Performing = _ => { sent.Add(System.Text.Encoding.ASCII.GetString(mime.Parts[0].Data!)); return sent.Count == 1 ? (CurlCode)firstResult : CurlCode.Ok; },
        };
        var run = new PortRun(engine, 653, "http://x/653");

        LibTestResult result = await new Lib653().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.HasCount(performs, sent);
        Assert.AreEqual("short value", sent[0]);
        Assert.AreEqual(performs == 2 ? "long value for length change" : "short value", sent[^1]);
        Assert.AreEqual("lib653", new Lib653().ProgramName);
    }

    [TestMethod]
    [DataRow(0, 0, 2, "")]
    [DataRow(1, (int)CurlCode.RecvError, 1, "curl_easy_perform(original) failed\n")]
    [DataRow(2, (int)CurlCode.RecvError, 2, "curl_easy_perform(duplicated) failed\n")]
    public async Task Lib654_PerformsTheOriginalThenTheDuplicate(int failing, int exitCode, int performs, string error)
    {
        var mime = new FakeMimeBuilder();
        int count = 0;
        var engine = new FakeCurlEngine { CreatingMime = () => mime, Performing = _ => ++count == failing ? CurlCode.RecvError : CurlCode.Ok };
        var run = new PortRun(engine, 654, "http://x/654", "file654.txt");

        LibTestResult result = await new Lib654().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(performs, count);
        Assert.AreEqual(error, run.Error.ToString());
        CollectionAssert.AreEqual(new[] { "SetData", "SetName", "SetType application/X-Greeting", "SetEncoder base64", "SetHeaders X-Test-Number: 654" }, mime.Parts[0].Calls);
        CollectionAssert.AreEqual(new[] { "SetFileData file654.txt" }, mime.Parts[1].Calls);
        CollectionAssert.AreEqual(new[] { "SetDataCallback -1" }, mime.Parts[2].Calls);
        Assert.AreEqual("lib654", new Lib654().ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib654.BuildParts(null!, "x"));
    }

    [TestMethod]
    [DataRow(true, -1, -1, 0, "")]
    [DataRow(false, (int)CurlCode.Ok, -1, (int)LibTestErrorCodes.Failure, "but instead returned error 0\n")]
    [DataRow(false, (int)CurlCode.CouldntResolveHost, -1, (int)CurlCode.CouldntResolveHost, "but instead returned error 6\n")]
    [DataRow(true, -1, (int)CurlCode.CouldntConnect, (int)CurlCode.CouldntConnect, "curl_easy_perform failed.\n")]
    [DataRow(false, (int)CurlCode.AbortedByCallback, (int)CurlCode.Ok, (int)LibTestErrorCodes.Failure, "Unexpected number of callbacks: 0\n")]
    public async Task Lib655_RefusesThenAllowsTheResolverStart(bool callsBack, int first, int second, int exitCode, string error)
    {
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                int answer = callsBack ? o.Callbacks.ResolverStart!(0) : 0;
                int fixedResult = ++performs == 1 ? first : second;
                return fixedResult >= 0 ? (CurlCode)fixedResult : answer == 0 ? CurlCode.Ok : CurlCode.AbortedByCallback;
            },
        };
        var run = new PortRun(engine, 655, "http://failthis/655", "http://x/655");
        var lib655 = new Lib655(new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        LibTestResult result = await lib655.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        StringAssert.EndsWith(run.Error.ToString(), error);
        Assert.AreEqual("lib655", lib655.ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib655(null!));
    }

    [TestMethod]
    [DataRow(0, 0, 0, "")]
    [DataRow(0, (int)CurlCode.CouldntConnect, (int)CurlCode.CouldntConnect, "curl_easy_perform() failed with code 7 (")]
    [DataRow((int)CurlUrlCode.MalformedInput, 0, 0, "problem setting CURLUPART")]
    public async Task Lib658AndLib659_TransferTheUrlHandle(int setResult, int performResult, int exitCode, string error)
    {
        foreach (ILibTest program in new ILibTest[] { new Lib658(), new Lib659() })
        {
            object? handle = null;
            var engine = new FakeCurlEngine
            {
                UrlSetResult = (CurlUrlCode)setResult,
                Performing = o => { handle = o.Network.UrlHandle; return (CurlCode)performResult; },
            };
            var run = new PortRun(engine, 658, "http://x/658");

            LibTestResult result = await program.RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(exitCode, result.ExitCode);
            StringAssert.Contains(run.Error.ToString(), error);
            Assert.AreEqual(setResult == 0, handle is not null);
        }

        Assert.AreEqual("lib658", new Lib658().ProgramName);
        Assert.AreEqual("lib659", new Lib659().ProgramName);
    }

    [TestMethod]
    public async Task Lib661_RunsTheEightTransfersWithTheirFileMethods()
    {
        var methods = new List<FtpFileMethod>();
        var engine = new FakeCurlEngine
        {
            Performing = o => { methods.Add(o.Protocols.Ftp.FileMethod); return methods.Count <= 5 ? CurlCode.RemoteFileNotFound : CurlCode.Ok; },
        };
        var run = new PortRun(engine, 661, "ftp://x/");

        LibTestResult result = await new Lib661().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { FtpFileMethod.SingleCwd, FtpFileMethod.SingleCwd, FtpFileMethod.NoCwd, FtpFileMethod.SingleCwd, FtpFileMethod.NoCwd, FtpFileMethod.NoCwd, FtpFileMethod.SingleCwd, FtpFileMethod.NoCwd },
            methods);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual("lib661", new Lib661().ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib661.Transfers(null!));
    }

    [TestMethod]
    [DataRow(2, (int)CurlCode.Ok, 0, "")]
    [DataRow(1, (int)CurlCode.LoginDenied, (int)CurlCode.LoginDenied, "test encountered error 67\n")]
    public async Task Lib661_UnexpectedAnswer_EndsThere(int performs, int answer, int exitCode, string error)
    {
        int count = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++count == performs ? (CurlCode)answer : CurlCode.RemoteFileNotFound };
        var run = new PortRun(engine, 661, "ftp://x/");

        LibTestResult result = await new Lib661().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(performs, count);
        Assert.AreEqual(error, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib661_EasyInitFailingOnAFreshHandle_EndsMajorBad()
    {
        var holder = new PortRun.LibraryHolder();
        int created = 0;
        var engine = new FakeCurlEngine { CreatingEasy = () => PortRun.CleanUpWhen(holder, ++created == 2), Performing = _ => CurlCode.RemoteFileNotFound };
        holder.Library = CurlGlobal.Init(engine);
        var run = new PortRun(engine, 661, "ftp://x/");

        await Lib661.TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.Contains("curl_easy_init() failed", run.Error.ToString());
    }

    private static FakeMimeBuilder Add(List<FakeMimeBuilder> mimes, FakeMimeBuilder mime)
    {
        mimes.Add(mime);
        return mime;
    }
}
