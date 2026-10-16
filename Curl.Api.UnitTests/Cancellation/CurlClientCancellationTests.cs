using Curl.Api.Codes;
using Curl.Api.Errors;

namespace Curl.Api.Cancellation;

/// <summary>
/// Cancelling the token of a <see cref="CurlClient"/> call through an engine that, like libcurl,
/// ignores the token and stops only when the transfer-info callback aborts (ADR-0639).
/// </summary>
[TestClass]
public sealed class CurlClientCancellationTests
{
    private static readonly CurlRequest Get = new(HttpMethod.Get, "http://example.test/");

    private static CurlClient NewClient(CancellableCurlEngine engine) => new(engine, new CurlClientOptions(), ownsEngine: true);

    [TestMethod]
    public async Task SendAsync_CancelledBeforeTheStart_ThrowsWithoutCreatingAHandle()
    {
        CancellableCurlEngine engine = new();
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => client.SendAsync(Get, source.Token));

        Assert.AreEqual(0, engine.CreatedCount);
    }

    [TestMethod]
    public async Task SendAsync_CancelledWhileConnecting_ThrowsOperationCanceledAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Connecting } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();

        Task<CurlResponse> send = client.SendAsync(Get, source.Token);
        await engine.Transfer.Reached;
        await source.CancelAsync();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => send);
        Assert.AreEqual(source.Token, exception.CancellationToken);
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_CancelledWhileUploading_ThrowsOperationCanceledAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Uploading } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();
        using MemoryStream upload = new([1, 2, 3, 4]);
        CurlRequest post = new(HttpMethod.Post, "http://example.test/") { Body = CurlContent.FromStream(upload) };

        Task<CurlResponse> send = client.SendAsync(post, source.Token);
        await engine.Transfer.Reached;
        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => send);
        Assert.AreEqual(4, upload.Position);
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_CancelledWhileDownloading_TheBodyThrowsOperationCanceledAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Downloading } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();
        CurlResponse response = await client.SendAsync(Get, source.Token);
        await engine.Transfer.Reached;
        byte[] destination = new byte[8];
        Assert.AreEqual(1, await response.Body.ReadAsync(destination));

        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => response.Body.ReadAsync(destination).AsTask());
        await response.Body.DisposeAsync();
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_CancelledWhilePaused_UnpausesAbortsAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Paused } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();
        CurlResponse response = await client.SendWithBodyCapacityAsync(Get, bodyCapacity: 1, source.Token);
        await engine.Transfer.Reached;
        Assert.AreEqual(0, engine.Transfer.ProgressCalls);

        await source.CancelAsync();
        await response.Body.DisposeAsync();

        // The client's multi handle driver also removes the cancelled transfer (ADR-0644), and may do so
        // before the unpaused transfer asks its transfer-info callback, so ask the callback directly.
        Assert.AreEqual(1, engine.Transfer.ResumeCalls);
        Assert.AreEqual(TransferCancellation.Abort, engine.Transfer.Options.Callbacks.TransferInfo!(0, 0, 0, 0));
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_CancelledWhilePaused_TheBodyGivesWhatWasBufferedThenThrowsOperationCanceled()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Paused } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();
        CurlResponse response = await client.SendWithBodyCapacityAsync(Get, bodyCapacity: 1, source.Token);
        await using Stream body = response.Body;
        await engine.Transfer.Reached;

        await source.CancelAsync();

        byte[] destination = new byte[8];
        Assert.AreEqual(1, await response.Body.ReadAsync(destination));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => response.Body.ReadAsync(destination).AsTask());
    }

    [TestMethod]
    public async Task SendAsync_TimedOut_ThrowsCurlExceptionAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { ImmediateResult = CurlCode.OperationTimedOut } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();

        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(() => client.SendAsync(Get, source.Token));

        Assert.AreEqual(CurlCode.OperationTimedOut, exception.Code);
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_CancelledBeforeTheStart_ThrowsWithoutCreatingAHandle()
    {
        CancellableCurlEngine engine = new();
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => client.ConnectWebSocketAsync("ws://example.test/", source.Token));

        Assert.AreEqual(0, engine.CreatedCount);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_CancelledWhileConnecting_ThrowsOperationCanceledAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Connecting } };
        await using CurlClient client = NewClient(engine);
        using CancellationTokenSource source = new();

        Task<CurlWebSocket> connect = client.ConnectWebSocketAsync("ws://example.test/", source.Token);
        await engine.Transfer.Reached;
        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => connect);
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_TimedOut_ThrowsCurlExceptionAndFreesTheHandle()
    {
        CancellableCurlEngine engine = new() { Transfer = { ImmediateResult = CurlCode.OperationTimedOut } };
        await using CurlClient client = NewClient(engine);

        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(() => client.ConnectWebSocketAsync("ws://example.test/"));

        Assert.AreEqual(CurlCode.OperationTimedOut, exception.Code);
        Assert.IsTrue(engine.Transfer.Disposed);
    }
}
