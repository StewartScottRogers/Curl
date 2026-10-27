using System.Collections.Concurrent;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-147: <c>curl_easy_perform</c> of a handle that is in a multi handle answers <c>CURLE_FAILED_INIT</c> with the
/// error text "easy handle already used in multi handle" and sends nothing, as libcurl 8.22.0 does; once the handle
/// is removed from the multi handle it performs again.
/// </summary>
[TestClass]
public sealed class Cgr147Tests
{
    [TestMethod]
    public async Task PerformAsync_OfAHandleLeftInAMultiHandleAfterItsTransfer_AnswersFailedInitAndSendsNothing()
    {
        RecordingProtocolHandler handler = new();
        await using ManagedCurlEngine engine = EngineOver(handler);
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new() { Network = new EasyNetworkOptions { Url = "http://h/" } };
        multi.Add(easy);
        await DriveUntilDoneAsync(multi);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.FailedInit, code);
        Assert.AreEqual("easy handle already used in multi handle", easy.ErrorText);
        CollectionAssert.AreEqual(new[] { "/" }, handler.Paths.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_OfAHandleRemovedFromItsMultiHandle_RunsTheTransfer()
    {
        RecordingProtocolHandler handler = new();
        await using ManagedCurlEngine engine = EngineOver(handler);
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new() { Network = new EasyNetworkOptions { Url = "http://h/" } };
        multi.Add(easy);
        await DriveUntilDoneAsync(multi);
        multi.Remove(easy);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "/", "/" }, handler.Paths.ToArray());
    }

    // Performs and waits until the multi handle reports its transfer done.
    private static async Task DriveUntilDoneAsync(IMultiTransfer multi)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            await multi.PerformAsync(CancellationToken.None);
            if (multi.TryReadInfo(out _, out _))
            {
                return;
            }

            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }

        Assert.Fail("The multi handle never reported its transfer done.");
    }

    private static ManagedCurlEngine EngineOver(RecordingProtocolHandler handler) =>
        new(TimeProvider.System, (_, _, _) => new ProtocolDispatcher([handler]), readEnvironmentVariable: _ => null);

    // Records each request's path and answers it with a 5-byte body.
    private sealed class RecordingProtocolHandler : IProtocolHandler
    {
        public ConcurrentQueue<string> Paths { get; } = new();

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Paths.Enqueue(context.Url.AbsolutePath);
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes("hello"), context.CancellationToken);
            return TransferResult.Success(5);
        }
    }
}
