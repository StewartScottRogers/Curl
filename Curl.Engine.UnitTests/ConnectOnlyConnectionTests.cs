using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

[TestClass]
public sealed class ConnectOnlyConnectionTests
{
    private static async Task<(ConnectOnlyConnection Connection, ControlledConnection Peer)> OpenAsync()
    {
        ConnectOnlyConnection connection = new(() => true);
        ControlledConnection peer = new();
        await connection.OpenAsync(peer);
        return (connection, peer);
    }

    [TestMethod]
    public async Task ReceiveAndSend_NoConnectionOpen_AnswerUnsupportedProtocol()
    {
        ConnectOnlyConnection connection = new(() => true);

        CurlConnectOnlyResult receive = await connection.ReceiveAsync(new byte[4], CancellationToken.None);
        CurlConnectOnlyResult send = await connection.SendAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.UnsupportedProtocol, 0), receive);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.UnsupportedProtocol, 0), send);
    }

    [TestMethod]
    public async Task ReceiveAndSend_PerformRunning_AnswerRecursiveApiCall()
    {
        (ConnectOnlyConnection connection, _) = await OpenAsync();
        connection.PerformRunning = true;

        CurlConnectOnlyResult receive = await connection.ReceiveAsync(new byte[4], CancellationToken.None);
        CurlConnectOnlyResult send = await connection.SendAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.RecursiveApiCall, 0), receive);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.RecursiveApiCall, 0), send);
    }

    [TestMethod]
    public async Task ReceiveAndSend_OptionsNoLongerConnectOnly_AnswerUnsupportedProtocol()
    {
        ConnectOnlyConnection connection = new(() => false);
        await connection.OpenAsync(new ControlledConnection());

        CurlConnectOnlyResult receive = await connection.ReceiveAsync(new byte[4], CancellationToken.None);
        CurlConnectOnlyResult send = await connection.SendAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, receive.Result);
        Assert.AreEqual(CurlCode.UnsupportedProtocol, send.Result);
    }

    [TestMethod]
    public async Task Receive_NothingReady_AnswersAgainUntilTheBytesArrive()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        byte[] buffer = new byte[8];

        CurlConnectOnlyResult first = await connection.ReceiveAsync(buffer, CancellationToken.None);
        CurlConnectOnlyResult second = await connection.ReceiveAsync(buffer, CancellationToken.None);
        peer.Deliver("hello"u8.ToArray());
        CurlConnectOnlyResult third = await connection.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Again, 0), first);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Again, 0), second);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 5), third);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(buffer, 0, 5));
    }

    [TestMethod]
    public async Task Receive_BufferSmallerThanTheBytesRead_KeepsTheRestForTheNextReceive()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        await connection.ReceiveAsync(new byte[8], CancellationToken.None);
        peer.Deliver("hello"u8.ToArray());
        byte[] small = new byte[3];

        CurlConnectOnlyResult first = await connection.ReceiveAsync(small, CancellationToken.None);
        string firstText = Encoding.ASCII.GetString(small, 0, first.ByteCount);
        CurlConnectOnlyResult second = await connection.ReceiveAsync(small, CancellationToken.None);

        Assert.AreEqual("hel", firstText);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 2), second);
        Assert.AreEqual("lo", Encoding.ASCII.GetString(small, 0, second.ByteCount));
    }

    [TestMethod]
    public async Task Receive_PeerClosed_AnswersOkWithNoBytes()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        await connection.ReceiveAsync(new byte[8], CancellationToken.None);
        peer.ClosePeer();

        CurlConnectOnlyResult result = await connection.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 0), result);
    }

    [TestMethod]
    public async Task Receive_ReadFailed_AnswersRecvError()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        await connection.ReceiveAsync(new byte[8], CancellationToken.None);
        peer.FailRead(new IOException("reset"));

        CurlConnectOnlyResult result = await connection.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.RecvError, 0), result);
    }

    [TestMethod]
    public async Task Send_ConnectionTakesTheBytes_AnswersThemAllSentAndFlushes()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();

        CurlConnectOnlyResult result = await connection.SendAsync("abc"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 3), result);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(peer.Written));
        Assert.AreEqual(1, peer.FlushCount);
    }

    [TestMethod]
    public async Task Send_WhileTheLastWriteRuns_AnswersAgainUntilItCompletes()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        TaskCompletionSource gate = new();
        peer.WriteGate = gate;

        CurlConnectOnlyResult first = await connection.SendAsync("ab"u8.ToArray(), CancellationToken.None);
        CurlConnectOnlyResult second = await connection.SendAsync("cd"u8.ToArray(), CancellationToken.None);
        peer.WriteGate = null;
        gate.SetResult();
        CurlConnectOnlyResult third = await connection.SendAsync("ef"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 2), first);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Again, 0), second);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 2), third);
        Assert.AreEqual("abef", Encoding.ASCII.GetString(peer.Written));
    }

    [TestMethod]
    public async Task Send_WriteFailsAtOnce_AnswersSendError()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        peer.WriteFailure = new IOException("broken pipe");

        CurlConnectOnlyResult result = await connection.SendAsync("abc"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.SendError, 0), result);
    }

    [TestMethod]
    public async Task Send_TheLastWriteFailedAfterItWasAnswered_AnswersSendErrorOnTheNextSend()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        TaskCompletionSource gate = new();
        peer.WriteGate = gate;
        CurlConnectOnlyResult first = await connection.SendAsync("ab"u8.ToArray(), CancellationToken.None);
        gate.SetException(new IOException("broken pipe"));

        CurlConnectOnlyResult second = await connection.SendAsync("cd"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 2), first);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.SendError, 0), second);
    }

    [TestMethod]
    public async Task ReceiveAndSend_CancelledToken_ThrowOperationCanceled()
    {
        (ConnectOnlyConnection connection, _) = await OpenAsync();
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => connection.ReceiveAsync(new byte[1], source.Token).AsTask());
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => connection.SendAsync(new byte[1], source.Token).AsTask());
    }

    [TestMethod]
    public async Task OpenAsync_AConnectionAlreadyOpen_ClosesItFirst()
    {
        (ConnectOnlyConnection connection, ControlledConnection first) = await OpenAsync();
        ControlledConnection second = new();

        await connection.OpenAsync(second);

        Assert.IsTrue(first.IsDisposed);
        Assert.IsFalse(second.IsDisposed);
    }

    [TestMethod]
    public async Task CloseAsync_ClosesTheConnectionAndLaterCallsAnswerUnsupportedProtocol()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();

        await connection.CloseAsync();
        CurlConnectOnlyResult result = await connection.ReceiveAsync(new byte[1], CancellationToken.None);

        Assert.IsTrue(peer.IsDisposed);
        Assert.AreEqual(CurlCode.UnsupportedProtocol, result.Result);
    }

    [TestMethod]
    [DataRow("https", true)]
    [DataRow("WSS", true)]
    [DataRow("smtps", true)]
    [DataRow("http", false)]
    [DataRow("ftp", false)]
    public void UsesTls_Scheme_IsTrueForATlsScheme(string scheme, bool expected)
    {
        Assert.AreEqual(expected, ConnectOnlyConnection.UsesTls(scheme));
    }

    [TestMethod]
    public async Task Socket_EachOpen_IsANewNumberAboveFirstSocketUntilClosed()
    {
        ConnectOnlyConnection connection = new(() => true);
        CurlSocket beforeOpen = connection.Socket;

        await connection.OpenAsync(new ControlledConnection());
        CurlSocket first = connection.Socket;
        await connection.OpenAsync(new ControlledConnection());
        CurlSocket second = connection.Socket;
        await connection.CloseAsync();

        Assert.AreEqual(CurlSocket.Bad, beforeOpen);
        Assert.IsGreaterThan(ConnectOnlyConnection.FirstSocket, (long)first.Handle);
        Assert.AreNotEqual(first, second);
        Assert.AreEqual(CurlSocket.Bad, connection.Socket);
    }

    [TestMethod]
    public async Task LiveSocket_OptionsNoLongerConnectOnly_IsBad()
    {
        ConnectOnlyConnection connection = new(() => false);
        await connection.OpenAsync(new ControlledConnection());

        CurlSocket live = connection.LiveSocket;

        Assert.AreNotEqual(CurlSocket.Bad, connection.Socket);
        Assert.AreEqual(CurlSocket.Bad, live);
    }

    [TestMethod]
    public async Task ReadyEvents_NoConnectionOpen_IsNone()
    {
        ConnectOnlyConnection connection = new(() => true);

        CurlWaitEvents ready = connection.ReadyEvents(CurlWaitEvents.PollIn | CurlWaitEvents.PollOut);

        Assert.AreEqual(CurlWaitEvents.None, ready);
        await connection.CloseAsync();
    }

    [TestMethod]
    public async Task ReadyEvents_PollOut_IsReadyUnlessASendStillRuns()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();
        CurlWaitEvents idle = connection.ReadyEvents(CurlWaitEvents.PollOut);
        TaskCompletionSource gate = new();
        peer.WriteGate = gate;
        await connection.SendAsync("ab"u8.ToArray(), CancellationToken.None);

        CurlWaitEvents sending = connection.ReadyEvents(CurlWaitEvents.PollOut);
        gate.SetResult();
        CurlWaitEvents sent = connection.ReadyEvents(CurlWaitEvents.PollOut);

        Assert.AreEqual(CurlWaitEvents.PollOut, idle);
        Assert.AreEqual(CurlWaitEvents.None, sending);
        Assert.AreEqual(CurlWaitEvents.PollOut, sent);
    }

    [TestMethod]
    public async Task ReadyEvents_PollIn_StartsAReadAndIsReadyOnceBytesArriveForTheNextReceive()
    {
        (ConnectOnlyConnection connection, ControlledConnection peer) = await OpenAsync();

        CurlWaitEvents nothingYet = connection.ReadyEvents(CurlWaitEvents.PollIn | CurlWaitEvents.PollPriority);
        peer.Deliver("hello"u8.ToArray());
        CurlWaitEvents arrived = connection.ReadyEvents(CurlWaitEvents.PollIn);
        byte[] small = new byte[2];
        CurlConnectOnlyResult received = await connection.ReceiveAsync(small, CancellationToken.None);
        CurlWaitEvents restWaiting = connection.ReadyEvents(CurlWaitEvents.PollIn);

        Assert.AreEqual(CurlWaitEvents.None, nothingYet);
        Assert.AreEqual(CurlWaitEvents.PollIn, arrived);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 2), received);
        Assert.AreEqual("he", Encoding.ASCII.GetString(small));
        Assert.AreEqual(CurlWaitEvents.PollIn, restWaiting);
    }
}
