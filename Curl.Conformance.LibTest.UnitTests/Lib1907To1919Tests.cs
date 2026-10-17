using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1907To1919Tests
{
    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1907, "http://x/");

        await Lib1916.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib1908.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1910.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1913.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        await Lib1911.TestAsync(run.Context, run.Steps, library);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1907.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1915.TestAsync(run.Context, run.Steps, library, TimeProvider.System, CancellationToken.None);
        await Lib1919.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.AreEqual(3, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports = [new Lib1907(), new Lib1908(), new Lib1910(), new Lib1911(), new Lib1912(), new Lib1913(), new Lib1915(TimeProvider.System), new Lib1916(), new Lib1918(), new Lib1919()];

        CollectionAssert.AreEqual(
            new[] { "lib1907", "lib1908", "lib1910", "lib1911", "lib1912", "lib1913", "lib1915", "lib1916", "lib1918", "lib1919" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task Lib1907_OnAFailingPerform_WritesTheEffectiveUrlAndAnswersTheCode()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect, Info = new TransferInfo { EffectiveUrl = "http://x/1907" } };
        var run = new PortRun(engine, 1907, "x/hello/../1907");

        LibTestResult result = await new Lib1907().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Effective URL: http://x/1907\n", run.OutputText);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1907_OnASucceedingPerform_WritesFailureExpected()
    {
        var run = new PortRun(new FakeCurlEngine(), 1907, "x/1907");

        LibTestResult result = await new Lib1907().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "failure expected, curl_easy_perform returned 0: <");
    }

    [TestMethod]
    public async Task Lib1908_AfterASucceedingPerform_PerformsTheCopyWithTheAltSvcFile()
    {
        var caches = new List<string?>();
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = o => { caches.Add(o.Http.AltSvc); return ++performs == 2 ? CurlCode.RecvError : CurlCode.Ok; } };
        var run = new PortRun(engine, 1908, "http://x/1908", "log/altsvc-1908");

        LibTestResult result = await new Lib1908().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "log/altsvc-1908", "log/altsvc-1908" }, caches);
    }

    [TestMethod]
    public async Task Lib1908_OnAFailingPerform_MakesNoCopy()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.GotNothing; } };
        var run = new PortRun(engine, 1908, "http://x/1908", "altsvc");

        LibTestResult result = await new Lib1908().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.GotNothing, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task Lib1910_FollowsRedirectsWithTheNewlineCredentials()
    {
        string? credentials = null;
        FollowLocation follow = default;
        var engine = new FakeCurlEngine { Performing = o => { credentials = o.Authentication.UserPassword; follow = o.Http.FollowLocation; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1910, "http://x/1910");

        LibTestResult result = await new Lib1910().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Lib1910.UserPassword, credentials);
        Assert.AreNotEqual(default, follow);
    }

    [TestMethod]
    public async Task Lib1911_WritesTheStringLength()
    {
        var run = new PortRun(new FakeCurlEngine(), 1911);

        _ = await new Lib1911().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual($"string length: {Lib1911.MaxInputLength + 1}\n", run.OutputText);
    }

    [TestMethod]
    public void Lib1911_CountUnexpectedAnswers_SetsEachStringOptionButTheExemptOnes()
    {
        var set = new List<CurlOption>();
        CurlEasyOption[] rows =
        [
            new("URL", CurlOption.Url, CurlOptionType.String, CurlEasyOptionFlags.None),
            new("TLSAUTH_TYPE", CurlOption.TlsauthType, CurlOptionType.String, CurlEasyOptionFlags.None),
            new("VERBOSE", CurlOption.Verbose, CurlOptionType.Long, CurlEasyOptionFlags.None),
            new("USERPWD", CurlOption.Userpwd, CurlOptionType.String, CurlEasyOptionFlags.None),
        ];
        var error = new StringWriter();

        int errors = Lib1911.CountUnexpectedAnswers(rows, option => { set.Add(option); return option == CurlOption.Url ? CurlCode.BadFunctionArgument : CurlCode.Ok; }, error);

        Assert.AreEqual(1, errors);
        CollectionAssert.AreEqual(new[] { CurlOption.Url, CurlOption.Userpwd }, set);
        Assert.AreEqual("curl_easy_setopt(USERPWD...) returned 0\n", error.ToString());
    }

    [TestMethod]
    public void Lib1911_IsExempt_NamesTheFourSafeOptions()
    {
        CurlOption[] exempt = [.. CurlOptions.All.Select(row => row.Id).Where(Lib1911.IsExempt).Distinct()];

        CollectionAssert.AreEquivalent(new[] { CurlOption.ProxyTlsauthType, CurlOption.TlsauthType, CurlOption.RandomFile, CurlOption.Egdsocket }, exempt);
    }

    [TestMethod]
    [DataRow(CurlCode.BadFunctionArgument, true)]
    [DataRow(CurlCode.UnknownOption, true)]
    [DataRow(CurlCode.NotBuiltIn, true)]
    [DataRow(CurlCode.UnsupportedProtocol, true)]
    [DataRow(CurlCode.Ok, false)]
    [DataRow(CurlCode.OutOfMemory, false)]
    public void Lib1911_IsExpectedAnswer_AcceptsTheFourRefusals(CurlCode result, bool expected) =>
        Assert.AreEqual(expected, Lib1911.IsExpectedAnswer(result));

    [TestMethod]
    public void Lib1911_ResultFor_IsOkOnlyWithoutUnexpectedAnswers()
    {
        Assert.AreEqual(CurlCode.Ok, Lib1911.ResultFor(0));
        Assert.AreEqual(LibTestErrorCodes.Failure, Lib1911.ResultFor(2));
    }

    [TestMethod]
    public void Lib1911_NullArguments_Throw()
    {
        Assert.AreEqual("rows", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1911.CountUnexpectedAnswers(null!, _ => CurlCode.Ok, TextWriter.Null)).ParamName);
        Assert.AreEqual("setOption", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1911.CountUnexpectedAnswers([], null!, TextWriter.Null)).ParamName);
        Assert.AreEqual("error", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1911.CountUnexpectedAnswers([], _ => CurlCode.Ok, null!)).ParamName);
    }

    [TestMethod]
    public async Task Lib1912_SucceedsAtOnce()
    {
        var run = new PortRun(null, 1912);

        LibTestResult result = await new Lib1912().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Lib1913_AsksForNoBodyAndTheFileTimeOnlyWithASecondArgument(bool secondArgument)
    {
        bool noBody = false;
        bool fileTime = false;
        var engine = new FakeCurlEngine { Performing = o => { noBody = o.Transfer.NoBody; fileTime = o.Transfer.FileTime; return CurlCode.RemoteFileNotFound; } };
        var run = new PortRun(engine, 1913, secondArgument ? ["ftp://x/not-there/1913", "1"] : ["ftp://x/not-there/1913"]);

        LibTestResult result = await new Lib1913().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RemoteFileNotFound, result.ExitCode);
        Assert.IsTrue(noBody);
        Assert.AreEqual(secondArgument, fileTime);
    }

    [TestMethod]
    [DataRow(CurlCode.OperationTimedOut)]
    [DataRow(CurlCode.CouldntConnect)]
    public async Task Lib1915_WritesBothRequestsCodesAndAnswersTheSecond(CurlCode firstResult)
    {
        var readers = new List<HstsReadCallback?>();
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                readers.Add(o.Callbacks.HstsRead);
                return readers.Count == 1 ? firstResult : CurlCode.AbortedByCallback;
            },
        };
        var run = new PortRun(engine, 1915, "http://x:1/not-there/1915");

        LibTestResult result = await new Lib1915(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual("First request returned 7\nSecond request returned 42\n", run.OutputText);
        Assert.AreEqual(2, readers.Count);
        Assert.AreEqual(HstsResult.Fail, readers[1]!(out _));
    }

    [TestMethod]
    public async Task Lib1915_WhenTheSecondHandleCannotBeCreated_StopsAfterTheFirst()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), 1915, "http://x:1/");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib1915.TestAsync(run.Context, run.Steps, holder.Library, TimeProvider.System, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.AreEqual("First request returned 0\n", run.OutputText);
    }

    [TestMethod]
    public void Lib1915_PreloadReader_HandsOverEachHostThenIsDone()
    {
        var error = new StringWriter();
        HstsReadCallback read = Lib1915.PreloadReader(error);
        var names = new List<string>();

        while (read(out HstsEntry? entry) == HstsResult.Ok)
        {
            Assert.IsFalse(entry!.IncludeSubDomains);
            names.Add(entry.Name);
        }

        CollectionAssert.AreEqual(Lib1915.PreloadHosts.Select(host => host.Name).ToArray(), names);
        StringAssert.StartsWith(error.ToString(), "add '1.example.com'\nadd '2.example.com.'\n");
        Assert.AreEqual(HstsResult.Done, read(out HstsEntry? after));
        Assert.IsNull(after);
    }

    [TestMethod]
    public void Lib1915_Writer_WritesIndexTotalNameAndExpiry()
    {
        var output = new StringWriter();
        HstsWriteCallback write = Lib1915.Writer(output);

        Assert.AreEqual(HstsResult.Ok, write(new HstsEntry("1.example.com", false, "25250320 01:02:03"), 0, 4));
        Assert.AreEqual(HstsResult.Ok, write(new HstsEntry("4.example.com", true, null), 3, 4));

        Assert.AreEqual("[0/4] 1.example.com 25250320 01:02:03\n[3/4] .4.example.com unlimited\n", output.ToString());
    }

    [TestMethod]
    public void Lib1915_NullArguments_Throw()
    {
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new Lib1915(null!)).ParamName);
        Assert.AreEqual("error", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1915.PreloadReader(null!)).ParamName);
        Assert.AreEqual("output", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1915.Writer(null!)).ParamName);
    }

    [TestMethod]
    [DataRow(1916, false)]
    [DataRow(1917, true)]
    public async Task Lib1916_PostsEmptyFieldsOrPostAloneAndWritesAFailingResult(int testNumber, bool postAlone)
    {
        ReadOnlyMemory<byte>? fields = null;
        bool post = false;
        var engine = new FakeCurlEngine { Performing = o => { fields = o.Http.PostFields; post = o.Http.Post; return CurlCode.BadFunctionArgument; } };
        var run = new PortRun(engine, testNumber, "mqtt://x/%20");

        LibTestResult result = await new Lib1916().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("result: 43\n", run.OutputText);
        Assert.IsTrue(post);
        Assert.AreEqual(postAlone, fields is null);
    }

    [TestMethod]
    public async Task Lib1916_OnASucceedingPerform_WritesNothing()
    {
        var run = new PortRun(new FakeCurlEngine(), 1916, "mqtt://x/%20");

        LibTestResult result = await new Lib1916().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1918_TheOptionsTable_FindsEveryRowAgain()
    {
        var run = new PortRun(new FakeCurlEngine(), 1918);

        LibTestResult result = await new Lib1918().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void Lib1918_CheckLookups_WritesANameMismatchThenAnIdMismatch()
    {
        var url = new CurlEasyOption("URL", CurlOption.Url, CurlOptionType.String, CurlEasyOptionFlags.None);
        var verbose = new CurlEasyOption("VERBOSE", CurlOption.Verbose, CurlOptionType.Long, CurlEasyOptionFlags.None);
        var userpwd = new CurlEasyOption("USERPWD", CurlOption.Userpwd, CurlOptionType.String, CurlEasyOptionFlags.None);
        var output = new StringWriter();

        Lib1918.CheckLookups(
            [url, verbose, userpwd],
            name => name == "URL" ? verbose : name == "VERBOSE" ? verbose : userpwd,
            id => id == CurlOption.Verbose ? url : userpwd,
            output);

        Assert.AreEqual($"name lookup id {(int)CurlOption.Verbose} does not match {(int)CurlOption.Url}\nID lookup {(int)CurlOption.Verbose} does not match {(int)CurlOption.Verbose}\n", output.ToString());
    }

    [TestMethod]
    public void Lib1918_NullArguments_Throw()
    {
        Assert.AreEqual("rows", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1918.CheckLookups(null!, _ => null, _ => null, TextWriter.Null)).ParamName);
        Assert.AreEqual("byName", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1918.CheckLookups([], null!, _ => null, TextWriter.Null)).ParamName);
        Assert.AreEqual("byId", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1918.CheckLookups([], _ => null, null!, TextWriter.Null)).ParamName);
        Assert.AreEqual("output", Assert.ThrowsExactly<ArgumentNullException>(() => Lib1918.CheckLookups([], _ => null, _ => null, null!)).ParamName);
    }

    [TestMethod]
    public async Task Lib1919_SendsTheBearerTwice()
    {
        var requests = new List<string>();
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                requests.Add($"{o.Authentication.HttpAuthentication} {o.Authentication.XOAuth2Bearer} {o.Authentication.SaslAuthorizationIdentity}");
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1919, "http://x/1919");

        LibTestResult result = await new Lib1919().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        string request = $"{HttpAuthenticationMethods.Bearer} {Lib1919.Bearer} {Lib1919.AuthorizationIdentity}";
        CollectionAssert.AreEqual(new[] { request, request }, requests);
    }

    [TestMethod]
    public async Task Lib1919_OnAFailingFirstPerform_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.LoginDenied; } };
        var run = new PortRun(engine, 1919, "http://x/1919");

        LibTestResult result = await new Lib1919().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(1, performs);
    }
}
