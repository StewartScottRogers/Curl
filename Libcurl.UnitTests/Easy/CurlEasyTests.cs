using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.Options;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Mime;
using Libcurl.Options;
using Libcurl.UnitTests.Global;
using Libcurl.UnitTests.Mime.Mime;

namespace Libcurl.UnitTests.Easy;

[TestClass]
public sealed class CurlEasyTests
{
    [TestMethod]
    [DataRow("http://127.0.0.1/522", 8990L, "http://127.0.0.1:8990/522", DisplayName = "URL with no port")]
    [DataRow("http://www.example.com:999/523", 19999L, "http://www.example.com:19999/523", DisplayName = "URL with its own port")]
    public async Task Perform_PortOptionOnTheManagedEngine_TransfersTheUrlWithThatPort(string url, long port, string expected)
    {
        await using var library = CurlGlobal.Init(CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, url);
        easy.SetOption(CurlOption.Port, port);
        // An unusable proxy ends the perform before anything is dialled, with the URL it would have transferred.
        easy.SetOption(CurlOption.Proxy, "foo://proxy:1");

        easy.Perform();

        Assert.AreEqual(CurlCode.Ok, easy.GetInfo(CurlInfo.EffectiveUrl, out var effective));
        Assert.AreEqual(expected, effective);
    }

    private static readonly EasyOptionDispatch VerboseOnly = new(new Dictionary<CurlOption, EasyOptionSetter>
    {
        [CurlOption.Verbose] = (ref EasyOptions options, object? value) =>
        {
            options = options with { Behaviour = options.Behaviour with { Verbose = Convert.ToInt64(value) != 0 } };
            return CurlCode.Ok;
        },
    });

    private static EasyOptions WithErrorBuffer(StringBuilder buffer) =>
        new() { Behaviour = new() { ErrorBuffer = buffer } };

