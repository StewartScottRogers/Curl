using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-033: the callbacks of a transfer a multi handle drives run on the thread calling
/// <c>curl_multi_perform</c>, inside the call, as libcurl's single-thread callback contract says.
/// </summary>
[TestClass]
public sealed class Cgr033Tests
{
    private const int BodyLength = 4 * 1024 * 1024;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform()
    {
        string directory = Directory.CreateTempSubdirectory("cgr033-").FullName;
        try
        {
            string path = Path.Combine(directory, "body.bin");
            await File.WriteAllBytesAsync(path, new byte[BodyLength], TestContext.CancellationTokenSource.Token);
            List<string> outside = [];
            long written = 0;
            int performingThread = 0;
            bool insidePerform = false;
            await using ManagedCurlEngine engine = new();
            await using IMultiTransfer multi = engine.CreateMulti();
            await using IEasyTransfer transfer = engine.CreateEasy();
            transfer.Options = new EasyOptions
            {
                Network = new() { Url = new Uri(path).AbsoluteUri },
                Callbacks = new()
                {
                    Write = data =>
                    {
                        if (!insidePerform || Environment.CurrentManagedThreadId != performingThread)
                        {
                            outside.Add($"write on thread {Environment.CurrentManagedThreadId}, inside perform {insidePerform}");
                        }

                        written += data.Length;
                        return data.Length;
                    },
                },
            };
            multi.Add(transfer);

            CurlMultiMessage? message = await DriveUntilDoneAsync(multi, thread => (performingThread, insidePerform) = (thread, true), () => insidePerform = false);

            Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, transfer, CurlCode.Ok), message);
            Assert.AreEqual(BodyLength, written);
            Assert.IsEmpty(outside, string.Join(Environment.NewLine, outside));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout()
    {
        string directory = Directory.CreateTempSubdirectory("cgr033-").FullName;
        try
        {
            string path = Path.Combine(directory, "body.bin");
            await File.WriteAllBytesAsync(path, new byte[BodyLength], TestContext.CancellationTokenSource.Token);
            await using ManagedCurlEngine engine = new();
            await using IMultiTransfer multi = engine.CreateMulti();
            await using IEasyTransfer transfer = engine.CreateEasy();
            transfer.Options = new EasyOptions { Network = new() { Url = new Uri(path).AbsoluteUri }, Callbacks = new() { Write = data => data.Length } };
            multi.Add(transfer);
            DateTime start = DateTime.UtcNow;

            CurlMultiMessage? message = await DriveUntilDoneAsync(multi, _ => { }, () => { });

            Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, transfer, CurlCode.Ok), message);
            Assert.IsLessThan(TimeSpan.FromSeconds(30), DateTime.UtcNow - start);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady()
    {
        // A file:// transfer may read its whole file inside the first perform where the platform's file reads
        // complete synchronously (Linux), leaving no callback to queue; this handler writes only once released,
        // off the performing thread, so its write always waits for a perform (BL-3869).
        WritingWhenReleasedProtocolHandler handler = new();
        await using ManagedCurlEngine engine = new(TimeProvider.System, (_, _, _) => new ProtocolDispatcher([handler]));
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer transfer = engine.CreateEasy();
        transfer.Options = new EasyOptions { Network = new() { Url = "http://h/x" }, Callbacks = new() { Write = data => data.Length } };
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);
        handler.Release();
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);

        // The transfer cannot finish while its write waits for a perform, so once the timeout is 0 a callback is queued.
        while (multi.GetTimeout().TimeoutMilliseconds != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(1, TestContext.CancellationTokenSource.Token);
        }

        (CurlMultiCode code, int ready) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(1, ready);
    }

    // The probe's loop: perform, then wait, until the transfer's message comes; each wait is long, so a
    // loop that only moved on at the wait's timeout would not finish within the test's limit.
    private static async Task<CurlMultiMessage?> DriveUntilDoneAsync(IMultiTransfer multi, Action<int> entering, Action leaving)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            entering(Environment.CurrentManagedThreadId);
            await multi.PerformAsync(CancellationToken.None);
            leaving();
            if (multi.TryReadInfo(out CurlMultiMessage? message, out _))
            {
                return message;
            }

            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(1), CancellationToken.None);
        }

        return null;
    }

    // Writes 10 bytes once the test releases it, on a thread-pool thread rather than the performing one.
    private sealed class WritingWhenReleasedProtocolHandler : IProtocolHandler
    {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public void Release() => released.TrySetResult();

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            await released.Task.WaitAsync(context.CancellationToken);
            await context.Output.WriteAsync(new byte[10], context.CancellationToken);
            return TransferResult.Success(10);
        }
    }
}
