using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins STD-222's fix (BL-6019, ADR-1434): the bytes and header lines of a proxy's CONNECT replies reach
/// <c>%{size_header}</c> and <c>%{num_headers}</c> for protocols other than HTTP. The expected values are the outside
/// fitness review's, measured on curl 8.22.0 and 8.18.0 through a proxy whose reply is 62 bytes with one header line:
/// <c>124</c> and <c>1</c> for FTP against vsftpd, <c>124</c> and <c>2</c> against pure-ftpd, and <c>62</c> and <c>1</c>
/// for SFTP and DICT.
/// </summary>
[TestClass]
public sealed class Std222Tests
{
    private static readonly ConnectTarget Target = new("h", 21, false);

    private static readonly KeyValuePair<string, string> GreetingLine = new("220-Local time is now 12", "34.");

    [TestMethod]
    public async Task ReportOn_OneTunnelAndNoHeadersOfItsOwn_CountsTheReply()
    {
        TransferReport report = await ReportOfAsync(new TransferReport(), Tunnel(), Tunnel(isReused: true));

        Assert.AreEqual(200, report.ProxyConnectResponseCode);
        Assert.AreEqual(62, report.HeaderSize);
        Assert.AreEqual(1, report.ConnectReplyHeaderCount);
        Assert.IsEmpty(report.PseudoHeaders);
    }

    [TestMethod]
    public async Task ReportOn_TwoTunnelsAndNoHeadersOfItsOwn_CountsTheLastReplyAlone()
    {
        TransferReport report = await ReportOfAsync(new TransferReport(), Tunnel(headers: 3), Tunnel());

        Assert.AreEqual(124, report.HeaderSize);
        Assert.AreEqual(1, report.ConnectReplyHeaderCount);
        Assert.IsEmpty(report.PseudoHeaders);
    }

    [TestMethod]
    public async Task ReportOn_TwoTunnelsAndAGreetingLineWithAColon_CountsTheLineAndTheLastReply()
    {
        TransferReport report = await ReportOfAsync(new TransferReport { PseudoHeaders = [GreetingLine] }, Tunnel(), Tunnel());

        Assert.AreEqual(124, report.HeaderSize);
        Assert.AreEqual(1, report.ConnectReplyHeaderCount);
        CollectionAssert.AreEqual(new[] { GreetingLine }, report.PseudoHeaders.ToArray());
    }

    [TestMethod]
    public async Task ReportOn_OneTunnelThenHeadersOfItsOwn_CountsThoseInPlaceOfTheReply()
    {
        TransferReport report = await ReportOfAsync(
            new TransferReport { HeaderSize = 10, ResponseHeaders = [new("Upgrade", "websocket")] },
            Tunnel());

        Assert.AreEqual(72, report.HeaderSize);
        Assert.AreEqual(0, report.ConnectReplyHeaderCount);
    }

    [TestMethod]
    public async Task ReportOn_AReplyWithNoHeaderLinesAndAGreetingLine_CountsTheLineAlone()
    {
        TransferReport report = await ReportOfAsync(new TransferReport { PseudoHeaders = [GreetingLine] }, Tunnel(headers: 0));

        Assert.AreEqual(0, report.ConnectReplyHeaderCount);
        CollectionAssert.AreEqual(new[] { GreetingLine }, report.PseudoHeaders.ToArray());
    }

    [TestMethod]
    public async Task ReportOn_ARefusedConnect_CountsItsReply()
    {
        ConnectResult refused = ConnectResult.Failed(
            CurlExitCode.CouldntConnect, "refused", null, proxyConnectResponseCode: 403, connectReplyHeadersStored: 2, proxyConnectHeaderBytes: 45);

        TransferReport report = await ReportOfAsync(new TransferReport(), refused);

        Assert.AreEqual(45, report.HeaderSize);
        Assert.AreEqual(2, report.ConnectReplyHeaderCount);
    }

    [TestMethod]
    public async Task Clear_AfterATunnel_ForgetsItsReply()
    {
        ConnectionEndPointRecorder recorder = new();
        await new EndPointRecordingConnector(new ScriptedConnector(Tunnel()), recorder).ConnectAsync(Target, CancellationToken.None);
        recorder.Clear();
        await new EndPointRecordingConnector(new ScriptedConnector(Tunnel()), recorder).ConnectAsync(Target, CancellationToken.None);

        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Assert.AreEqual(62, report.HeaderSize);
    }

    private static ConnectResult Tunnel(int headers = 1, bool isReused = false) =>
        ConnectResult.Connected(
            new SilentConnection(),
            null,
            proxyConnectResponseCode: 200,
            isReused: isReused,
            connectReplyHeadersStored: headers,
            proxyConnectHeaderBytes: 62);

    private static async Task<TransferReport> ReportOfAsync(TransferReport handlerReport, params ConnectResult[] connects)
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ScriptedConnector(connects), recorder);
        foreach (ConnectResult _ in connects)
        {
            await connector.ConnectAsync(Target, CancellationToken.None);
        }

        return recorder.ReportOn(TransferResult.Success(0) with { Report = handlerReport }).Report!;
    }

    /// <summary>Answers each connect with the next of <paramref name="results" />.</summary>
    private sealed class ScriptedConnector(params ConnectResult[] results) : IConnector
    {
        private int connects;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(results[connects++]);
    }

    /// <summary>A connection that carries nothing.</summary>
    private sealed class SilentConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
