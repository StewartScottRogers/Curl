using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.SshJump;

[TestClass]
public sealed class SshJumpExtensionTests
{
    private static readonly IConnector CurlConnector = new InMemoryJumpHost((_, _) => Task.CompletedTask);

    [TestMethod]
    public void Activate_WithoutTheOption_GivesNoHook()
    {
        Assert.IsNull(SshJumpExtension.Activate(ExtensionOptionValues.None));
        Assert.IsNull(SshJumpExtension.Activate(ExtensionOptionValues.None, SshJumpServices.System));
    }

    [TestMethod]
    public void Activate_WithAJumpHost_GivesAHookOverItsConnectors()
    {
        IConnectionHook hook = SshJumpExtension.Activate(ValuesOf("alice@bastion.example:2222"))!;

        Assert.AreSame(SshJumpExtension.Option, hook.Option);
        Assert.AreEqual(new SshJumpHost("alice", null, "bastion.example", 2222), ((SshJumpConnectionHook)hook).JumpHost);
        Assert.IsInstanceOfType<SshJumpConnector>(hook.ConnectorOver(CurlConnector));
        Assert.IsInstanceOfType<SshJumpConnector>(hook.ConnectorOver(CurlConnector, new NoTlsProvider()));
    }

    [TestMethod]
    public async Task DatagramConnectorOver_RefusesEveryDatagramSocketAsTheChannelCarriesTcpOnly()
    {
        IConnectionHook hook = SshJumpExtension.Activate(ValuesOf("alice@bastion.example"))!;

        DatagramOpenResult result = await hook.DatagramConnectorOver(new NoDatagramConnector()).OpenAsync("quic.example", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("the SSH jump host alice@bastion.example:22 carries TCP only, so UDP to quic.example port 443 is not sent", result.ErrorMessage);
    }

    [TestMethod]
    public void Activate_WithTextThatIsNotAJumpHost_IsTheCommandLineError()
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => SshJumpExtension.Activate(ValuesOf("bastion.example")));

        Assert.StartsWith("--ssh-jump needs user@host[:port], not 'bastion.example'", error.Message);
    }

    [TestMethod]
    public void Activate_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => SshJumpExtension.Activate(null!, SshJumpServices.System));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveSshJumpOut_SoTheParserRefusesItAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == "ssh-jump"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldSshJumpWithAValue()
    {
        ExtensionOption option = ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == "ssh-jump");

        Assert.AreSame(SshJumpExtension.Option, option);
        Assert.AreEqual(ExtensionOptionArgument.Value, option.Argument);
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithoutTheOption_KeepsCurlsConnectors()
    {
        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionConnectionHookCatalogue.Entries);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(CurlConnector, hooks.ConnectorOver(CurlConnector));
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithTheOption_ActivatesTheJumpHook() =>
        Assert.IsInstanceOfType<SshJumpConnectionHook>(ConnectionHooks.ActivatedBy(ValuesOf("alice@bastion.example"), ExtensionConnectionHookCatalogue.Entries).Hook);

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOption()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("ssh-jump", StringComparison.Ordinal)));
        Assert.Contains("--ssh-jump", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://www.rfc-editor.org/rfc/rfc4254#section-7.2"), SshJumpExtension.Description.Specification);
    }

    private static ExtensionOptionValues ValuesOf(string jumpHost) => new(new Dictionary<string, string> { ["ssh-jump"] = jumpHost });

    private sealed class NoDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new AssertFailedException("curl's datagram connector is never used under --ssh-jump");
    }

    private sealed class NoTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            throw new AssertFailedException("no handshake runs here");
    }
}
