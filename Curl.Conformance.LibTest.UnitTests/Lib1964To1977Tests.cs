using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1964To1977Tests
{
    private const string ConnectTo = "127.0.0.1:9000:127.0.0.1:8990";

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib1964(), new Lib1965(), new Lib1967(), LibAwsSigV4S3Request.Lib1970, LibAwsSigV4S3Request.Lib1971, new Lib1972(),
            LibAwsSigV4S3Request.Lib1973, LibAwsSigV4S3Request.Lib1974, LibAwsSigV4S3Request.Lib1975, new Lib1977(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib1964", "lib1965", "lib1967", "lib1970", "lib1971", "lib1972", "lib1973", "lib1974", "lib1975", "lib1977" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1964, "http://x/", ConnectTo);

        await Lib1964.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1977.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await LibAwsSigV4S3Request.Lib1970.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1972.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        await Lib1967.TestAsync(run.Context, library);

        StringAssert.Contains(run.Error.ToString(), "curl_easy_init() failed");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1964_SignsWithXxxAndSendsItsDateHeaderThroughTheConnectTo()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.RecvError; } };
        var run = new PortRun(engine, 1964, "http://xxx:yyy@127.0.0.1:9000/1964/testapi/test", ConnectTo);

        LibTestResult result = await new Lib1964().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual("xxx", seen!.Authentication.AwsSigV4);
        Assert.IsNull(seen.Authentication.UserPassword);
        CollectionAssert.AreEqual(new[] { ConnectTo }, seen.Network.ConnectTo.ToArray());
        CollectionAssert.AreEqual(new[] { "Content-Type: application/json", Lib1964.DateHeader }, seen.Http.HttpHeaders.ToArray());
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    public async Task Lib1964_WithoutAConnectTo_SetsNoList()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 1964, "http://x/1964");

        LibTestResult result = await new Lib1964().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, seen!.Network.ConnectTo.Count);
    }

    [TestMethod]
    public async Task Lib1965_WritesEachSchemesVerdict()
    {
        var run = new PortRun(new FakeCurlEngine(), 1965);

        LibTestResult result = await new Lib1965().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        string[] lines = run.OutputText.Split('\n');
        Assert.HasCount(Lib1965.Schemes.Count + 1, lines);
        Assert.AreEqual("bad! ACCEPTED", lines[0]);
        Assert.AreEqual(" ACCEPTED", lines[16]);
    }

    [TestMethod]
    public async Task Lib1965_OnARefusedScheme_WritesRejected()
    {
        var run = new PortRun(new FakeCurlEngine { UrlSetResult = CurlUrlCode.BadScheme }, 1965);

        LibTestResult result = await new Lib1965().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        string[] lines = run.OutputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(Lib1965.Schemes.Count, lines);
        Assert.IsTrue(lines.All(line => line.EndsWith(" REJECTED", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Lib1967_WritesTheUrlTheHandleHolds()
    {
        var run = new PortRun(new FakeCurlEngine(), 1967);

        LibTestResult result = await new Lib1967().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("URL /x\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1967_WhenTheGetFails_WritesNothing()
    {
        var run = new PortRun(new FakeCurlEngine { UrlGetResult = CurlUrlCode.NoHost }, 1967);

        LibTestResult result = await new Lib1967().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    [DataRow(1970, true, 0L, false, null, 1)]
    [DataRow(1971, true, -1L, true, null, 1)]
    [DataRow(1973, false, -1L, false, LibAwsSigV4S3Request.PostFields, 1)]
    [DataRow(1974, false, -1L, false, null, 0)]
    [DataRow(1975, true, -1L, true, null, 2)]
    public async Task LibAwsSigV4S3Request_SetsItsBodyAndSignsForS3(int testNumber, bool upload, long inFileSize, bool reads, string? postFields, int headers)
    {
        LibAwsSigV4S3Request port = PortFor(testNumber);
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, testNumber, "http://127.0.0.1:9000/aws_sigv4/testapi/test", ConnectTo);

        LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(upload, seen!.Transfer.Upload);
        Assert.AreEqual(inFileSize, seen.Transfer.InFileSize);
        Assert.AreEqual(reads, seen.Callbacks.Read is not null);
        Assert.AreEqual(postFields, seen.Http.PostFields is { } fields ? System.Text.Encoding.ASCII.GetString(fields.Span) : null);
        Assert.HasCount(headers, port.Headers);
        CollectionAssert.AreEqual(port.Headers.ToArray(), seen.Http.HttpHeaders.ToArray());
        Assert.AreEqual(LibAwsSigV4S3Request.Provider, seen.Authentication.AwsSigV4);
        Assert.AreEqual(LibAwsSigV4S3Request.Credentials, seen.Authentication.UserPassword);
        CollectionAssert.AreEqual(new[] { ConnectTo }, seen.Network.ConnectTo.ToArray());
        Assert.IsTrue(seen.Behaviour.Verbose);
        Assert.IsFalse(seen.Behaviour.IncludeHeader);
    }

    [TestMethod]
    public async Task LibAwsSigV4S3Request_WithoutAConnectTo_AnswersThePerformsCode()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.GotNothing; } }, 1974, "http://x/1974");

        LibTestResult result = await LibAwsSigV4S3Request.Lib1974.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.GotNothing, result.ExitCode);
        Assert.AreEqual(0, seen!.Network.ConnectTo.Count);
    }

    [TestMethod]
    public void LibAwsSigV4S3Request_ReadNothing_EndsTheUpload() =>
        Assert.AreEqual(0, LibAwsSigV4S3Request.ReadNothing(new byte[4]));

    [TestMethod]
    public async Task Lib1972_PostsItsOnePartMimeSigned()
    {
        var mime = new FakeMimeBuilder();
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { CreatingMime = () => mime, Performing = o => { seen = o; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1972, "http://127.0.0.1:9000/aws_sigv4/testapi/test", ConnectTo);

        LibTestResult result = await new Lib1972().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("foo", mime.Parts.Single().Name);
        CollectionAssert.AreEqual("bar"u8.ToArray(), mime.Parts.Single().Data);
        Assert.IsNotNull(seen!.Http.MimePost);
        Assert.AreEqual(LibAwsSigV4S3Request.Provider, seen.Authentication.AwsSigV4);
        CollectionAssert.AreEqual(new[] { LibAwsSigV4S3Request.JsonContentType }, seen.Http.HttpHeaders.ToArray());
    }

    [TestMethod]
    public async Task Lib1977_WritesEachTransfersEffectiveUrl()
    {
        var handles = new List<object?>();
        var engine = new FakeCurlEngine { Performing = o => { handles.Add(o.Network.UrlHandle); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1977, "http://127.0.0.1:8990/1977");

        LibTestResult result = await new Lib1977().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(3, handles);
        Assert.AreNotSame(handles[0], handles[1]);
        Assert.AreSame(handles[1], handles[2]);
        Assert.AreEqual(3, run.OutputText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Count(line => line.StartsWith("effective URL: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(2, 1)]
    [DataRow(3, 2)]
    public async Task Lib1977_OnAFailingPerform_StopsWithItsCode(int failingPerform, int lines)
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == failingPerform ? CurlCode.CouldntConnect : CurlCode.Ok };
        var run = new PortRun(engine, 1977, "http://127.0.0.1:8990/1977");

        LibTestResult result = await new Lib1977().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(failingPerform, performs);
        Assert.AreEqual(lines, run.OutputText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    private static LibAwsSigV4S3Request PortFor(int testNumber) => testNumber switch
    {
        1970 => LibAwsSigV4S3Request.Lib1970,
        1971 => LibAwsSigV4S3Request.Lib1971,
        1973 => LibAwsSigV4S3Request.Lib1973,
        1974 => LibAwsSigV4S3Request.Lib1974,
        _ => LibAwsSigV4S3Request.Lib1975,
    };
}
