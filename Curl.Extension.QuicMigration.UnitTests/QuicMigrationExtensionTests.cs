using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>Checks the <c>--quic-migrate</c> option's activation, its hook and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class QuicMigrationExtensionTests
{
    private static readonly ExtensionOptionValues Given = new(new Dictionary<string, string> { ["quic-migrate"] = string.Empty });

    [TestMethod]
    public void Activate_WithoutTheOption_GivesNoHook() =>
        Assert.IsNull(QuicMigrationExtension.Activate(ExtensionOptionValues.None));

    [TestMethod]
    public void Activate_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicMigrationExtension.Activate(null!));

    [TestMethod]
    public void Activate_WithTheOption_GivesAHookThatKeepsCurlsStreamConnectorAndWrapsItsDatagramConnector()
    {
        FakeDatagramConnector curlDatagramConnector = new();
        IConnector curlConnector = new UnusedConnector();

        IConnectionHook hook = QuicMigrationExtension.Activate(Given)!;

        Assert.AreSame(QuicMigrationExtension.Option, hook.Option);
        Assert.AreSame(curlConnector, hook.ConnectorOver(curlConnector));
        Assert.AreSame(curlDatagramConnector, ((MigratingDatagramConnector)hook.DatagramConnectorOver(curlDatagramConnector)).CurlDatagramConnector);
    }

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveQuicMigrateOut_SoTheParserRefusesItAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == "quic-migrate"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldQuicMigrateAsAFlag()
    {
        ExtensionOption option = ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == "quic-migrate");

        Assert.AreSame(QuicMigrationExtension.Option, option);
        Assert.AreEqual(ExtensionOptionArgument.None, option.Argument);
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithoutTheOption_KeepsCurlsDatagramConnector()
    {
        FakeDatagramConnector curlDatagramConnector = new();

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionConnectionHookCatalogue.Entries);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curlDatagramConnector, hooks.DatagramConnectorOver(curlDatagramConnector));
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithTheOption_WrapsCurlsDatagramConnector() =>
        Assert.IsInstanceOfType<MigratingDatagramConnector>(ConnectionHooks.ActivatedBy(Given, ExtensionConnectionHookCatalogue.Entries).DatagramConnectorOver(new FakeDatagramConnector()));

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOption()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("quic-migrate", StringComparison.Ordinal)));
        Assert.Contains("--quic-migrate", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://www.rfc-editor.org/rfc/rfc9000#section-9"), QuicMigrationExtension.Description.Specification);
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