    [TestMethod]
    public void Perform_ReturnsTheEnginesCodeAndPassesTheHandlesOptions()
    {
        var transfer = new FakeEasyTransfer { Result = CurlCode.CouldntConnect };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, 1L));
        var code = easy.Perform();

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.IsTrue(transfer.PerformedWith!.Behaviour.Verbose);
        Assert.AreSame(transfer.PerformedWith, easy.Options);
        Assert.AreEqual(CancellationToken.None, transfer.PerformedToken);
    }

    [TestMethod]
    public async Task PerformAsync_ReturnsTheEnginesCodeAndPassesTheToken()
    {
        var transfer = new FakeEasyTransfer { Result = CurlCode.RecvError };
        await using var easy = new CurlEasy(transfer, VerboseOnly);
        using var source = new CancellationTokenSource();

        var code = await easy.PerformAsync(source.Token);

        Assert.AreEqual(CurlCode.RecvError, code);
        Assert.AreEqual(source.Token, transfer.PerformedToken);
        Assert.AreSame(transfer.Options, transfer.PerformedWith);
    }

    [TestMethod]
    public void Perform_AFailure_FillsTheErrorBufferWithTheEnginesText()
    {
        var buffer = new StringBuilder("stale");
        var transfer = new FakeEasyTransfer { Result = CurlCode.CouldntConnect, ErrorText = "Failed to connect", Options = WithErrorBuffer(buffer) };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        var code = easy.Perform();

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual("Failed to connect", buffer.ToString());
        Assert.AreEqual("Failed to connect", easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_ALongFailureText_CutsItTo255BytesLeavingRoomForTheTerminator()
    {
        var buffer = new StringBuilder();
        var transfer = new FakeEasyTransfer { Result = CurlCode.ReadError, ErrorText = new string('a', 254) + "é" + "tail", Options = WithErrorBuffer(buffer) };
        await using var easy = new CurlEasy(transfer, VerboseOnly);

        await easy.PerformAsync();

        Assert.AreEqual(new string('a', 254), buffer.ToString());
        Assert.AreEqual(new string('a', 254), easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_AFailureTextOf255Bytes_KeepsItWhole()
    {
        var buffer = new StringBuilder();
        var text = new string('b', 253) + "é";
        var transfer = new FakeEasyTransfer { Result = CurlCode.ReadError, ErrorText = text, Options = WithErrorBuffer(buffer) };
        await using var easy = new CurlEasy(transfer, VerboseOnly);

        await easy.PerformAsync();

        Assert.AreEqual(text, buffer.ToString());
        Assert.AreEqual(EasyErrorBuffer.MessageByteLimit, Encoding.UTF8.GetByteCount(buffer.ToString()));
    }

    [TestMethod]
    public void Perform_AFailureWithNoEngineText_LeavesTheErrorBufferEmptyNotTheStrerrorText()
    {
        // libcurl 8.22.0 writes the buffer only through failf (lib/curl_trc.c); nothing copies
        // curl_easy_strerror's text in when no failf ran, so the cleared buffer stays empty.
        var buffer = new StringBuilder("an earlier failure");
        var transfer = new FakeEasyTransfer { Result = CurlCode.CouldntConnect, ErrorText = string.Empty, Options = WithErrorBuffer(buffer) };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.CouldntConnect, easy.Perform());

        Assert.AreEqual(string.Empty, buffer.ToString());
        Assert.AreEqual(string.Empty, easy.ErrorText);
    }

    [TestMethod]
    public void Perform_ASuccess_ClearsTheErrorBuffer()
    {
        var buffer = new StringBuilder("an earlier failure");
        var transfer = new FakeEasyTransfer { ErrorText = "detail the engine left", Options = WithErrorBuffer(buffer) };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.Ok, easy.Perform());

        Assert.AreEqual(string.Empty, buffer.ToString());
    }

    [TestMethod]
    public void Perform_NoErrorBufferSet_StillAnswersTheErrorText()
    {
        var transfer = new FakeEasyTransfer { Result = CurlCode.CouldntResolveHost, ErrorText = "Could not resolve host: nowhere" };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.CouldntResolveHost, easy.Perform());

        Assert.IsNull(transfer.Options.Behaviour.ErrorBuffer);
        Assert.AreEqual("Could not resolve host: nowhere", easy.ErrorText);
    }

    [TestMethod]
    public void SetOption_AFailingCode_LeavesTheOptionsUnchanged()
    {
        var transfer = new FakeEasyTransfer();
        var before = transfer.Options;
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.NotBuiltIn, easy.SetOption(CurlOption.Url, "http://example.com/"));
        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.SetOption(CurlOption.Verbose, "1"));

        Assert.AreSame(before, transfer.Options);
    }

    [TestMethod]
    public void Dispose_DisposesTheTransferOnce()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);

        easy.Dispose();
        easy.Dispose();

        Assert.AreEqual(1, transfer.DisposeCount);
        Assert.IsTrue(easy.IsCleanedUp);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheTransferOnce()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        Assert.IsFalse(easy.IsCleanedUp);

        await easy.DisposeAsync();
        await easy.DisposeAsync();

        Assert.AreEqual(1, transfer.DisposeCount);
    }

    [TestMethod]
    public void Members_AfterCleanup_ThrowObjectDisposed()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.SetOption(CurlOption.Verbose, 1L));
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Perform());
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.PerformAsync());
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Options);
        Assert.IsNull(transfer.PerformedWith);
    }

    [TestMethod]
    public void Reset_AfterSetOption_RestoresTheDefaultOptionsAndKeepsTheConnection()
    {
        var transfer = new FakeEasyTransfer { OpenConnection = "connection #0" };
        using var easy = new CurlEasy(transfer, VerboseOnly);
        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, 1L));

        easy.Reset();

        Assert.AreEqual(1, transfer.ResetCount);
        Assert.AreEqual(new EasyOptions(), easy.Options);
        Assert.IsFalse(easy.Options.Behaviour.Verbose);
        Assert.AreEqual("connection #0", transfer.OpenConnection);
    }

    [TestMethod]
    public void DupHandle_CarriesTheOptionsButNotTheConnection()
    {
        var transfer = new FakeEasyTransfer { OpenConnection = "connection #0" };
        using var easy = new CurlEasy(transfer, VerboseOnly);
        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, 1L));

        using var duplicate = easy.DupHandle();

        Assert.AreNotSame(easy, duplicate);
        Assert.IsTrue(duplicate.Options.Behaviour.Verbose);
        Assert.AreEqual(easy.Options, duplicate.Options);
        Assert.AreNotSame(transfer, duplicate.Transfer);
        Assert.IsNull(((FakeEasyTransfer)duplicate.Transfer).OpenConnection);
        Assert.IsFalse(duplicate.IsCleanedUp);
    }

    [TestMethod]
    public void DupHandle_ThenSetOptionOnTheDuplicate_LeavesTheOriginalUnchanged()
    {
        using var easy = new CurlEasy(new FakeEasyTransfer(), VerboseOnly);
        using var duplicate = easy.DupHandle();

        Assert.AreEqual(CurlCode.Ok, duplicate.SetOption(CurlOption.Verbose, 1L));

        Assert.IsTrue(duplicate.Options.Behaviour.Verbose);
        Assert.IsFalse(easy.Options.Behaviour.Verbose);
    }

    [TestMethod]
    public void DupHandle_CleanupOfTheDuplicate_LeavesTheOriginalLive()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, VerboseOnly);
        var duplicate = easy.DupHandle();

        duplicate.Dispose();

        Assert.IsTrue(duplicate.IsCleanedUp);
        Assert.IsFalse(easy.IsCleanedUp);
        Assert.AreEqual(0, transfer.DisposeCount);
    }

    [TestMethod]
    public void ResetAndDupHandle_AfterCleanup_ThrowObjectDisposed()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(easy.Reset);
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.DupHandle());
        Assert.AreEqual(0, transfer.ResetCount);
    }

    [TestMethod]
    [DataRow(CurlPause.Continue, CurlPause.Continue)]
    [DataRow(CurlPause.Receive, CurlPause.Receive)]
    [DataRow(CurlPause.Send, CurlPause.Send)]
    [DataRow(CurlPause.All, CurlPause.All)]
    [DataRow((CurlPause)(1 << 1), CurlPause.Continue)]
    [DataRow((CurlPause)(-1), CurlPause.All)]
    [DataRow((CurlPause)(1 | 1 << 3), CurlPause.Receive)]
    public void Pause_EachCombination_PassesOnlyTheReceiveAndSendBits(CurlPause given, CurlPause expected)
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, VerboseOnly);

        var code = easy.Pause(given);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, transfer.PausedWith);
        Assert.AreEqual(expected, transfer.PausedWith[0]);
    }

    [TestMethod]
    [DataRow(CurlPause.Continue)]
    [DataRow(CurlPause.Receive)]
    [DataRow(CurlPause.Send)]
    [DataRow(CurlPause.All)]
    public void Pause_EngineRefuses_ReturnsTheEnginesCode(CurlPause given)
    {
        var transfer = new FakeEasyTransfer { PauseAndUpkeepResult = CurlCode.BadFunctionArgument };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        var code = easy.Pause(given);

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
        Assert.AreEqual(given, transfer.PausedWith[0]);
    }

    [TestMethod]
    public void Upkeep_ReturnsTheEnginesCodeWithNoToken()
    {
        var transfer = new FakeEasyTransfer { PauseAndUpkeepResult = CurlCode.SendError };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        var code = easy.Upkeep();

        Assert.AreEqual(CurlCode.SendError, code);
        Assert.HasCount(1, transfer.UpkeptWith);
        Assert.AreEqual(CancellationToken.None, transfer.UpkeptWith[0]);
    }

    [TestMethod]
    public async Task UpkeepAsync_ReturnsTheEnginesCodeAndPassesTheToken()
    {
        var transfer = new FakeEasyTransfer();
        await using var easy = new CurlEasy(transfer, VerboseOnly);
        using var source = new CancellationTokenSource();

        var code = await easy.UpkeepAsync(source.Token);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(source.Token, transfer.UpkeptWith[0]);
    }

    [TestMethod]
    public void Recv_DataReady_WritesItAndAnswersOkWithTheCount()
    {
        var connection = new FakeConnectOnlyConnection { Incoming = "hi"u8.ToArray(), ReceiveResult = new(CurlCode.Ok, 2) };
        using var easy = new CurlEasy(new FakeEasyTransfer { Connection = connection }, VerboseOnly);
        var buffer = new byte[8];

        var code = easy.Recv(buffer, out var received);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(2, received);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(buffer, 0, received));
        Assert.AreEqual(CancellationToken.None, connection.LastToken);
    }

    [TestMethod]
    public void Recv_NothingReady_AnswersAgainWithNoBytes()
    {
        var connection = new FakeConnectOnlyConnection { ReceiveResult = new(CurlCode.Again, 5) };
        using var easy = new CurlEasy(new FakeEasyTransfer { Connection = connection }, VerboseOnly);

        var code = easy.Recv(new byte[8], out var received);

        Assert.AreEqual(CurlCode.Again, code);
        Assert.AreEqual(0, received);
    }

    [TestMethod]
    public void RecvAndSend_NoConnectOnlyConnection_AnswerTheEnginesUnsupportedProtocol()
    {
        using var easy = new CurlEasy(new FakeEasyTransfer(), VerboseOnly);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, easy.Recv(new byte[8], out var received));
        Assert.AreEqual(CurlCode.UnsupportedProtocol, easy.Send("x"u8.ToArray(), out var sent));
        Assert.AreEqual(0, received);
        Assert.AreEqual(0, sent);
    }

    [TestMethod]
    public void Send_ConnectionTakesTheBytes_AnswersOkWithTheCount()
    {
        var connection = new FakeConnectOnlyConnection { SendResult = new(CurlCode.Ok, 3) };
        using var easy = new CurlEasy(new FakeEasyTransfer { Connection = connection }, VerboseOnly);

        var code = easy.Send("abc"u8.ToArray(), out var sent);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(3, sent);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(connection.Sent.Single()));
    }

    [TestMethod]
    public void Send_NothingCouldBeSent_AnswersAgainWithNoBytes()
    {
        var connection = new FakeConnectOnlyConnection { SendResult = new(CurlCode.Again, 3) };
        using var easy = new CurlEasy(new FakeEasyTransfer { Connection = connection }, VerboseOnly);

        var code = easy.Send("abc"u8.ToArray(), out var sent);

        Assert.AreEqual(CurlCode.Again, code);
        Assert.AreEqual(0, sent);
    }

    [TestMethod]
    public async Task RecvAsyncAndSendAsync_PassTheTokenAndAnswerTheEnginesResult()
    {
        var connection = new FakeConnectOnlyConnection { ReceiveResult = new(CurlCode.Again, 0), SendResult = new(CurlCode.SendError, 0) };
        await using var easy = new CurlEasy(new FakeEasyTransfer { Connection = connection }, VerboseOnly);
        using var source = new CancellationTokenSource();

        var receive = await easy.RecvAsync(new byte[4], source.Token);
        Assert.AreEqual(source.Token, connection.LastToken);
        var send = await easy.SendAsync("x"u8.ToArray(), source.Token);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Again, 0), receive);
        Assert.AreEqual(CurlCode.SendError, send.Result);
        Assert.AreEqual(source.Token, connection.LastToken);
    }

    [TestMethod]
    public async Task RecvAndSend_AfterCleanup_ThrowObjectDisposed()
    {
        var easy = new CurlEasy(new FakeEasyTransfer(), VerboseOnly);
        await easy.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Recv(new byte[1], out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Send(new byte[1], out _));
    }

    [TestMethod]
    public async Task PauseAndUpkeep_AfterCleanup_ThrowObjectDisposed()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Pause(CurlPause.All));
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Upkeep());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => easy.UpkeepAsync().AsTask());
        Assert.IsEmpty(transfer.PausedWith);
        Assert.IsEmpty(transfer.UpkeptWith);
    }

    [TestMethod]
    public async Task EasyInit_WrapsTheEnginesNewTransfer()
    {
        var engine = new FakeCurlEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        var easy = library.EasyInit();

        Assert.HasCount(1, engine.Created);
        Assert.IsFalse(easy.IsCleanedUp);
    }

    [TestMethod]
    public void Perform_MimePartChangedAfterTheSetopt_PostsThePartAsItIsNow()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        using var mime = new CurlMime(new FakeMimeBuilder());
        var part = mime.AddPart();
        part.SetName("short value");
        easy.SetOption(CurlOption.Mimepost, mime);

        part.SetName("long value for length change");
        easy.Perform();

        Assert.AreEqual("long value for length change", transfer.PerformedWith!.Http.MimePost!.Parts.Single().Name);
    }

    [TestMethod]
    public void Perform_MimeFreedAfterTheSetopt_PostsAnEmptyFormInstead()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        var mime = new CurlMime(new FakeMimeBuilder());
        mime.AddPart().SetName("greeting");
        easy.SetOption(CurlOption.Mimepost, mime);

        mime.Dispose();
        easy.Perform();

        Assert.IsNull(transfer.PerformedWith!.Http.MimePost);
        Assert.IsEmpty(transfer.PerformedWith.Http.HttpPost!.Parts);
    }

    [TestMethod]
    public void DupHandle_MimeFreedAfterTheDuplicate_DuplicatePostsItsCopy()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        var mime = new CurlMime(new FakeMimeBuilder());
        var part = mime.AddPart();
        part.SetName("before");
        easy.SetOption(CurlOption.Mimepost, mime);
        part.SetName("at duplicate");

        using var duplicate = easy.DupHandle();
        mime.Dispose();

        Assert.AreEqual("at duplicate", duplicate.Options.Http.MimePost!.Parts.Single().Name);
    }

    [TestMethod]
    [DataRow(CurlOption.Mimepost, DisplayName = "CURLOPT_MIMEPOST NULL")]
    [DataRow(CurlOption.Httppost, DisplayName = "CURLOPT_HTTPPOST NULL")]
    public void Perform_MimeUnboundByALaterPostOption_LeavesTheOptionsAsSet(CurlOption option)
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        using var mime = new CurlMime(new FakeMimeBuilder());
        easy.SetOption(CurlOption.Mimepost, mime);
        easy.SetOption(option, null);
        var set = easy.Options;

        mime.AddPart().SetName("later");
        easy.Perform();

        Assert.AreSame(set, transfer.PerformedWith);
    }

    [TestMethod]
    public void Reset_AfterAMimePost_PerformsWithTheDefaultOptions()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        using var mime = new CurlMime(new FakeMimeBuilder());
        easy.SetOption(CurlOption.Mimepost, mime);

        easy.Reset();
        easy.Perform();

        Assert.IsNull(transfer.PerformedWith!.Http.MimePost);
    }
}
