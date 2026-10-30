using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Composition;
using Curl.Extension.WireGuard.UnitTests.Tunnel;

namespace Curl.Extension.WireGuard.UnitTests;

/// <summary>Checks the <c>--wireguard</c> option's activation and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class WireGuardExtensionTests
{
    [TestMethod]
    public void Activate_WithoutTheOption_GivesNoHook()
    {
        Assert.IsNull(WireGuardExtension.Activate(ExtensionOptionValues.None));
        Assert.IsNull(WireGuardExtension.Activate(ExtensionOptionValues.None, _ => throw new AssertFailedException("nothing to read"), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create()));
    }

    [TestMethod]
    public void Activate_WithAConfiguration_GivesAHookOverIt()
    {
        IConnectionHook? hook = Activate(_ => TunnelTestPeer.ConfigurationText());

        WireGuardConnectionHook wireGuard = Assert.IsInstanceOfType<WireGuardConnectionHook>(hook);
        Assert.AreEqual("192.0.2.1", wireGuard.Configuration.Peers[0].Endpoint!.Host);
    }

    [TestMethod]
    public void Activate_UnreadableFile_IsTheCommandLineError()
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => Activate(_ => throw new FileNotFoundException("no such file")));

        Assert.StartsWith("--wireguard cannot read 'wg0.conf': no such file", error.Message);
    }

    [TestMethod]
    public void Activate_FileThatIsNotWgQuick_IsTheCommandLineErrorNamingTheLine()
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => Activate(_ => "[Interface]\n"));

        Assert.StartsWith("--wireguard 'wg0.conf' line ", error.Message);
    }

    [TestMethod]
    public void Activate_NoPeerHasAnEndpoint_IsTheCommandLineError()
    {
        string withoutEndpoint = TunnelTestPeer.ConfigurationText().Replace("Endpoint = 192.0.2.1:51820", string.Empty, StringComparison.Ordinal);

        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => Activate(_ => withoutEndpoint));

        Assert.StartsWith("--wireguard needs a [Peer] with an Endpoint in 'wg0.conf'", error.Message);
    }

    [TestMethod]
    public void Activate_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => WireGuardExtension.Activate(null!, _ => string.Empty, new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create()));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveWireGuardOut_SoTheParserRefusesItAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == "wireguard"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldWireGuardWithAValue()
    {
        ExtensionOption option = ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == "wireguard");

        Assert.AreSame(WireGuardExtension.Option, option);
        Assert.AreEqual(ExtensionOptionArgument.Value, option.Argument);
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithoutTheOption_ActivatesNoHook() =>
        Assert.IsNull(ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionConnectionHookCatalogue.Entries).Hook);

    [TestMethod]
    public void ConnectionHookCatalogue_WithTheOption_ActivatesTheWireGuardHookFromTheFile()
    {
        string directory = Directory.CreateTempSubdirectory("curl-wireguard-").FullName;
        try
        {
            string path = Path.Combine(directory, "wg0.conf");
            File.WriteAllText(path, TunnelTestPeer.ConfigurationText());

            IConnectionHook? hook = ConnectionHooks.ActivatedBy(ValuesOf(path), ExtensionConnectionHookCatalogue.Entries).Hook;

            Assert.IsInstanceOfType<WireGuardConnectionHook>(hook);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOption()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("wireguard", StringComparison.Ordinal)));
        Assert.Contains("--wireguard", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://www.wireguard.com/papers/wireguard.pdf"), WireGuardExtension.Description.Specification);
    }

    private static IConnectionHook? Activate(Func<string, string> readFile) =>
        WireGuardExtension.Activate(ValuesOf("wg0.conf"), readFile, new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

    private static ExtensionOptionValues ValuesOf(string path) => new(new Dictionary<string, string> { ["wireguard"] = path });
}
