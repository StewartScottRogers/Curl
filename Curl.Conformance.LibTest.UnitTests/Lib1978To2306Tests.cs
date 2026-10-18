using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1978To2306Tests
{
    private const string ConnectTo = "127.0.0.1:9000:127.0.0.1:8990";

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib1978(), new Lib1985(), new Lib2023(), new Lib2032(TimeProvider.System), new Lib2082(), new Lib2118(),
            new Lib2301(), new Lib2302(), new Lib2304(TimeProvider.System), new Lib2306(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib1978", "lib1985", "lib2023", "lib2032", "lib2082", "lib2118", "lib2301", "lib2302", "lib2304", "lib2306" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public void Constructors_WithoutATimeProvider_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2032(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2304(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2032Connections(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2302Frames(null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2302Frames(TextWriter.Null, null!));
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1978, "http://x/");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib2306.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib2032.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1978.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2118.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2023.TestAsync(run.Context, run.Steps, library, HttpAuthenticationMethods.Basic, HttpAuthenticationMethods.Basic, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib1985.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2301.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2302.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2304.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib2082.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);

        StringAssert.Contains(run.Error.ToString(), "curl_easy_init() failed");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1978_SignsTheUploadWithItsHeadersThenUploadsUnsigned()
    {
        var seen = new List<EasyOptions>();
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen.Add(o); return CurlCode.Ok; } }, 1978, "http://127.0.0.1:9000/aws_sigv4/testapi/test", ConnectTo);

        LibTestResult result = await new Lib1978().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(2, seen);
        Assert.AreEqual("aws:amz:us-east-1:s3", seen[0].Authentication.AwsSigV4);
        Assert.IsTrue(seen[0].Transfer.Upload);
        Assert.AreEqual(0, seen[0].Transfer.InFileSize);
        CollectionAssert.AreEqual(Lib1978.Headers.ToArray(), seen[0].Http.HttpHeaders.ToArray());
        CollectionAssert.AreEqual(new[] { ConnectTo }, seen[0].Network.ConnectTo.ToArray());
        Assert.IsNull(seen[1].Authentication.AwsSigV4);
    }

    [TestMethod]
    public async Task Lib1978_WhenTheSignedUploadFails_StopsWithoutAConnectTo()
    {
        var seen = new List<EasyOptions>();
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen.Add(o); return CurlCode.RecvError; } }, 1978, "http://x/1978");

        LibTestResult result = await new Lib1978().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.HasCount(1, seen);
        Assert.IsEmpty(seen[0].Network.ConnectTo);
    }

    [TestMethod]
    public async Task Lib1985_WritesBothStepCodesAndAnswersTheSecond()
    {
        var seen = new List<EasyOptions>();
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = o => { seen.Add(o); return ++performs == 1 ? CurlCode.LoginDenied : CurlCode.Ok; } };
        var run = new PortRun(engine, 1985, "http://firsthost:8990/1985", "http://secondhost:8990/19850001", "8990", "127.0.0.1");

        LibTestResult result = await new Lib1985().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual($"STEP1_CODE={(int)CurlCode.LoginDenied}\nSTEP2_CODE=0\n", run.OutputText);
        CollectionAssert.AreEqual(new[] { "firsthost:8990:127.0.0.1", "secondhost:8990:127.0.0.1" }, seen[0].Network.Resolve.ToArray());
        Assert.AreEqual(Lib1985.UserPassword, seen[0].Authentication.UserPassword);
        Assert.AreEqual("unused-token", seen[0].Authentication.XOAuth2Bearer);
        Assert.AreEqual(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Bearer, seen[0].Authentication.HttpAuthentication);
        Assert.AreEqual(HttpAuthenticationMethods.Digest, seen[1].Authentication.HttpAuthentication);
        Assert.AreEqual("http://secondhost:8990/19850001", seen[1].Network.Url);
        Assert.AreEqual(4, seen[0].Callbacks.Write!("body"u8));
    }

    [TestMethod]
    [DataRow("basic", HttpAuthenticationMethods.Basic)]
    [DataRow("Digest", HttpAuthenticationMethods.Digest)]
    [DataRow("NTLM", HttpAuthenticationMethods.Ntlm)]
    [DataRow("bearer", HttpAuthenticationMethods.None)]
    [DataRow(null, HttpAuthenticationMethods.None)]
    public void Lib2023_ParseAuthName_KnowsBasicDigestAndNtlmInAnyCase(string? name, HttpAuthenticationMethods expected) =>
        Assert.AreEqual(expected, Lib2023.ParseAuthName(name));

    [TestMethod]
    [DataRow("basic", null)]
    [DataRow("kerberos", "basic")]
    public async Task Lib2023_WithoutBothSchemes_EndsMajorBadBeforeTheInit(string main, string? fallback)
    {
        string[] arguments = fallback is null ? ["http://x/2023", main] : ["http://x/2023", main, fallback];
        var run = new PortRun(null, 2023, arguments);

        LibTestResult result = await new Lib2023().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("auth schemes not found on commandline\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2023_SendsTheFiveRequestsOnTwoHandles()
    {
        var seen = new List<EasyOptions>();
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen.Add(o); return CurlCode.Ok; } }, 2023, "http://x/2023", "basic", "digest");

        LibTestResult result = await new Lib2023().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "http://x/20230100", "http://x/20230200", "http://x/20230300", "http://x/20230400", "http://x/20230500" },
            seen.Select(o => o.Network.Url).ToArray());
        CollectionAssert.AreEqual(
            new[] { Lib2023.WrongPassword, Lib2023.RightPassword, Lib2023.WrongPassword, Lib2023.WrongPassword, Lib2023.RightPassword },
            seen.Select(o => o.Authentication.UserPassword).ToArray());
        CollectionAssert.AreEqual(
            new[] { HttpAuthenticationMethods.Basic, HttpAuthenticationMethods.Digest, HttpAuthenticationMethods.Basic, HttpAuthenticationMethods.Digest, HttpAuthenticationMethods.Digest },
            seen.Select(o => o.Authentication.HttpAuthentication).ToArray());
        StringAssert.StartsWith(run.Error.ToString(), "Sending new request 100 to http://x/20230100 with credential testuser:wrongpass (auth 1)\n");
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(3, 3)]
    public async Task Lib2023_WhenARequestFails_StopsThere(int failingRequest, int expectedRequests)
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => ++performs == failingRequest ? CurlCode.LoginDenied : CurlCode.Ok }, 2023, "http://x/2023", "ntlm", "basic");

        LibTestResult result = await new Lib2023().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(expectedRequests, performs);
    }

    [TestMethod]
    public async Task Lib2023_WhenTheSecondHandleCannotBeMade_EndsMajorBad()
    {
        var holder = new PortRun.LibraryHolder();
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { PortRun.CleanUpWhen(holder, ++performs == 2); return CurlCode.Ok; } }, 2023, "http://x/2023", "basic", "basic");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib2023.TestAsync(run.Context, run.Steps, holder.Library, HttpAuthenticationMethods.Basic, HttpAuthenticationMethods.Basic, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(2, performs);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:8990/2082", false, PrerequestResult.Ok, false)]
    [DataRow("http://[::1]:8990/2082#ipv6", true, PrerequestResult.Ok, false)]
    [DataRow("http://127.0.0.1:8990/2082#err#redir", false, PrerequestResult.Abort, true)]
    public async Task Lib2082_SetsItsPrerequestCallbackFromTheUrl(string url, bool ipv6, PrerequestResult answer, bool follows)
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 2082, url);

        LibTestResult result = await new Lib2082().RunAsync(run.Context, CancellationToken.None);
        PrerequestResult returned = seen!.Callbacks.Prerequest!("10.0.0.1", "10.0.0.2", 80, 5000);
        long written = seen.Callbacks.Write!("body"u8);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(answer, returned);
        Assert.AreEqual(follows ? FollowLocation.All : FollowLocation.None, seen.Http.FollowLocation);
        Assert.AreEqual(4, written);
        Assert.AreEqual("body", run.Error.ToString());
        string connected = ipv6 ? "Connected to [10.0.0.1]\nConnected from [10.0.0.2]\n" : "Connected to 10.0.0.1\nConnected from 10.0.0.2\n";
        Assert.AreEqual($"{connected}Remote port = 80\nLocal port = 5000\nReturning = {(int)answer}\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib2082_WhenTheTransferFails_WritesItsFailure()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.AbortedByCallback }, 2082, "http://x/2082#err");

        LibTestResult result = await new Lib2082().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.AbortedByCallback, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), $"lib2082.c:{Lib2082.PerformFailureLine} curl_easy_perform() failed with code 42 (");
    }

    [TestMethod]
    public async Task Lib2082_WithoutAUrl_TransfersNothingNamed()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 2082);

        LibTestResult result = await new Lib2082().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, seen!.Network.Url);
    }

    [TestMethod]
    public async Task Lib2118_VerifiesTheHostButNotThePeerOverIpv4()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.PeerFailedVerification; } }, 2118, "https://localhost:8990/2118");

        LibTestResult result = await new Lib2118().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.PeerFailedVerification, result.ExitCode);
        Assert.IsFalse(seen!.Tls.Origin.VerifyPeer);
        Assert.IsTrue(seen.Tls.Origin.VerifyHost);
        Assert.AreEqual(IpResolve.V4, seen.Network.IpResolve);
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    public async Task Lib2301_AnswersAPingChunkWithAPong()
    {
        var socket = new FakeEasyWebSocket();
        var engine = new FakeCurlEngine
        {
            WebSocket = socket,
            Performing = o =>
            {
                _ = o.Callbacks.Write!(new byte[] { 0x89, 0x00 });
                _ = o.Callbacks.Write!(new byte[] { 0x81, 0x01, 0x41 });
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 2301, "ws://x/2301");

        LibTestResult result = await new Lib2301().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(
            "Called CURLOPT_WRITEFUNCTION with 2 bytes: 89 00 \nsend back a simple PONG\nCalled CURLOPT_WRITEFUNCTION with 3 bytes: 81 01 41 \ncurl_easy_perform() returned 0\n",
            run.Error.ToString());
        Assert.HasCount(1, socket.Sent);
        Assert.AreEqual(CurlWebSocketFlags.None, socket.Sent[0].Flags);
    }

    [TestMethod]
    public void Lib2301_Write_WhenThePongCannotBeSent_FailsTheChunk()
    {
        var error = new StringWriter();

        long written = Lib2301.Write(error, null, new byte[] { 0x89 });
        long empty = Lib2301.Write(error, null, ReadOnlySpan<byte>.Empty);

        Assert.AreEqual(0, written);
        Assert.AreEqual(0, empty);
        Assert.AreEqual(
            "Called CURLOPT_WRITEFUNCTION with 1 bytes: 89 \nsend back a simple PONG\nreturns error from callback\nCalled CURLOPT_WRITEFUNCTION with 0 bytes: \n",
            error.ToString());
    }

    [TestMethod]
    public async Task Lib2302_GathersChunksByFrameFlagsAndFlushesAfterCleanup()
    {
        var socket = new FakeEasyWebSocket();
        var engine = new FakeCurlEngine
        {
            WebSocket = socket,
            Performing = o =>
            {
                socket.CurrentFrame = new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 2);
                _ = o.Callbacks.Write!(new byte[] { 0x41 });
                _ = o.Callbacks.Write!(new byte[] { 0x42 });
                socket.CurrentFrame = new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 0, 1);
                _ = o.Callbacks.Write!(new byte[] { 0x43 });
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 2302, "ws://x/2302");

        LibTestResult result = await new Lib2302().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("41 42 \nRECFLAGS: 1\n43 \nRECFLAGS: 2\n", run.OutputText);
        Assert.AreEqual("curl_easy_perform() returned 0\n", run.Error.ToString());
    }

    [TestMethod]
    public void Lib2302_Write_WhenTheChunkDoesNotFit_SaysSoAndStillTakesIt()
    {
        var error = new StringWriter();
        var frames = new Lib2302Frames(TextWriter.Null, error);
        _ = frames.Add(new byte[Lib2302Frames.BufferSize], null);

        long written = Lib2302.Write(error, frames, null, new byte[] { 1 });

        Assert.AreEqual(1, written);
        Assert.AreEqual("returns error from callback\n", error.ToString());
    }

    [TestMethod]
    public void Lib2302Frames_FlushesWhenTheFramePresenceOrFlagsChange()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var frames = new Lib2302Frames(output, error);
        var text = new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 1);

        frames.Flush();
        _ = frames.Add(new byte[] { 0x01 }, null);
        _ = frames.Add(new byte[] { 0x02 }, null);
        _ = frames.Add(new byte[] { 0x03 }, text);
        _ = frames.Add(new byte[] { 0x04 }, text with { Offset = 1 });
        _ = frames.Add(new byte[] { 0x05 }, text with { Flags = CurlWebSocketFlags.Ping });
        frames.Flush();
        frames.Flush();

        Assert.AreEqual("01 02 \n03 04 \nRECFLAGS: 1\n05 \nRECFLAGS: 10\n", output.ToString());
        Assert.AreEqual("RECFLAGS: NULL\n", error.ToString());
    }

    [TestMethod]
    public void Lib2302Frames_Add_PastTheBuffer_HoldsNothing()
    {
        var frames = new Lib2302Frames(TextWriter.Null, TextWriter.Null);

        long first = frames.Add(new byte[Lib2302Frames.BufferSize], null);
        long second = frames.Add(new byte[] { 1 }, null);

        Assert.AreEqual(Lib2302Frames.BufferSize, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public async Task Lib2304_WhenTheConnectFails_MakesNoRounds()
    {
        var socket = new FakeEasyWebSocket();
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket, Performing = o => { seen = o; return CurlCode.CouldntConnect; } }, 2304, "ws://x/2304");

        LibTestResult result = await new Lib2304(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("curl_easy_perform() returned 7\n", run.Error.ToString());
        Assert.AreEqual("websocket/2304", seen!.Http.UserAgent);
        Assert.IsEmpty(socket.Sent);
    }

    [TestMethod]
    public async Task Lib2304_WithEveryPongBack_MakesElevenRoundsThenCloses()
    {
        var socket = new FakeEasyWebSocket();
        socket.Receives.Enqueue((CurlCode.Ok, "hi", CurlWebSocketFlags.Text));
        for (int round = 0; round < Lib2304.Rounds; round++)
        {
            if (round > 0)
            {
                socket.Receives.Enqueue((CurlCode.Again, string.Empty, CurlWebSocketFlags.None));
            }

            socket.Receives.Enqueue((CurlCode.Ok, Lib2304.Payload, CurlWebSocketFlags.Pong));
        }

        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket }, 2304, "ws://x/2304");

        LibTestResult result = await RunAdvancingAsync(new Lib2304(time).RunAsync(run.Context, CancellationToken.None), time);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(Lib2304.Rounds + 1, socket.Sent);
        Assert.AreEqual((CurlWebSocketFlags.Ping, Lib2304.Payload), socket.Sent[0]);
        Assert.AreEqual((CurlWebSocketFlags.Close, string.Empty), socket.Sent[^1]);
        StringAssert.Contains(run.Error.ToString(), "recv_any: got 2 bytes rflags 1\nSend ping\nws: curl_ws_send returned 0, sent 6\nReceive pong\nws: got PONG back\nws: got the same payload back\n");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    [DataRow(CurlCode.Ok, "foobaz", CurlWebSocketFlags.Pong, "ws: did NOT get the same payload back\n")]
    [DataRow(CurlCode.Ok, "foobar", CurlWebSocketFlags.Text, "recv_pong: wrong frame, got 6 bytes rflags 1\n")]
    [DataRow(CurlCode.RecvError, "", CurlWebSocketFlags.None, "ws: curl_ws_recv returned 56, received 0\n")]
    public async Task Lib2304_WithoutTheRightPong_WritesConnectionClosed(CurlCode code, string text, CurlWebSocketFlags flags, string line)
    {
        var socket = new FakeEasyWebSocket();
        socket.Receives.Enqueue((CurlCode.Again, string.Empty, CurlWebSocketFlags.None));
        socket.Receives.Enqueue((code, text, flags));
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket }, 2304, "ws://x/2304");

        LibTestResult result = await new Lib2304(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Connection closed\n", run.OutputText);
        StringAssert.EndsWith(run.Error.ToString(), line);
        Assert.HasCount(1, socket.Sent);
    }

    [TestMethod]
    public async Task Lib2304_WhenThePingCannotBeSent_StopsWithoutAClose()
    {
        var socket = new FakeEasyWebSocket();
        socket.SendResults.Enqueue(CurlCode.SendError);
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket }, 2304, "ws://x/2304");

        LibTestResult result = await new Lib2304(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(1, socket.Sent);
        StringAssert.EndsWith(run.Error.ToString(), "ws: curl_ws_send returned 55, sent 0\n");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib2306_TransfersTheSecondUrlAfterTheFirst()
    {
        var urls = new List<string?>();
        var run = new PortRun(new FakeCurlEngine { Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; } }, 2306, "http://x/2306", "http://x/23060002");

        LibTestResult result = await new Lib2306().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "http://x/2306", "http://x/23060002" }, urls);
    }

    [TestMethod]
    public async Task Lib2306_WhenTheFirstFails_StopsThere()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.WeirdServerReply; } }, 2306, "http://x/2306", "http://x/23060002");

        LibTestResult result = await new Lib2306().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task Lib2032_StartsThreeHandlesOneAfterAnotherAndWritesTheirCounts()
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, 2032, "http://x/2032");

        LibTestResult result = await new Lib2032(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Data connection 0: 0\nData connection 1: 0\nData connection 2: 0\n", run.OutputText);
        string error = run.Error.ToString();
        StringAssert.Contains(error, "lib2032.c:181 new state 2\n");
        StringAssert.EndsWith(error, "lib2032.c:189 num_handles 3 timeout -1 running 0\n");
    }

    [TestMethod]
    public async Task Lib2032_WhileANewHandleHoldsASocket_GoesRoundAgain()
    {
        var engine = new FakeCurlEngine();
        int performs = 0;
        engine.Multi.WaitSockets = [new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, 0)];
        engine.Multi.Performing = () => engine.Multi.WaitSockets = ++performs < 2 ? engine.Multi.WaitSockets : [];
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, long.MaxValue);
        var run = new PortRun(engine, 2032, "http://x/2032");

        LibTestResult result = await new Lib2032(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "Warning: socket did not open immediately for new handle (trying again)\n");
        StringAssert.EndsWith(run.Error.ToString(), "running 0 state 2\n");
    }

    [TestMethod]
    public async Task Lib2032_WhileTransfersRunAfterTheLastStart_GoesOnUntilNoneRuns()
    {
        var engine = new FakeCurlEngine();
        for (int perform = 0; perform < 3; perform++)
        {
            engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 0));
        }

        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, 0);
        var run = new PortRun(engine, 2032, "http://x/2032");

        LibTestResult result = await new Lib2032(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "lib2032.c:156 running 1 state 2\nlib2032.c:189 num_handles 3 timeout 0 running 1\n");
        StringAssert.EndsWith(run.Error.ToString(), "lib2032.c:156 running 0 state 2\n");
    }

    [TestMethod]
    public async Task Lib2032_WhenAMultiStepFails_StopsAndStillWritesTheCounts()
    {
        foreach (Action<FakeMultiTransfer> fault in new Action<FakeMultiTransfer>[]
        {
            multi => multi.PerformResult = (CurlMultiCode.BadHandle, 0),
            multi => multi.WaitSocketsResult = CurlMultiCode.BadHandle,
            multi => multi.TimeoutResult = (CurlMultiCode.BadHandle, 0),
            multi => multi.WaitResult = (CurlMultiCode.BadHandle, 0),
            multi => multi.AddResult = CurlMultiCode.BadEasyHandle,
        })
        {
            var engine = new FakeCurlEngine();
            fault(engine.Multi);
            var run = new PortRun(engine, 2032, "http://x/2032");

            LibTestResult result = await new Lib2032(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.EndsWith(run.OutputText, "Data connection 2: 0\n");
        }
    }

    [TestMethod]
    public async Task Lib2032_WhenTheTestRunsTooLong_Aborts()
    {
        foreach (bool inWait in new[] { false, true })
        {
            var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
            var engine = new FakeCurlEngine();
            Action advance = () => time.Advance(TimeSpan.FromMinutes(5));
            if (inWait)
            {
                engine.Multi.Waiting = advance;
            }
            else
            {
                engine.Multi.Performing = advance;
            }

            var run = new PortRun(engine, 2032, "http://x/2032");

            LibTestResult result = await new Lib2032(time).RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual((int)LibTestErrorCodes.RunsForever, result.ExitCode);
        }
    }

    [TestMethod]
    public async Task Lib2032_WhenTheLibraryGoesAtTheFirstHandle_StopsAtTheNextInit()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(new FakeCurlEngine(), 2032, "http://x/2032");
        var engine = (FakeCurlEngine)run.Context.Engine!;
        holder.Library = CurlGlobal.Init(engine);
        engine.Multi.Performing = () => PortRun.CleanUpWhen(holder, !holder.Library.IsCleanedUp);

        var clock = new LibTestClock(TimeProvider.System);
        clock.StartTestTiming();

        await Lib2032.TestAsync(run.Context, run.Steps, holder.Library, clock, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        StringAssert.EndsWith(run.OutputText, "Data connection 2: 0\n");
    }

    [TestMethod]
    public async Task Lib2032Connections_Write_TracksEachHandlesSocket()
    {
        long socket = Lib2032Connections.NoSocket;
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Informing = () => new TransferInfo { LastSocket = socket } });
        await using CurlEasy easy = library.EasyInit()!;
        var error = new StringWriter();
        var connections = new Lib2032Connections(error);

        long none = connections.Write(0, easy, "ab"u8);
        socket = 7;
        long first = connections.Write(0, easy, "cde"u8);
        long same = connections.Write(0, easy, "f"u8);
        socket = 8;
        long moved = connections.Write(0, easy, "g"u8);
        long movedEmpty = connections.Write(0, easy, ReadOnlySpan<byte>.Empty);

        CollectionAssert.AreEqual(new long[] { 2, 3, 1, 0, 1 }, new[] { none, first, same, moved, movedEmpty });
        Assert.AreEqual(7, connections.Received[0]);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, connections.CallbackResult);
        Assert.AreEqual("Handle 0 started on socket 7 and moved to 8\nHandle 0 started on socket 7 and moved to 8\n", error.ToString());
    }

    private static async Task<LibTestResult> RunAdvancingAsync(Task<LibTestResult> run, ManualTimeProvider time)
    {
        for (int turn = 0; turn < 1000 && !run.IsCompleted; turn++)
        {
            time.Advance(TimeSpan.FromSeconds(2));
            await Task.Yield();
        }

        return await run;
    }
}
