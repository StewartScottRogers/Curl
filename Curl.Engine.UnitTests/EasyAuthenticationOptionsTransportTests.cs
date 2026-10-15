using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Authentication;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using GssApiDelegation = Curl.Api.Options.Authentication.GssApiDelegation;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="NegotiateOptionsMapping" />, <see cref="HttpRequestOptionsMapping.ProxyAuthSchemesOf" /> and
/// <see cref="EngineComposition.CreateProxyTunnelOptions" /> read the Negotiate service names, <c>--delegation</c> and
/// the proxy's auth methods from an <see cref="EasyAuthenticationOptions" /> record built directly, as a libcurl
/// caller sets it (BL-2353).
/// </summary>
[TestClass]
public sealed class EasyAuthenticationOptionsTransportTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(GssApiDelegation.None, SecurityDelegation.None)]
    [DataRow(GssApiDelegation.PolicyFlag, SecurityDelegation.Policy)]
    [DataRow(GssApiDelegation.Flag, SecurityDelegation.Always)]
    public void NegotiateOptionsOf_Record_CopiesTheServiceNamesAndDelegation(GssApiDelegation delegation, SecurityDelegation expectedDelegation)
    {
        EasyAuthenticationOptions authentication = new() { ServiceName = "svc", ProxyServiceName = "proxysvc", GssApiDelegation = delegation };
        Diagnostics.Arrange("record", authentication);

        NegotiateOptions options = NegotiateOptionsMapping.Of(authentication);
        Diagnostics.Act("Negotiate options", options);

        NegotiateOptions expected = new("svc", "proxysvc", expectedDelegation);
        Diagnostics.Assert("Negotiate options", expected, options);
        Assert.AreEqual(expected, options);
    }

    [TestMethod]
    public void NegotiateOptionsOf_DefaultRecord_GivesNoServiceNamesAndNoDelegation()
    {
        NegotiateOptions options = NegotiateOptionsMapping.Of(new EasyAuthenticationOptions());
        Diagnostics.Act("Negotiate options", options);

        NegotiateOptions expected = new(null, null, SecurityDelegation.None);
        Diagnostics.Assert("Negotiate options", expected, options);
        Assert.AreEqual(expected, options);
    }

    [TestMethod]
    [DataRow(HttpAuthenticationMethods.Any, HttpAuthSchemes.Any)]
    [DataRow(HttpAuthenticationMethods.Basic, HttpAuthSchemes.Basic)]
    [DataRow(HttpAuthenticationMethods.Digest, HttpAuthSchemes.Digest)]
    [DataRow(HttpAuthenticationMethods.Ntlm, HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthenticationMethods.Negotiate, HttpAuthSchemes.Negotiate)]
    [DataRow(HttpAuthenticationMethods.None, HttpAuthSchemes.Basic)]
    [DataRow(HttpAuthenticationMethods.AwsSigV4, HttpAuthSchemes.Basic)]
    public void ProxyAuthSchemesOf_Methods_GiveTheSchemesTheCommandLineGives(HttpAuthenticationMethods methods, HttpAuthSchemes expected)
    {
        EasyAuthenticationOptions authentication = new() { ProxyAuthentication = methods };
        Diagnostics.Arrange("proxy methods", methods);

        HttpAuthSchemes schemes = HttpRequestOptionsMapping.ProxyAuthSchemesOf(authentication);
        Diagnostics.Act("schemes", schemes);

        Diagnostics.Assert("schemes", expected, schemes);
        Assert.AreEqual(expected, schemes);
    }

    [TestMethod]
    public void CreateProxyTunnelOptions_Record_AuthenticatesTheProxyWithTheRecordsMethods()
    {
        EasyAuthenticationOptions authentication = new() { ProxyAuthentication = HttpAuthenticationMethods.Ntlm };
        CommandLineOptions options = CommandLineParser.Parse(["http://h/"], _ => true).Options!;
        Diagnostics.Arrange("proxy methods", authentication.ProxyAuthentication);

        HttpProxyTunnelOptions tunnel = EngineComposition.CreateProxyTunnelOptions(options, new EasyProxyOptions(), authentication, new LateBoundSecurityContextFactory());
        Diagnostics.Act("tunnel schemes", tunnel.ProxyAuthSchemes);

        Diagnostics.Assert("tunnel schemes", HttpAuthSchemes.Ntlm, tunnel.ProxyAuthSchemes);
        Assert.AreEqual(HttpAuthSchemes.Ntlm, tunnel.ProxyAuthSchemes);
        Assert.IsNotNull(tunnel.ProxyAuthenticator);
    }
}
