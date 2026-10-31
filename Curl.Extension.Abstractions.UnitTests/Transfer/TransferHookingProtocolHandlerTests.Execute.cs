using System.Text;

namespace Curl.Extension.Abstractions.Transfer;


public sealed partial class TransferHookingProtocolHandlerTests
{
    private const string OkHeaders = "HTTP/1.1 200 OK\r\n\r\n";
    private const string NotModifiedHeaders = "HTTP/1.1 304 Not Modified\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_WithAHookAnsweringItself_WritesItsResponseAndResultWithoutRunningTheHandler()
    {
        MemoryStream output = new();
        MemoryStream headerOutput = new();
        MemoryStream dumpHeaderOutput = new();
        TransferResult cached = TransferResult.Success(6) with { Report = new TransferReport { ResponseCode = 200 } };
        FinishedHook reader = new();

        TransferResult result = await new TransferHooks([new AnsweringHook(cached), reader]).Around(new FailingHandler())
            .ExecuteAsync(ContextWith(output, headerOutput, dumpHeaderOutput));

        Assert.AreSame(cached, result);
        Assert.AreSame(cached, reader.Finished);
        Assert.AreEqual("cached", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(OkHeaders, Encoding.ASCII.GetString(headerOutput.ToArray()));
        Assert.AreEqual(OkHeaders, Encoding.ASCII.GetString(dumpHeaderOutput.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAHookReplacingTheResponse_CapturesTheHandlersOutputAndWritesItsOwn()
    {
        MemoryStream output = new();
        MemoryStream headerOutput = new();
        MemoryStream dumpHeaderOutput = new();
        ReplacingHook replacing = new();
        FinishedHook reader = new();

        TransferResult result = await new TransferHooks([replacing, reader]).Around(new NotModifiedHandler())
            .ExecuteAsync(ContextWith(output, headerOutput, dumpHeaderOutput));

        Assert.AreEqual($"not modified|{NotModifiedHeaders}|{NotModifiedHeaders}", replacing.Captured);
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreSame(result, reader.Finished);
        Assert.AreEqual("stored", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(OkHeaders, Encoding.ASCII.GetString(headerOutput.ToArray()));
        Assert.AreEqual(OkHeaders, Encoding.ASCII.GetString(dumpHeaderOutput.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithTwoHooks_RunsTheFirstOutermostAndTheBodyThroughEveryHooksStream()
    {
        List<string> order = [];
        MemoryStream output = new();

        await new TransferHooks([new OrderingHook("first", order), new OrderingHook("second", order)]).Around(new NotModifiedHandler())
            .ExecuteAsync(ContextWith(output, null, null));

        CollectionAssert.AreEqual(new[] { "first in", "second in", "second out", "first out" }, order);
        Assert.AreEqual("NOT MODIFIED", Encoding.ASCII.GetString(output.ToArray()));
    }

    private static TransferContext ContextWith(Stream output, Stream? headerOutput, Stream? dumpHeaderOutput) => new()
    {
        Url = CurlUrl.Parse("http://example.com/page"),
        Output = output,
        HeaderOutput = headerOutput,
        DumpHeaderOutput = dumpHeaderOutput,
        Http = new HttpRequestOptions { Headers = ["X-Curl: own"] },
    };

    private static async Task WriteAsync(Stream? stream, string text)
    {
        if (stream is not null)
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(text));
        }
    }

    private sealed class FailingHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => throw new AssertFailedException("Curl's handler ran.");
    }

    private sealed class NotModifiedHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            await WriteAsync(context.HeaderOutput, NotModifiedHeaders);
            await WriteAsync(context.DumpHeaderOutput, NotModifiedHeaders);
            await WriteAsync(context.Output, "not modified");
            return TransferResult.Success(12) with { Report = new TransferReport { ResponseCode = 304 } };
        }
    }

    private sealed class AnsweringHook(TransferResult cached) : ITransferHook
    {
        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
        {
            await WriteAsync(context.HeaderOutput, OkHeaders);
            await WriteAsync(context.DumpHeaderOutput, OkHeaders);
            await WriteAsync(context.Output, "cached");
            return cached;
        }
    }

    private sealed class ReplacingHook : ITransferHook
    {
        public string? Captured { get; private set; }

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
        {
            MemoryStream body = new();
            MemoryStream headers = new();
            MemoryStream dumpedHeaders = new();
            await next(context.WithOutputs(body, headers, dumpedHeaders));
            Captured = string.Join('|', new[] { body, headers, dumpedHeaders }.Select(stream => Encoding.ASCII.GetString(stream.ToArray())));
            await WriteAsync(context.HeaderOutput, OkHeaders);
            await WriteAsync(context.DumpHeaderOutput, OkHeaders);
            await WriteAsync(context.Output, "stored");
            return TransferResult.Success(6) with { Report = new TransferReport { ResponseCode = 200 } };
        }
    }

    private sealed class FinishedHook : ITransferHook
    {
        public TransferResult? Finished { get; private set; }

        public void TransferFinished(TransferResult result) => Finished = result;
    }

    private sealed class OrderingHook(string name, List<string> order) : ITransferHook
    {
        public Stream ResponseBodyOver(Stream output) => new UpperCasingStream(output);

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
        {
            order.Add($"{name} in");
            TransferResult result = await next(context);
            order.Add($"{name} out");
            return result;
        }
    }

    private sealed class UpperCasingStream(Stream next) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => next.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            next.Write(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(buffer, offset, count).ToUpperInvariant()));
    }
}
