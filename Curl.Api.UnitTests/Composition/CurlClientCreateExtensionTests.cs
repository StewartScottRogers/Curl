using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Api.Composition;

// The real managed engine with a test extension (BL-4098): the extension's handler answers in process, so nothing is sent.
[TestClass]
public sealed class CurlClientCreateExtensionTests
{
    private const string ExtensionUrl = "example-extension://host/";

    [TestMethod]
    public async Task Create_WithExtensionsOn_SendsAnExtensionSchemeToItsHandler()
    {
        ReachedExtensionHandler handler = new();
        await using CurlClient client = CurlClient.Create(new CurlClientOptions(), _ => null, _ => [handler]);

        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, ExtensionUrl), CancellationToken.None);
        await response.Body.DisposeAsync();

        Assert.AreEqual(1, handler.Transfers);
    }

    [TestMethod]
    public async Task Create_WithCurlExtensionsZero_RefusesTheExtensionSchemeAsUnsupported()
    {
        ReachedExtensionHandler handler = new();
        await using CurlClient client = CurlClient.Create(
            new CurlClientOptions(),
            name => name == "CURL_EXTENSIONS" ? "0" : null,
            _ => [handler]);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(
            () => client.SendAsync(new CurlRequest(HttpMethod.Get, ExtensionUrl), CancellationToken.None));

        Assert.AreEqual(CurlCode.UnsupportedProtocol, failure.Code);
        Assert.AreEqual(0, handler.Transfers);
    }

    [TestMethod]
    public async Task SendAsync_WithDnsOptionsAndExtensionsOn_RunsTheTransfer()
    {
        ReachedExtensionHandler handler = new();
        await using CurlClient client = CurlClient.Create(new CurlClientOptions(), _ => null, _ => [handler]);

        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, ExtensionUrl) { Dns = new CurlDnsOptions { Json = true, Transport = "tcp" } },
            CancellationToken.None);
        await response.Body.DisposeAsync();

        Assert.AreEqual(1, handler.Transfers);
    }

    [TestMethod]
    public async Task SendAsync_WithDnsOptionsAndCurlExtensionsZero_FailsAsAnUnknownOption()
    {
        ReachedExtensionHandler handler = new();
        await using CurlClient client = CurlClient.Create(
            new CurlClientOptions(),
            name => name == "CURL_EXTENSIONS" ? "0" : null,
            _ => [handler]);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(
            () => client.SendAsync(new CurlRequest(HttpMethod.Get, ExtensionUrl) { Dns = new CurlDnsOptions { Json = true } }, CancellationToken.None));

        Assert.AreEqual(CurlCode.UnknownOption, failure.Code);
        Assert.AreEqual(0, handler.Transfers);
    }

    [TestMethod]
    public void ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook()
    {
        Dictionary<string, string> values = new() { ["dns-json"] = string.Empty };

        Func<IProtocolHandler, IProtocolHandler>? around = CurlClient.ExtensionTransferHooksFor(values);
        Assert.IsNotNull(around);
        ReachedExtensionHandler inner = new();
        IProtocolHandler wrapped = around(inner);
        Assert.AreNotSame(inner, wrapped);
        Assert.AreEqual("TransferHookingProtocolHandler", wrapped.GetType().Name);
        Assert.AreEqual(1, CountHooks(wrapped));
        Assert.AreEqual("DnsQueryTransferHook", HooksOf(wrapped)[0].GetType().Name);
        TransferHooks hooks = TransferHooks.ActivatedBy(new ExtensionOptionValues(values), ExtensionTransferHookCatalogue.Entries);
        Assert.AreEqual(1, hooks.Hooks.Count);
        Assert.AreEqual("DnsQueryTransferHook", hooks.Hooks[0].GetType().Name);
    }

    [TestMethod]
    public void ExtensionTransferHooksFor_NoOptions_ActivatesNone()
    {
        Assert.IsNull(CurlClient.ExtensionTransferHooksFor(new Dictionary<string, string>()));
    }

    [TestMethod]
    public void ToExtensionOptions_TransportAndJson_NameBoth()
    {
        IReadOnlyDictionary<string, string> values = new CurlDnsOptions { Transport = "tcp", Json = true }.ToExtensionOptions();

        Assert.AreEqual("tcp", values["dns-transport"]);
        Assert.AreEqual(string.Empty, values["dns-json"]);
    }

    [TestMethod]
    public void ToExtensionOptions_Default_IsEmpty()
    {
        Assert.AreEqual(0, new CurlDnsOptions().ToExtensionOptions().Count);
    }

    private static int CountHooks(IProtocolHandler wrapped) => HooksOf(wrapped).Count;

    private static IReadOnlyList<ITransferHook> HooksOf(IProtocolHandler wrapped) =>
        (IReadOnlyList<ITransferHook>)wrapped.GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(IReadOnlyList<ITransferHook>))
            .GetValue(wrapped)!;

    private sealed class ReachedExtensionHandler : IProtocolHandler
    {
        public int Transfers { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["example-extension"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Transfers++;
            return ValueTask.FromResult(TransferResult.Success(0));
        }
    }
}
