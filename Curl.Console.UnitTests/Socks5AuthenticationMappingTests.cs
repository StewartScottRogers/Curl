using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how <c>--socks5-basic</c>, <c>--socks5-gssapi</c>, <c>--socks5-gssapi-service</c>,
/// <c>--proxy-service-name</c>, <c>--socks5-gssapi-nec</c> and <c>--delegation</c> become the
/// <see cref="Socks5AuthenticationOptions" /> the TCP connector's SOCKS5 greeting follows (BL-615).
/// </summary>
[TestClass]
public sealed class Socks5AuthenticationMappingTests
{
    private const string Url = "http://h/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new string[0], true, true, DisplayName = "Neither: both, as curl 8.21.0 measured")]
    [DataRow(new[] { "--socks5-basic" }, true, false, DisplayName = "--socks5-basic alone")]
    [DataRow(new[] { "--socks5-gssapi" }, false, true, DisplayName = "--socks5-gssapi alone")]
    [DataRow(new[] { "--socks5-basic", "--socks5-gssapi" }, true, true, DisplayName = "Both")]
    [DataRow(new[] { "--socks5-basic", "--no-socks5-basic" }, true, true, DisplayName = "Turned off again")]
    public void FromCommandLine_AllowsTheMethodsTheOptionsName(string[] arguments, bool allowBasic, bool allowGssapi)
    {
        Socks5AuthenticationOptions options = Map([.. arguments, Url]);
        Diagnostics.Act("allow basic / gssapi", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi}");

        Diagnostics.Assert("allow basic / gssapi", $"{allowBasic} / {allowGssapi}", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi}");
        Assert.AreEqual((allowBasic, allowGssapi), (options.AllowUserNameAndPassword, options.AllowGssapi));
    }

    [TestMethod]
    [DataRow(new string[0], "rcmd")]
    [DataRow(new[] { "--proxy-service-name", "proxysvc" }, "proxysvc")]
    [DataRow(new[] { "--socks5-gssapi-service", "sockssvc", "--proxy-service-name", "proxysvc" }, "sockssvc")]
    [DataRow(new[] { "--socks5-gssapi-service", "" }, "")]
    public void FromCommandLine_TakesTheGssapiServiceInCurlsOrder(string[] arguments, string service)
    {
        Socks5AuthenticationOptions options = Map([.. arguments, Url]);
        Diagnostics.Act("gssapi service name", options.GssapiServiceName);

        Diagnostics.Assert("gssapi service name", service, options.GssapiServiceName);
        Assert.AreEqual(service, options.GssapiServiceName);
    }

    [TestMethod]
    public void FromCommandLine_CopiesNecAndTheContextFactory()
    {
        LateBoundSecurityContextFactory contexts = new();
        Diagnostics.Arrange("uses SSPI", true);

        Socks5AuthenticationOptions options = Socks5AuthenticationMapping.FromCommandLine(Parse("--socks5-gssapi-nec", Url), contexts, usesSspi: true);
        Diagnostics.Act("nec / same contexts / SSPI texts", $"{options.GssapiNec} / {ReferenceEquals(contexts, options.SecurityContexts)} / {options.UsesSspiTexts}");

        Diagnostics.Assert("nec / same contexts / SSPI texts", "True / True / True", $"{options.GssapiNec} / {ReferenceEquals(contexts, options.SecurityContexts)} / {options.UsesSspiTexts}");
        Assert.IsTrue(options.GssapiNec);
        Assert.AreSame(contexts, options.SecurityContexts);
        Assert.IsTrue(options.UsesSspiTexts);
    }

    [TestMethod]
    [DataRow(true, SecurityDelegation.None, DisplayName = "SSPI build: no delegation")]
    [DataRow(false, SecurityDelegation.Always, DisplayName = "GSS-API build: --delegation")]
    public void FromCommandLine_AppliesDelegationOnlyInTheGssapiBuild(bool usesSspi, SecurityDelegation delegation)
    {
        Diagnostics.Arrange("uses SSPI", usesSspi);
        Socks5AuthenticationOptions options = Socks5AuthenticationMapping.FromCommandLine(Parse("--delegation", "always", Url), null, usesSspi);
        Diagnostics.Act("delegation / SSPI texts", $"{options.GssapiDelegation} / {options.UsesSspiTexts}");

        Diagnostics.Assert("delegation / SSPI texts", $"{delegation} / {usesSspi}", $"{options.GssapiDelegation} / {options.UsesSspiTexts}");
        Assert.AreEqual(delegation, options.GssapiDelegation);
        Assert.AreEqual(usesSspi, options.UsesSspiTexts);
    }

    [TestMethod]
    public void CreateTransports_WithSocks5Basic_GivesTheTcpConnectorTheOptionsAndTheRunsContexts()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--socks5-basic", Url));

        Socks5AuthenticationOptions options = transports.TcpConnector.Socks5Authentication;
        Diagnostics.Act("allow basic / gssapi / has contexts", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi} / {options.SecurityContexts is not null}");
        Diagnostics.Assert("allow basic / gssapi / has contexts", "True / False / True", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi} / {options.SecurityContexts is not null}");
        Assert.AreEqual((true, false), (options.AllowUserNameAndPassword, options.AllowGssapi));
        Assert.IsNotNull(options.SecurityContexts);
    }

    private Socks5AuthenticationOptions Map(string[] arguments) =>
        Socks5AuthenticationMapping.FromCommandLine(Parse(arguments), null, OperatingSystem.IsWindows());

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.ArrangeCommandLine(arguments);
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
