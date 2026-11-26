using Curl.Api.Codes;
using Curl.Api.Errors;

namespace Curl.Api.ResponseBodies;

[TestClass]
public sealed class ResponseBodyStreamTests
{
    private static ResponseBodyStream NewBody(SteppedEasyTransfer easy, int capacity = 64, CancellationToken cancellationToken = default)
    {
        ResponseBodyStream body = new(easy, capacity, cancellationToken);
        easy.Options = easy.Options with { Callbacks = easy.Options.Callbacks with { Write = body.TakeBody } };
        return body;
    }

    private static async Task<byte[]> ReadAllAsync(Stream body, int chunkSize)
    {
        List<byte> all = [];
        byte[] destination = new byte[chunkSize];
        int read;
        while ((read = await body.ReadAsync(destination)) > 0)
        {
            all.AddRange(destination[..read]);
        }

        return [.. all];
    }

    [TestMethod]
    public async Task StartAsync_BodyStarts_ReturnsWhileTheTransferGoesOn()
    {
        SteppedEasyTransfer easy = new();
        await using ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1, 2, 3);

        await body.StartAsync();

        Assert.IsNull(easy.PerformResult);
        Assert.IsFalse(easy.Disposed);
        easy.Finish(CurlCode.Ok);
    }

    [TestMethod]
    public async Task ReadAsync_SmallChunks_YieldTheBodyAsDeliveredThenEnd()
    {
        SteppedEasyTransfer easy = new();
        await using ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1, 2, 3);
        await body.StartAsync();
        easy.Deliver(4, 5);
        easy.Finish(CurlCode.Ok);

        byte[] all = await ReadAllAsync(body, chunkSize: 2);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, all);
    }

    [TestMethod]
    public async Task ReadAsync_ReaderFallsBehind_PausesThenResumesTheTransfer()
    {
        SteppedEasyTransfer easy = new();
        await using ResponseBodyStream body = NewBody(easy, capacity: 4);
        easy.Deliver(1, 2, 3, 4);
        await body.StartAsync();
        easy.Deliver(5, 6, 7);
        easy.Deliver(8);
        easy.Finish(CurlCode.Ok);

        await easy.Paused;
        int resumesWhilePaused = easy.ResumeCount;
        byte[] all = await ReadAllAsync(body, chunkSize: 1);

        Assert.AreEqual(0, resumesWhilePaused);
        Assert.IsGreaterThanOrEqualTo(1, easy.PauseCount);
        Assert.IsGreaterThanOrEqualTo(1, easy.ResumeCount);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, all);
        Assert.AreEqual(CurlCode.Ok, easy.PerformResult);
    }

    [TestMethod]
    public async Task DisposeAsync_BeforeTheEnd_AbortsTheTransferAndDisposesTheHandle()
    {
        SteppedEasyTransfer easy = new();
        ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1);
        await body.StartAsync();

        await body.DisposeAsync();

        Assert.IsTrue(easy.PerformedToken.IsCancellationRequested);
        Assert.AreEqual(CurlCode.AbortedByCallback, easy.PerformResult);
        Assert.IsTrue(easy.Disposed);
        Assert.IsFalse(body.CanRead);
    }

    [TestMethod]
    public async Task DisposeAsync_WhilePaused_AbortsTheTransfer()
    {
        SteppedEasyTransfer easy = new();
        ResponseBodyStream body = NewBody(easy, capacity: 1);
        easy.Deliver(1);
        await body.StartAsync();
        easy.Deliver(2);
        await easy.Paused;

        await body.DisposeAsync();

        Assert.AreEqual(1, easy.ResumeCount);
        Assert.AreEqual(CurlCode.WriteError, easy.PerformResult);
        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task Dispose_ThenDisposeAsync_AbortsOnceAndWaitsForTheHandle()
    {
        SteppedEasyTransfer easy = new();
        ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1);
        await body.StartAsync();

        body.Dispose();
        await body.DisposeAsync();

        Assert.IsTrue(easy.Disposed);
        Assert.AreEqual(CurlCode.AbortedByCallback, easy.PerformResult);
    }

    [TestMethod]
    public async Task ReadAsync_AfterDispose_Throws()
    {
        SteppedEasyTransfer easy = new();
        ResponseBodyStream body = NewBody(easy);
        easy.Finish(CurlCode.Ok);
        await body.StartAsync();
        await body.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => body.ReadAsync(new byte[1]).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_TransferFailsAfterTheBodyStarted_ThrowsCurlException()
    {
        SteppedEasyTransfer easy = new() { ErrorText = "Recv failure: Connection was reset" };
        await using ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1, 2);
        await body.StartAsync();
        easy.Finish(CurlCode.RecvError);
        byte[] destination = new byte[8];

        int read = await body.ReadAsync(destination);
        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(() => body.ReadAsync(destination).AsTask());

        Assert.AreEqual(2, read);
        Assert.AreEqual(CurlCode.RecvError, exception.Code);
        Assert.AreEqual("Recv failure: Connection was reset", exception.Message);
    }

    [TestMethod]
    public async Task StartAsync_TransferFailsAfterDeliveringBody_ReturnsAndReadsThenThrows()
    {
        SteppedEasyTransfer easy = new() { ErrorText = "gRPC status 13" };
        await using ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1, 2, 3);
        easy.Finish(CurlCode.HttpReturnedError);
        byte[] destination = new byte[8];

        await body.StartAsync();
        int read = await body.ReadAsync(destination);
        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(() => body.ReadAsync(destination).AsTask());

        Assert.AreEqual(3, read);
        Assert.AreEqual(CurlCode.HttpReturnedError, exception.Code);
    }

    [TestMethod]
    public async Task StartAsync_TransferFailsWithNoBody_Throws()
    {
        SteppedEasyTransfer easy = new() { ErrorText = "Could not connect" };
        await using ResponseBodyStream body = NewBody(easy);
        easy.Finish(CurlCode.CouldntConnect);

        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(() => body.StartAsync());

        Assert.AreEqual(CurlCode.CouldntConnect, exception.Code);
    }

    [TestMethod]
    public async Task ReadAsync_PendingReadCancelled_ThrowsAndTheTransferGoesOn()
    {
        SteppedEasyTransfer easy = new();
        await using ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1);
        await body.StartAsync();
        byte[] destination = new byte[8];
        Assert.AreEqual(1, await body.ReadAsync(destination));
        using CancellationTokenSource source = new();

        Task<int> pending = body.ReadAsync(destination, 0, 8, source.Token);
        await source.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        easy.Deliver(2);
        easy.Finish(CurlCode.Ok);
        byte[] rest = await ReadAllAsync(body, chunkSize: 8);

        CollectionAssert.AreEqual(new byte[] { 2 }, rest);
        Assert.IsFalse(easy.PerformedToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task StartAsync_TransferFailsBeforeTheBody_ThrowsWithTheHandleDisposed()
    {
        SteppedEasyTransfer easy = new() { ErrorText = "" };
        await using ResponseBodyStream body = NewBody(easy);
        easy.Finish(CurlCode.CouldntResolveHost);

        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(body.StartAsync);

        Assert.AreEqual(CurlCode.CouldntResolveHost, exception.Code);
        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task StartAsync_EmptyBody_ReturnsAnEndedBody()
    {
        SteppedEasyTransfer easy = new();
        await using ResponseBodyStream body = NewBody(easy);
        easy.Finish(CurlCode.Ok);

        await body.StartAsync();

        Assert.IsTrue(easy.Disposed);
        Assert.AreEqual(0, await body.ReadAsync(new byte[4]));
    }

    [TestMethod]
    public async Task StartAsync_PerformThrows_ThrowsWithTheHandleDisposed()
    {
        SteppedEasyTransfer easy = new() { PerformThrows = new InvalidOperationException("engine fault") };
        ResponseBodyStream body = NewBody(easy);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(body.StartAsync);

        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task CancellationToken_CancelledWhileTheBodyStreams_AbortsTheTransfer()
    {
        SteppedEasyTransfer easy = new();
        using CancellationTokenSource source = new();
        await using ResponseBodyStream body = NewBody(easy, cancellationToken: source.Token);
        easy.Deliver(1);
        await body.StartAsync();

        await source.CancelAsync();
        byte[] destination = new byte[8];
        Assert.AreEqual(1, await body.ReadAsync(destination));
        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => body.ReadAsync(destination).AsTask());

        Assert.AreEqual(source.Token, exception.CancellationToken);
        Assert.AreEqual(CurlCode.AbortedByCallback, easy.PerformResult);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadAsync_CancelledMidStreamAndTheUnpauseThrows_ThrowsOnlyToTheReader(bool clientCloses)
    {
        InvalidOperationException fault = new("unpause fault");
        List<Exception> unobserved = [];
        EventHandler<UnobservedTaskExceptionEventArgs> watch = (_, raised) =>
        {
            if (raised.Exception.Flatten().InnerExceptions.Contains(fault))
            {
                unobserved.Add(raised.Exception);
            }
        };
        TaskScheduler.UnobservedTaskException += watch;
        try
        {
            await CancelAPendingReadAsync(new SteppedEasyTransfer { PauseThrows = fault }, clientCloses);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= watch;
        }

        Assert.IsEmpty(unobserved);
    }

    [TestMethod]
    public async Task DisposeAsync_TheUnpauseThrows_DisposesTheHandleWithoutThrowing()
    {
        SteppedEasyTransfer easy = new() { PauseThrows = new InvalidOperationException("unpause fault") };
        ResponseBodyStream body = NewHeldBody(easy, CancellationToken.None, CancellationToken.None);
        easy.Deliver(1);
        await body.StartAsync();

        ValueTask disposed = body.DisposeAsync();
        await easy.PauseThrew;
        easy.Finish(CurlCode.AbortedByCallback);
        await disposed;

        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task ReadAsync_PerformThrowsAfterTheBodyStarted_ThrowsItAfterTheBytes()
    {
        SteppedEasyTransfer easy = new();
        ResponseBodyStream body = NewBody(easy);
        easy.Deliver(1);
        await body.StartAsync();
        byte[] destination = new byte[8];
        Assert.AreEqual(1, await body.ReadAsync(destination));
        Task<int> pending = body.ReadAsync(destination).AsTask();

        easy.Fault(new InvalidOperationException("engine fault"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        await body.DisposeAsync();
        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task ReadAsync_PerformThrowsAfterTheCallerCancelled_ThrowsOperationCanceled()
    {
        SteppedEasyTransfer easy = new();
        using CancellationTokenSource source = new();
        await using ResponseBodyStream body = new(easy, 64, FaultWhenCancelledAsync, source.Token, CancellationToken.None);
        easy.Options = easy.Options with { Callbacks = easy.Options.Callbacks with { Write = body.TakeBody } };
        easy.Deliver(1);
        await body.StartAsync();
        byte[] destination = new byte[8];
        Assert.AreEqual(1, await body.ReadAsync(destination));
        Task<int> pending = body.ReadAsync(destination).AsTask();

        await source.CancelAsync();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.AreEqual(source.Token, exception.CancellationToken);
    }

    /// <summary>A perform that throws once its token is cancelled, as a torn-down transfer may.</summary>
    private static async Task<CurlCode> FaultWhenCancelledAsync(EngineContracts.IEasyTransfer easy, CancellationToken abort)
    {
        await easy.PerformAsync(abort);
        throw new InvalidOperationException("torn down");
    }

    /// <summary>Cancels, mid-stream, the token a pending read waits on, from the caller or with the client closing too.</summary>
    private static async Task CancelAPendingReadAsync(SteppedEasyTransfer easy, bool clientCloses)
    {
        using CancellationTokenSource caller = new();
        using CancellationTokenSource client = new();
        CancellationTokenSource stop = clientCloses ? client : caller;
        await using ResponseBodyStream body = NewHeldBody(easy, caller.Token, client.Token);
        easy.Deliver(1);
        await body.StartAsync();
        byte[] destination = new byte[8];
        Assert.AreEqual(1, await body.ReadAsync(destination));
        Task<int> pending = body.ReadAsync(destination, 0, 8, stop.Token);

        try
        {
            // Throws here, on the canceller's thread, whatever the transfer's callbacks let out; a timer's thread would end the process.
            stop.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            Assert.IsTrue(easy.PauseThrew.IsCompleted);
        }
        finally
        {
            // Ends the held transfer, which disposing the body waits for, even when an assertion failed.
            easy.Finish(CurlCode.AbortedByCallback);
        }
    }

    /// <summary>
    /// A body whose transfer ignores its token and so is still writing when a token fires, until the test
    /// finishes it: the cancellation callback is then sure to run against a running transfer.
    /// </summary>
    private static ResponseBodyStream NewHeldBody(SteppedEasyTransfer easy, CancellationToken caller, CancellationToken client)
    {
        ResponseBodyStream body = new(easy, 64, static (transfer, _) => transfer.PerformAsync(CancellationToken.None).AsTask(), caller, client);
        easy.Options = easy.Options with { Callbacks = easy.Options.Callbacks with { Write = body.TakeBody } };
        return body;
    }

    [TestMethod]
    public async Task Members_DescribeAReadOnlyForwardOnlyStream()
    {
        SteppedEasyTransfer easy = new();
        await using ResponseBodyStream body = NewBody(easy);
        easy.Finish(CurlCode.Ok);
        await body.StartAsync();

        body.Flush();

        Assert.IsTrue(body.CanRead);
        Assert.IsFalse(body.CanSeek);
        Assert.IsFalse(body.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => body.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => body.Write([1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => body.Read(new byte[1], 0, 1));
    }
}
