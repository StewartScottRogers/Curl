using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Console;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Engine;

/// <summary>
/// Tests that <see cref="HttpRequestOptionsMapping.Of" /> carries <c>--limit-rate</c>'s receive speed,
/// <see cref="EasyTransferOptions.MaxReceiveSpeed" />, to the HTTP handler as
/// <see cref="HttpRequestOptions.ReceiveBytesPerSecond" /> (BL-4045).
/// </summary>
[TestClass]
public sealed class HttpRequestOptionsMappingReceiveSpeedTests
{
    private static readonly CommandLineOptions NoOptions = CommandLineParser.Parse(["http://example.com/"], _ => true).Options!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(-1L, null)]
    [DataRow(0L, null)]
    [DataRow(10L, 10L)]
    public void Of_MaxReceiveSpeed_IsTheReceiveRateWhenPositive(long maxReceiveSpeed, long? expected)
    {
        Diagnostics.Arrange("max receive speed", maxReceiveSpeed);

        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyHttpOptions(), new EasyBehaviourOptions(), new EasyTransferOptions { MaxReceiveSpeed = maxReceiveSpeed }, new EasyAuthenticationOptions());

        Diagnostics.Assert("receive rate", expected, mapped.ReceiveBytesPerSecond);
        Assert.AreEqual(expected, mapped.ReceiveBytesPerSecond);
    }
}
