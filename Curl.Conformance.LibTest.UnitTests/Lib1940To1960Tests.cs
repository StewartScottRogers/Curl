using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.Options;
using Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1940To1960Tests
{
    private static readonly CurlHeader Date = new("Date", "Thu, 09 Nov 2010 14:49:00 GMT", 1, 0, CurlHeaderOrigin.Header);

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib1940(), new Lib1945(), new Lib1947(), new Lib1948(), LibAwsSigV4Headers.Lib1955, LibAwsSigV4Headers.Lib1956,
            LibAwsSigV4Headers.Lib1957, LibAwsSigV4Headers.Lib1958, LibAwsSigV4Headers.Lib1959, new Lib1960(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib1940", "lib1945", "lib1947", "lib1948", "lib1955", "lib1956", "lib1957", "lib1958", "lib1959", "lib1960" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1940, "http://x/", "127.0.0.1", "8990");

        await Lib1940.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1945.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1947.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1948.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await LibAwsSigV4Headers.Lib1955.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1960.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        StringAssert.Contains(run.Error.ToString(), "curl_easy_init() failed");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1940_ThroughAProxy_WritesEachOriginsHeadersAndAnswersOk()
    {
        var headers = new FakeTransferHeaders(
        [
            Date,
            new("Set-Cookie", "a=1", 2, 0, CurlHeaderOrigin.Header),
            new("Set-Cookie", "b=2", 2, 1, CurlHeaderOrigin.Header),
            new("Server", "proxy", 1, 0, CurlHeaderOrigin.Connect),
            new("Location", "/next", 1, 0, CurlHeaderOrigin.Informational),
            new("Test", "trailer", 1, 0, CurlHeaderOrigin.Trailer),
        ]);
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Headers = headers, Performing = o => { seen = o; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1941, "http://hello:8990/1941", "127.0.0.1:8991");

        LibTestResult result = await new Lib1940().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(
            " Date == Thu, 09 Nov 2010 14:49:00 GMT\n- Set-Cookie == a=1 (0/2)\n- Set-Cookie == b=2 (1/2)\n Server == proxy\n Location == /next\n Test == trailer\n",
            run.OutputText);
        Assert.AreEqual("127.0.0.1:8991", seen!.Proxy.Proxy);
        Assert.IsTrue(seen.Proxy.HttpProxyTunnel);
        Assert.AreEqual(Curl.Api.Options.Http.FollowLocation.All, seen.Http.FollowLocation);
        Assert.IsTrue(headers.RequestsAsked.All(request => request == ITransferHeaders.LastRequest));
    }

    [TestMethod]
    public async Task Lib1940_ForCase1946_AsksForTheFirstRequestAndSkipsTheConnectHeaders()
    {
        var headers = new FakeTransferHeaders([Date, new("Server", "proxy", 1, 0, CurlHeaderOrigin.Connect)], [Date]);
        var run = new PortRun(new FakeCurlEngine { Headers = headers }, Lib1940.FirstRequestCase, "http://x/1946");

        LibTestResult result = await new Lib1940().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(" Date == Thu, 09 Nov 2010 14:49:00 GMT\n", run.OutputText);
        Assert.Contains(0, headers.RequestsAsked);
    }

    [TestMethod]
    public async Task Lib1940_OnAFailingPerform_AnswersItsCodeAndWritesNothing()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1940, "http://x/1940", string.Empty);

        LibTestResult result = await new Lib1940().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1940_ShowHeaders_StopsAtTheFirstOfANameThatCannotBeRead()
    {
        var headers = new FakeTransferHeaders([new CurlHeader("fold", "one", 3, 0, CurlHeaderOrigin.Header), new CurlHeader("fold", "two", 3, 1, CurlHeaderOrigin.Header)]);
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Headers = headers });
        await using (library)
        {
            CurlEasy easy = library.EasyInit();
            var output = new StringWriter();

            Lib1940.ShowHeaders(easy, ITransferHeaders.LastRequest, CurlHeaderOrigin.Header, output);

            Assert.AreEqual("- fold == one (0/3)\n- fold == two (1/3)\n", output.ToString());
        }
    }

    [TestMethod]
    public async Task Lib1940_CheckBadArguments_AnswersTheFirstUnexpectedCode()
    {
        Assert.AreEqual(CurlHeaderCode.Missing, await CheckBadArgumentsAsync(new FakeTransferHeaders([]) { Forced = CurlHeaderCode.Missing }, 0));
        Assert.AreEqual(CurlHeaderCode.Missing, await CheckBadArgumentsAsync(new FakeTransferHeaders([]), 0));
        Assert.AreEqual(CurlHeaderCode.NoHeaders, await CheckBadArgumentsAsync(new FakeTransferHeaders([Date]) { OkWithoutHeader = true }, 0));
        Assert.AreEqual(CurlHeaderCode.BadIndex, await CheckBadArgumentsAsync(new FakeTransferHeaders([Date], []), 0));
        Assert.AreEqual(CurlHeaderCode.Ok, await CheckBadArgumentsAsync(new FakeTransferHeaders([Date]), ITransferHeaders.LastRequest));
    }

    [TestMethod]
    public async Task Lib1945_OnAFailingPerform_WritesTheBadnessAndEveryHeader()
    {
        var headers = new FakeTransferHeaders(
            [new CurlHeader("Server", "proxy", 1, 0, CurlHeaderOrigin.Connect), new CurlHeader("Date", "now", 1, 0, CurlHeaderOrigin.Header), new CurlHeader("X", "p", 1, 0, CurlHeaderOrigin.Pseudo)]);
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Headers = headers, Performing = o => { seen = o; return CurlCode.GotNothing; } };
        var run = new PortRun(engine, 1945, "http://hello:8990/1945", "127.0.0.1:8991");

        LibTestResult result = await new Lib1945().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.GotNothing, result.ExitCode);
        Assert.AreEqual("badness: 52\n Server == proxy (0/1)\n Date == now (0/1)\n", run.OutputText);
        Assert.AreEqual("127.0.0.1:8991", seen!.Proxy.Proxy);
        Assert.IsTrue(seen.Proxy.HttpProxyTunnel);
    }

    [TestMethod]
    public async Task Lib1945_WithoutAProxy_WritesTheHeadersAndAnswersOk()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Headers = new FakeTransferHeaders([Date]), Performing = o => { seen = o; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1945, "http://x/1945");

        LibTestResult result = await new Lib1945().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(" Date == Thu, 09 Nov 2010 14:49:00 GMT (0/1)\n", run.OutputText);
        Assert.IsNull(seen!.Proxy.Proxy);
    }

    [TestMethod]
    public async Task Lib1947_CountsTheRequestsOfEachTransfer()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine
        {
            Headers = new FakeTransferHeaders([Date], [new("Status", "200", 1, 0, CurlHeaderOrigin.Pseudo)], [Date]),
            Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; },
        };
        var run = new PortRun(engine, 1947, "http://x/1947", "http://x/19470003");

        LibTestResult result = await new Lib1947().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("count = 3\ncount = 3\n", run.OutputText);
        CollectionAssert.AreEqual(new[] { "http://x/1947", "http://x/19470003" }, urls);
    }

    [TestMethod]
    [DataRow(1, "")]
    [DataRow(2, "count = 0\n")]
    public async Task Lib1947_OnAFailingPerform_WritesItsFailure(int failingPerform, string output)
    {
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Headers = new FakeTransferHeaders(),
            Performing = _ => ++performs == failingPerform ? CurlCode.CouldntConnect : CurlCode.Ok,
        };
        var run = new PortRun(engine, 1947, "http://x/1947", "http://x/19470003");

        LibTestResult result = await new Lib1947().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(output, run.OutputText);
        StringAssert.StartsWith(run.Error.ToString(), "curl_easy_perform() failed: ");
    }

    [TestMethod]
    public async Task Lib1948_PutsThenPostsTheSameBytes()
    {
        var bodies = new List<string>();
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                bodies.Add(o.Http.Post ? Encoding.ASCII.GetString(o.Http.PostFields!.Value.Span) : ReadAll(o.Callbacks.Read!));
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1948, "http://x/1948");

        LibTestResult result = await new Lib1948().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { Lib1948.PutData, Lib1948.PutData }, bodies);
    }

    [TestMethod]
    public async Task Lib1948_OnAFailingPut_DoesNotPost()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.SendError; } }, 1948, "http://x/1948");

        LibTestResult result = await new Lib1948().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public void Lib1948_PutBuffer_HandsOutWhatFitsUntilEmpty()
    {
        var buffer = new Lib1948.PutBuffer("abcde"u8.ToArray());
        var span = new byte[3];

        Assert.AreEqual(3, buffer.Read(span));
        Assert.AreEqual(2, buffer.Read(span));
        Assert.AreEqual(0, buffer.Read(span));
        Assert.AreEqual("dec", Encoding.ASCII.GetString(span));
    }

    [TestMethod]
    [DataRow("lib1955", 7)]
    [DataRow("lib1956", 2)]
    [DataRow("lib1957", 2)]
    [DataRow("lib1958", 2)]
    [DataRow("lib1959", 2)]
    public async Task LibAwsSigV4Headers_SignsItsHeadersThroughTheConnectToEntry(string program, int headerCount)
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.RecvError; } };
        var run = new PortRun(engine, 1955, "http://exam.ple.com:9000/aws_sigv4/testapi/test", "exam.ple.com:9000:127.0.0.1:8990");
        var port = (LibAwsSigV4Headers)LibTestPrograms.Ported.Find(program)!;

        LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual(LibAwsSigV4Headers.ProviderAndCredentials, seen!.Authentication.AwsSigV4);
        Assert.AreEqual(LibAwsSigV4Headers.ProviderAndCredentials, seen.Authentication.UserPassword);
        CollectionAssert.AreEqual(new[] { "exam.ple.com:9000:127.0.0.1:8990" }, seen.Network.ConnectTo.ToArray());
        CollectionAssert.AreEqual(port.Headers.ToArray(), seen.Http.HttpHeaders.ToArray());
        Assert.HasCount(headerCount, port.Headers);
        CollectionAssert.Contains(port.Headers.ToArray(), LibAwsSigV4Headers.JsonContentType);
        Assert.IsTrue(seen.Behaviour.Verbose);
        Assert.IsFalse(seen.Behaviour.IncludeHeader);
    }

    [TestMethod]
    public async Task LibAwsSigV4Headers_WithoutTheConnectToArgument_SetsNoEntry()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 1956, "http://x/1956");

        LibTestResult result = await LibAwsSigV4Headers.Lib1956.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsEmpty(seen!.Network.ConnectTo);
    }

    [TestMethod]
    public async Task Lib1960_AsThePrecheck_AnswersOkAndWritesNothing()
    {
        var run = new PortRun(null, 1960, Lib1960.CheckUrl);

        LibTestResult result = await new Lib1960().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1960_WithoutAPort_AnswersMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 1960, "http://x/file", "127.0.0.1", "65536");

        LibTestResult result = await new Lib1960().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1960_WithoutAnIPv4Address_WritesThePtonFailure()
    {
        var run = new PortRun(new FakeCurlEngine(), 1960, "http://x/file", "localhost", "8990");

        LibTestResult result = await new Lib1960().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("curlx_inet_pton() failed\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1960_HandsOverAnAlreadyConnectedSocket()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 1960, "http://127.0.0.1:8990/file", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1960().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CurlSocket socket = seen!.Callbacks.OpenSocket!(CurlSocketType.IpConnection, new CurlSocketAddress(2, 1, 6, ReadOnlyMemory<byte>.Empty));
        Assert.AreEqual(new CurlSocket(Lib1960.HandedOverSocket), socket);
        Assert.AreEqual(SocketOptionResult.AlreadyConnected, seen.Callbacks.SocketOption!(socket, CurlSocketType.IpConnection));
        Assert.AreEqual(0, seen.Callbacks.CloseSocket!(socket));
        Assert.IsTrue(seen.Behaviour.IncludeHeader);
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    [DataRow("127.0.0.1", true)]
    [DataRow(null, false)]
    [DataRow("127.0.1", false)]
    [DataRow("a.b.c.d", false)]
    [DataRow("::ffff:1.2.3.4", false)]
    public void Lib1960_IsIPv4Address_TakesOnlyADottedQuad(string? text, bool expected) =>
        Assert.AreEqual(expected, Lib1960.IsIPv4Address(text));

    [TestMethod]
    [DataRow("0", true)]
    [DataRow("65535", true)]
    [DataRow("65536", false)]
    [DataRow("-1", false)]
    [DataRow(null, false)]
    public void Lib1960_IsPort_TakesDigitsUpTo65535(string? text, bool expected) =>
        Assert.AreEqual(expected, Lib1960.IsPort(text));

    private static async Task<CurlHeaderCode> CheckBadArgumentsAsync(FakeTransferHeaders headers, int request)
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Headers = headers });
        await using (library)
        {
            return Lib1940.CheckBadArguments(library.EasyInit(), request);
        }
    }

    private static string ReadAll(ReadCallback read)
    {
        var builder = new StringBuilder();
        var buffer = new byte[8];
        long count;
        while ((count = read(buffer)) > 0)
        {
            builder.Append(Encoding.ASCII.GetString(buffer, 0, (int)count));
        }

        return builder.ToString();
    }
}
