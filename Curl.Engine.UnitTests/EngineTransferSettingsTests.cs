using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

[TestClass]
public sealed class EngineTransferSettingsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // The command-line host builds the settings without naming PostWithoutData, so every command-line transfer
    // takes the default: true would send a CoAP GET as a POST and fail an MQTT transfer with exit 43.
    [TestMethod]
    public void Constructor_PostWithoutDataNotNamed_IsNotPostWithoutData()
    {
        Diagnostics.Arrange("PostWithoutData", "not named");

        EngineTransferSettings settings = new(
            Silent: false,
            ShowError: false,
            FailMode: HttpFailMode.None,
            CreateFileMode: null,
            Insecure: false,
            Passphrase: null,
            Engine: null,
            EchModeIsMalformed: false,
            PostData: null,
            ProxyCredentials: null,
            AllowedProtocols: null,
            AllowedRedirectProtocols: null,
            SslLevel: TransportSecurityLevel.None,
            FtpSslControlOnly: false,
            SslRequired: false,
            ListOnly: false,
            UploadFlags: [],
            ProxyHeaders: [],
            ProxyTunnel: false,
            OverUnixSocket: false,
            HappyEyeballsTimeout: TimeSpan.Zero,
            AppendUrlQuery: url => url);
        Diagnostics.Act("PostWithoutData", settings.PostWithoutData);

        Diagnostics.Assert("PostWithoutData", false, settings.PostWithoutData);
        Assert.IsFalse(settings.PostWithoutData);
    }
}
