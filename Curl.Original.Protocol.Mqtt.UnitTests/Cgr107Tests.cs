using System.Globalization;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Mqtt.Fakes;

namespace Curl.Original.Protocol.Mqtt;

/// <summary>
/// Pins CGR-107 (BL-3959): an mqtt transfer reports every packet byte it sent as
/// <c>%{size_request}</c>. The gap report measured real curl 8.22.0 printing 34 for a subscribe
/// to <c>mqtt://HOST/t</c> (here <c>mqtt://h/t</c>): the 26-byte CONNECT and the 8-byte SUBSCRIBE. A send that fails
/// counts none of its packet. The bytes are platform-neutral.
/// </summary>
[TestClass]
public sealed class Cgr107Tests
{
    [TestMethod]
    public async Task ExecuteAsync_Subscribe_ReportsConnectAndSubscribeBytesAsRequestSize()
    {
        ScriptedConnection connection = new(Bytes("20 02 00 00"), Bytes("90 03 00 01 00"), Bytes("30 04 00 01"), "tA"u8.ToArray());
        var context = new TransferContext { Url = CurlUrl.Parse("mqtt://h/t"), Output = new RecordingStream() };

        TransferResult result = await new MqttProtocolHandler(FakeConnector.For(connection), () => "PBadK4E3").ExecuteAsync(context);

        Assert.AreEqual(34, result.Report!.RequestSize);
        Assert.AreEqual(result.BytesTransferred, result.Report.DownloadSize);
    }

    private static byte[] Bytes(string hex) =>
        [.. hex.Split(' ').Select(pair => byte.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture))];
}
