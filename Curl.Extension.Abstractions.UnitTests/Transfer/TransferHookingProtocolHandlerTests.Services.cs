namespace Curl.Extension.Abstractions.Transfer;

public sealed partial class TransferHookingProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithServices_LendsThemToTheHookWhichAddsHeadersAfterCurlsAndTheHooksOwn()
    {
        TransferHookServices services = UnusedTransferHookServices.Create();
        TokenHook hook = new();
        HeaderRecordingHandler handler = new();

        await new TransferHooks([hook], services).Around(handler).ExecuteAsync(ContextWith(new MemoryStream(), null, null));

        Assert.AreSame(services, hook.Services);
        CollectionAssert.AreEqual(new[] { "X-Curl: own", "X-Hook: own", "Authorization: Bearer fetched" }, handler.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutServices_RunsTheHooksOverloadWithoutThem()
    {
        TokenHook hook = new();
        HeaderRecordingHandler handler = new();

        await new TransferHooks([hook]).Around(handler).ExecuteAsync(ContextWith(new MemoryStream(), null, null));

        Assert.IsNull(hook.Services);
        CollectionAssert.AreEqual(new[] { "X-Curl: own", "X-Hook: own" }, handler.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithServicesAndAHookNotTakingThem_RunsItsOverloadWithoutThem()
    {
        List<string> order = [];

        await TransferHooks.ActivatedBy(ExtensionOptionValues.None, [_ => new OrderingHook("only", order)], UnusedTransferHookServices.Create())
            .Around(new NotModifiedHandler())
            .ExecuteAsync(ContextWith(new MemoryStream(), null, null));

        CollectionAssert.AreEqual(new[] { "only in", "only out" }, order);
    }

    private sealed class TokenHook : ITransferHook
    {
        public TransferHookServices? Services { get; private set; }

        public IEnumerable<string> RequestHeadersFor(ITransferContext context) => ["X-Hook: own"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
        {
            Services = services;
            return next(context.WithRequestHeaders(["Authorization: Bearer fetched"]));
        }
    }

    private sealed class HeaderRecordingHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public IReadOnlyList<string> Headers { get; private set; } = [];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Headers = context.Http!.Headers;
            return ValueTask.FromResult(TransferResult.Success(0));
        }
    }
}
