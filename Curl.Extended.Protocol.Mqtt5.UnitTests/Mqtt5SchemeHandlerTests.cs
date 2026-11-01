using Curl.Extended.Protocol.Mqtt5.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5;

[TestClass]
public sealed class Mqtt5SchemeHandlerTests
{
    [TestMethod]
    [DataRow("mqtt5://broker/t", 1883, false)]
    [DataRow("mqtt5s://broker/t", 8883, true)]
    [DataRow("MQTT5+WS://broker/t", 80, false)]
    public async Task ExecuteAsync_HandsTheTransferToTheHandlerForItsScheme(string url, int port, bool useTls)
    {
        ScriptedMqtt5Connector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new Mqtt5SchemeHandler(connector).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(port, connector.Target!.Port);
        Assert.AreEqual(useTls, connector.Target.UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new Mqtt5SchemeHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("refused"))).ExecuteAsync(null!));
}
