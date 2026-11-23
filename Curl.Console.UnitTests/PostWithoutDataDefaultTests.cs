using Curl.Cli;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a command-line transfer does not take <see cref="EngineTransferSettings.PostWithoutData" />
/// unless it asks for it: <see cref="EasyOptionsFromCommandLine.TransferSettingsOf" /> leaves it at its default,
/// so a CoAP GET stays a GET and an MQTT publish is not refused with exit 43 (AF-0987).
/// </summary>
[TestClass]
public sealed class PostWithoutDataDefaultTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TransferSettingsOf_PlainCommandLine_IsNotPostWithoutData()
    {
        Diagnostics.Arrange("arguments", "coap://example.com/");
        CommandLineParseResult result = QuicBuildParser.Parse(["coap://example.com/"], _ => true);
        Assert.IsTrue(result.IsAccepted);

        EngineTransferSettings settings = EasyOptionsFromCommandLine.TransferSettingsOf(result.Options);

        Diagnostics.Assert("PostWithoutData", false, settings.PostWithoutData);
        Assert.IsFalse(settings.PostWithoutData);
    }
}
