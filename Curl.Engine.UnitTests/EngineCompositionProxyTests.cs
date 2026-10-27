using System.Text;

using Curl.Authentication;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins how <see cref="EngineComposition" /> maps the proxy options onto the HTTP proxy tunnel's options
/// and the tunnel's authenticator.
/// </summary>
[TestClass]
public sealed class EngineCompositionProxyTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);
    [TestMethod]
    public void CreateProxyTunnelOptions_EncodesCommandLineTextInThePlatformEncoding()
    {
        HttpProxyTunnelOptions options = TunnelOptionsFor(["-p", "-x", "127.0.0.1:18238", "http://example.com/a"]);

        // The code page itself differs by platform, so only whether it is the platform's is written.
        Diagnostics.Assert(
            "command-line text is in the platform's credential encoding",
            true,
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()).CodePage == options.CommandLineTextEncoding.CodePage);
        Assert.AreEqual(
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()).CodePage,
            options.CommandLineTextEncoding.CodePage);
    }

    [TestMethod]
    [DataRow(new[] { "-p", "-x", "127.0.0.1:18602", "http://example.com/a" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-digest", "-p", "-x", "127.0.0.1:18602", "http://example.com/a" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--proxy-anyauth", "-p", "-x", "127.0.0.1:18602", "http://example.com/a" }, HttpAuthSchemes.Any)]
    public void CreateProxyTunnelOptions_AllowsTheSchemeTheProxyAuthSwitchesPickAndAnswersWithTheRankedAuthenticator(string[] arguments, HttpAuthSchemes expected)
    {
        HttpProxyTunnelOptions options = TunnelOptionsFor(arguments);

        Diagnostics.Assert("options.ProxyAuthSchemes", expected, options.ProxyAuthSchemes);
        Assert.AreEqual(expected, options.ProxyAuthSchemes);
        Assert.IsInstanceOfType<RankedHttpAuthenticator>(options.ProxyAuthenticator);
    }

    [TestMethod]
    public async Task CreateTransports_ProxyNtlm_AnswersTheTunnelWithTheRoutersType1()
    {
        // The tunnel's authenticator is bound to ADR-0142's router once the connectors exist
        // (BL-604): SSPI's Type 1 on Windows, curl's own NTLM's elsewhere.
        string[] arguments = ["--proxy-ntlm", "-U", "u:p", "-p", "-x", "127.0.0.1:18604", "http://example.test/"];
        Diagnostics.ArrangeCommandLine(arguments);
        CurlTransports transports = EngineComposition.CreateTransports(
            EasyOptionsFromCommandLine.ToolOptionsOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.NetworkOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.TlsOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.HttpOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.ProxyOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.AuthenticationOf(CommandLineParser.Parse(arguments, _ => true).Options!));
        HttpAuthRequest request = new("CONNECT", CurlUrl.Parse("http://127.0.0.1:18604/"), "example.test:80", new System.Net.NetworkCredential("u", "p"), null, HttpAuthSchemes.Ntlm, IsProxy: true);

        string? value = await transports.ProxyTunnelOptions.ProxyAuthenticator!.CreateAuthorizationAsync(request, [], CancellationToken.None);
        bool isType1 = value?.StartsWith("NTLM TlRMTVNTUAAB", StringComparison.Ordinal) == true;
        Diagnostics.Act("Proxy-Authorization is an NTLM Type 1", isType1);

        Diagnostics.Assert("Proxy-Authorization is an NTLM Type 1", true, isType1);
        StringAssert.StartsWith(value, "NTLM TlRMTVNTUAAB", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateProxyTunnelOptions_ProxyHttp2_AsksTheConnectorForAnHttp2Tunnel()
    {
        HttpProxyTunnelOptions options = OpenSslBuildTunnelOptionsFor(["--proxy-http2", "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a"]);

        Diagnostics.Assert("options.ProxyHttp2", true, options.ProxyHttp2);
        Assert.IsTrue(options.ProxyHttp2);
    }

    [TestMethod]
    [DataRow(new[] { "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a" }, DisplayName = "left out")]
    [DataRow(new[] { "--proxy-http2", "--no-proxy-http2", "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a" }, DisplayName = "--no-proxy-http2")]
    public void CreateProxyTunnelOptions_WithoutProxyHttp2_LeavesTheHttp2TunnelOff(string[] arguments)
    {
        HttpProxyTunnelOptions options = OpenSslBuildTunnelOptionsFor(arguments);

        Diagnostics.Assert("options.ProxyHttp2", false, options.ProxyHttp2);
        Assert.IsFalse(options.ProxyHttp2);
    }


    /// <summary>The tunnel options the production composition maps from <paramref name="arguments" />.</summary>
    private HttpProxyTunnelOptions TunnelOptionsFor(string[] arguments)
    {
        Diagnostics.ArrangeCommandLine(arguments);
        HttpProxyTunnelOptions options =
            EngineComposition.CreateProxyTunnelOptions(EasyOptionsFromCommandLine.HttpOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.ProxyOf(CommandLineParser.Parse(arguments, _ => true).Options!), EasyOptionsFromCommandLine.AuthenticationOf(CommandLineParser.Parse(arguments, _ => true).Options!), new SystemSecurityContextFactory());
        Diagnostics.Act("proxy auth schemes, HTTP/2 tunnel", $"{options.ProxyAuthSchemes}, {options.ProxyHttp2}");
        return options;
    }

    /// <summary>The tunnel options the production composition maps from <paramref name="arguments" /> parsed as curl's OpenSSL build parses them.</summary>
    private HttpProxyTunnelOptions OpenSslBuildTunnelOptionsFor(string[] arguments)
    {
        Diagnostics.ArrangeCommandLine(arguments);
        HttpProxyTunnelOptions options =
            EngineComposition.CreateProxyTunnelOptions(
                EasyOptionsFromCommandLine.HttpOf(CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, isWindows: false).Options!),
                EasyOptionsFromCommandLine.ProxyOf(CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, isWindows: false).Options!), EasyOptionsFromCommandLine.AuthenticationOf(CommandLineParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, DiskDataFileReader.ForProcess, isWindows: false).Options!),
                new SystemSecurityContextFactory());
        Diagnostics.Act("proxy auth schemes, HTTP/2 tunnel", $"{options.ProxyAuthSchemes}, {options.ProxyHttp2}");
        return options;
    }
}

/// <summary>Answers every password prompt with an empty password, so a test that parses a <c>-u</c> user without a password never waits on the process console.</summary>
internal sealed class EmptyPasswordPrompt : IPasswordPrompt
{
    /// <inheritdoc/>
    public string ReadPassword(string prompt) => string.Empty;
}
