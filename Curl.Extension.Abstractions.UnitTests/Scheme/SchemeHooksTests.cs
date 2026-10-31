using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Abstractions.Scheme;

[TestClass]
public sealed class SchemeHooksTests
{
    private static readonly ExtensionOption Connect = new("example-connect", ExtensionOptionArgument.None, "Run WebSocket over extended CONNECT");

    private static readonly ExtensionOption Relay = new("example-relay", ExtensionOptionArgument.Value, "Run WebSocket through a relay");

    [TestMethod]
    public void ActivatedBy_WithNoExtensionOptionGiven_GivesCurlsHandlersBack()
    {
        NamedHandler curls = new("ws", "wss");

        SchemeHooks hooks = SchemeHooks.ActivatedBy(ExtensionOptionValues.None, [ExampleHook.ActivatedBy(Connect, new ExampleHook(Connect))], UnusedTransferHookServices.Create());

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curls, hooks.Over(curls));
        Assert.IsNull(SchemeHooks.None.Hook);
        Assert.AreSame(curls, SchemeHooks.None.Over(curls));
    }

    [TestMethod]
    public void Over_WithAHandlerServingNoneOfTheHooksSchemes_GivesCurlsHandlerBack()
    {
        NamedHandler curls = new("http", "https");
        ExampleHook hook = new(Connect);

        SchemeHooks hooks = SchemeHooks.ActivatedBy(ValuesOf(Connect), [ExampleHook.ActivatedBy(Connect, hook)], UnusedTransferHookServices.Create());

        Assert.AreSame(hook, hooks.Hook);
        Assert.AreSame(curls, hooks.Over(curls));
        Assert.IsNull(hook.GivenHandler);
    }

    [TestMethod]
    public async Task Over_WithItsOptionGiven_ServesTheHooksSchemesWithItsHandlerAndTheRestWithCurls()
    {
        NamedHandler curls = new("ws", "wss", "http");
        ExampleHook hook = new(Connect);
        TransferHookServices services = UnusedTransferHookServices.Create();

        IProtocolHandler served = SchemeHooks.ActivatedBy(ValuesOf(Connect), [ExampleHook.ActivatedBy(Relay, new ExampleHook(Relay)), ExampleHook.ActivatedBy(Connect, hook)], services).Over(curls);

        Assert.AreSame(curls, hook.GivenHandler);
        Assert.AreSame(services, hook.GivenServices);
        Assert.AreSame(curls.SupportedSchemes, served.SupportedSchemes);
        Assert.AreEqual(1, (await served.ExecuteAsync(ContextFor("ws://example.com/chat"))).BytesTransferred);
        Assert.AreEqual(1, (await served.ExecuteAsync(ContextFor("WSS://example.com/chat"))).BytesTransferred);
        Assert.AreEqual(0, (await served.ExecuteAsync(ContextFor("http://example.com/"))).BytesTransferred);
        Assert.AreEqual(2, hook.Handler.Runs);
        Assert.AreEqual(1, curls.Runs);
    }

    [TestMethod]
    public void ActivatedBy_WithTwoExtensionsClaimingCurlsSchemes_FailsNamingBothOptions()
    {
        ExtensionHookConflictException failure = Assert.ThrowsExactly<ExtensionHookConflictException>(() => SchemeHooks.ActivatedBy(
            ValuesOf(Connect, Relay),
            [ExampleHook.ActivatedBy(Connect, new ExampleHook(Connect)), ExampleHook.ActivatedBy(Relay, new ExampleHook(Relay))],
            UnusedTransferHookServices.Create()));

        StringAssert.StartsWith(failure.Message, "the options --example-connect and --example-relay each serve a curl scheme in place of curl's handler; give only one of them");
    }

    [TestMethod]
    public void ActivatedBy_WithNoServices_Fails()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SchemeHooks.ActivatedBy(ExtensionOptionValues.None, [], null!));
    }

    private static ExtensionOptionValues ValuesOf(params ExtensionOption[] options) =>
        new(options.ToDictionary(option => option.Name, _ => ""));

    private static TransferContext ContextFor(string url) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
    };

    private sealed class ExampleHook(ExtensionOption option) : ISchemeHook
    {
        public ExtensionOption Option => option;

        public IReadOnlyCollection<string> Schemes { get; } = ["ws", "wss"];

        public NamedHandler Handler { get; } = new("ws", "wss");

        public IProtocolHandler? GivenHandler { get; private set; }

        public TransferHookServices? GivenServices { get; private set; }

        public static Func<ExtensionOptionValues, ISchemeHook?> ActivatedBy(ExtensionOption option, ExampleHook hook) =>
            values => values.IsGiven(option) ? hook : null;

        public IProtocolHandler HandlerOver(IProtocolHandler curlHandler, TransferHookServices services)
        {
            GivenHandler = curlHandler;
            GivenServices = services;
            return Handler;
        }
    }

    private sealed class NamedHandler(params string[] schemes) : IProtocolHandler
    {
        private readonly long bytes = schemes.Contains("http") ? 0 : 1;

        public int Runs { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes { get; } = schemes;

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Runs++;
            return ValueTask.FromResult(TransferResult.Success(bytes));
        }
    }
}
