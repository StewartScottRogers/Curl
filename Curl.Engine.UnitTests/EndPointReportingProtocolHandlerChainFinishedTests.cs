using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class EndPointReportingProtocolHandlerChainFinishedTests
{
    [TestMethod]
    public async Task TransferChainFinishedAsync_Always_PassesTheEndOnToTheWrappedHandler()
    {
        ChainEndHandler inner = new();
        TransferContext first = new() { Url = CurlUrl.Parse("dict://x/d:a"), Output = Stream.Null };
        TransferResult result = TransferResult.Success(4);

        await new EndPointReportingProtocolHandler(inner, new ConnectionEndPointRecorder()).TransferChainFinishedAsync(first, result);

        Assert.AreSame(first, inner.First);
        Assert.AreSame(result, inner.Result);
    }

    private sealed class ChainEndHandler : IProtocolHandler
    {
        public ITransferContext? First { get; private set; }

        public TransferResult? Result { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes => ["dict"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => ValueTask.FromResult(TransferResult.Success(0));

        public ValueTask TransferChainFinishedAsync(ITransferContext first, TransferResult result)
        {
            First = first;
            Result = result;
            return ValueTask.CompletedTask;
        }
    }
}
