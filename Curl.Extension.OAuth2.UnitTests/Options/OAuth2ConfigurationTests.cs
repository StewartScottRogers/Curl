using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Flows;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Options;

/// <summary>Pins how the OAuth2 options are read into a flow (ADR-1075).</summary>
[TestClass]
public sealed class OAuth2ConfigurationTests
{
    private const string TokenUrl = "https://server.example.com/token";

    [TestMethod]
    public void Read_ClientCredentials_GivesTheTokenEndpointClientAndScope()
    {
        var configuration = Read(
            ("oauth2-client-credentials", TokenUrl), ("oauth2-client-id", "s6BhdRkqt3"), ("oauth2-client-secret", "7Fjfp0ZBr1KtDRbnfVdmIw"), ("oauth2-scope", "read"));

        Assert.AreEqual(OAuth2FlowKind.ClientCredentials, configuration?.Flow);
        Assert.AreEqual(new OAuth2FlowRequest(new Uri(TokenUrl), new OAuth2Client("s6BhdRkqt3", "7Fjfp0ZBr1KtDRbnfVdmIw", OAuth2ClientAuthentication.Basic), "read"), configuration?.Request);
        Assert.IsNull(configuration?.FlowEndpoint);
        Assert.AreEqual(0, configuration?.RedirectPort);
        Assert.AreEqual("/default", configuration?.CacheDirectory);
        Assert.IsInstanceOfType<OAuth2ClientCredentialsFlow>(FlowOf(configuration!));
    }

    [TestMethod]
    public void Read_Device_GivesTheDeviceAuthorizationEndpointAndFlow()
    {
        var configuration = Read(("oauth2-device", TokenUrl), ("oauth2-client-id", "1406020730"), ("oauth2-device-url", "https://server.example.com/device"));

        Assert.AreEqual(OAuth2FlowKind.Device, configuration?.Flow);
        Assert.AreEqual(new Uri("https://server.example.com/device"), configuration?.FlowEndpoint);
        Assert.IsNull(configuration?.Request.Client.ClientSecret);
        Assert.IsInstanceOfType<OAuth2DeviceAuthorizationFlow>(FlowOf(configuration!));
    }

    [TestMethod]
    public void Read_Pkce_GivesTheAuthorizationEndpointPortAndFlow()
    {
        var configuration = Read(
            ("oauth2-pkce", TokenUrl), ("oauth2-client-id", "s6BhdRkqt3"), ("oauth2-authorize-url", "http://server.example.com/authorize"), ("oauth2-redirect-port", "65535"));

        Assert.AreEqual(OAuth2FlowKind.AuthorizationCodeWithPkce, configuration?.Flow);
        Assert.AreEqual(new Uri("http://server.example.com/authorize"), configuration?.FlowEndpoint);
        Assert.AreEqual(65535, configuration?.RedirectPort);
        Assert.IsInstanceOfType<OAuth2AuthorizationCodeFlow>(FlowOf(configuration!));
    }

    [TestMethod]
    public void Read_TokenCacheGiven_UsesItAndAnEmptyOneTurnsTheCacheOff()
    {
        Assert.AreEqual("/mine", Read(("oauth2-client-credentials", TokenUrl), ("oauth2-client-id", "a"), ("oauth2-token-cache", "/mine"))?.CacheDirectory);
        Assert.IsNull(Read(("oauth2-client-credentials", TokenUrl), ("oauth2-client-id", "a"), ("oauth2-token-cache", ""))?.CacheDirectory);
    }

    [TestMethod]
    public void Read_NoFlowOption_GivesNone()
    {
        Assert.IsNull(Read(("oauth2-client-id", "s6BhdRkqt3")));
    }

    [TestMethod]
    [DataRow("Use only one of --oauth2-client-credentials, --oauth2-device and --oauth2-pkce.", "oauth2-client-credentials", TokenUrl, "oauth2-pkce", TokenUrl)]
    [DataRow("--oauth2-device needs --oauth2-client-id.", "oauth2-device", TokenUrl, "oauth2-scope", "read")]
    [DataRow("--oauth2-device needs --oauth2-device-url.", "oauth2-device", TokenUrl, "oauth2-client-id", "a")]
    [DataRow("--oauth2-pkce needs --oauth2-authorize-url.", "oauth2-pkce", TokenUrl, "oauth2-client-id", "a")]
    [DataRow("--oauth2-client-credentials needs an http or https URL, not \"ftp://server.example.com/token\".", "oauth2-client-credentials", "ftp://server.example.com/token", "oauth2-client-id", "a")]
    [DataRow("--oauth2-client-credentials needs an http or https URL, not \"token\".", "oauth2-client-credentials", "token", "oauth2-client-id", "a")]
    [DataRow("--oauth2-redirect-port needs a port from 0 to 65535, not \"65536\".", "oauth2-client-credentials", TokenUrl, "oauth2-redirect-port", "65536")]
    [DataRow("--oauth2-redirect-port needs a port from 0 to 65535, not \"-1\".", "oauth2-client-credentials", TokenUrl, "oauth2-redirect-port", "-1")]
    public void Read_WrongOptions_ThrowsWithTheReason(string expected, string firstName, string firstValue, string secondName, string secondValue)
    {
        (string, string)[] options = secondName == "oauth2-redirect-port"
            ? [(firstName, firstValue), (secondName, secondValue), ("oauth2-client-id", "a")]
            : [(firstName, firstValue), (secondName, secondValue)];

        var exception = Assert.ThrowsExactly<OAuth2OptionException>(() => OAuth2Configuration.Read(OAuth2OptionsTests.Given(options), "/default"));

        Assert.AreEqual(expected, exception.Message);
    }

    private static OAuth2Configuration? Read(params (string Name, string Value)[] options) =>
        OAuth2Configuration.Read(OAuth2OptionsTests.Given(options), "/default");

    private static IOAuth2TokenFlow FlowOf(OAuth2Configuration configuration)
    {
        var connector = new QueuedConnector();
        return configuration.FlowFor(
            new OAuth2HttpClient(connector),
            new TransferHookServices(connector, new RecordedListener(ListenResult.Failed(CurlExitCode.CouldntConnect, "no"))),
            TimeProvider.System,
            TextWriter.Null,
            count => new byte[count]);
    }
}
