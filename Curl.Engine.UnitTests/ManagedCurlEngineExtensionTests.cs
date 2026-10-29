using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedCurlEngine" />'s public constructor that takes a host's extension handlers (ADR-0999,
/// BL-4098): a transfer to an extension's scheme reaches its handler, and with none that scheme is unsupported.
/// </summary>
[TestClass]
public sealed class ManagedCurlEngineExtensionTests
{
    private const string ExtensionUrl = "example-extension://host/";

    [TestMethod]
    public async Task PerformAsync_WithAnExtensionHandler_ReachesItForItsScheme()
    {
        ReachedExtensionHandler handler = new();
        await using ManagedCurlEngine engine = new(_ => [handler]);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = ExtensionUrl } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(1, handler.Transfers);
    }

    [TestMethod]
    public async Task PerformAsync_WithNoExtensionHandlers_RefusesTheExtensionScheme()
    {
        await using ManagedCurlEngine engine = new(null);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = ExtensionUrl } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, code);
    }

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
