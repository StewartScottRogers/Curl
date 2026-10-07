using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// curl's <c>-v</c> line <c>socket successfully bound to interface '&lt;name&gt;'</c> (BL-1076), written
/// right after <c>Trying</c> when a plain or <c>if!</c> name's <c>SO_BINDTODEVICE</c> succeeds, even when
/// the connect is then refused, and never for <c>ifhost!</c> or a refused device bind: curl 8.18.0 on
/// Linux (WSL), <c>--interface lo http://127.0.0.1:1/</c> and the rest, BL-1026 and BL-1076 Notes.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string DeviceBoundLine = "socket successfully bound to interface 'lo'";

    [TestMethod]
    [DataRow("lo", "lo", DisplayName = "plain name")]
    [DataRow("lo", null, DisplayName = "if!")]
    public async Task ConnectAsync_WithANameTheDeviceBindTakes_ReportsTheDeviceBoundAfterTrying(string interfaceName, string? hostName)
    {
        var lines = await DeviceBindLinesAsync(new LocalBinding(interfaceName, hostName, null, 0, 1), deviceBinds: true, connects: true);

        Diagnostics.Assert("lines.Take(2).ToArray()", string.Join(" | ", new[] { "  Trying 127.0.0.1:47599...", DeviceBoundLine }), string.Join(" | ", lines.Take(2).ToArray()));
        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:47599...", DeviceBoundLine }, lines.Take(2).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_WithANameTheDeviceBindTakesAndARefusedConnect_StillReportsTheDeviceBound()
    {
        // curl 8.18.0 on WSL, --interface lo http://127.0.0.1:1/: the line, then
        // "connect to 127.0.0.1 port 1 from 127.0.0.1 port N failed: Connection refused".
        var lines = await DeviceBindLinesAsync(new LocalBinding("lo", "lo", null, 0, 1), deviceBinds: true, connects: false);

        Diagnostics.Assert("lines.Take(2).ToArray()", string.Join(" | ", new[] { "  Trying 127.0.0.1:47599...", DeviceBoundLine }), string.Join(" | ", lines.Take(2).ToArray()));
        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:47599...", DeviceBoundLine }, lines.Take(2).ToArray());
        StringAssert.StartsWith(lines[2], "connect to 127.0.0.1 port 47599 from ");
    }

    [TestMethod]
    [DataRow(true, DisplayName = "device bound")]
    [DataRow(false, DisplayName = "device refused")]
    public async Task ConnectAsync_WithIfhost_ReportsNoDeviceBound(bool deviceBinds)
    {
        var lines = await DeviceBindLinesAsync(new LocalBinding(null, "127.0.0.1", "lo", 0, 1), deviceBinds, connects: true);

        Diagnostics.Assert("lines[0]", "  Trying 127.0.0.1:47599...", lines[0]);
        Assert.AreEqual("  Trying 127.0.0.1:47599...", lines[0]);
        CollectionAssert.DoesNotContain(lines, DeviceBoundLine);
    }

    [TestMethod]
    public async Task ConnectAsync_WithANameTheDeviceBindRefuses_ReportsNoDeviceBound()
    {
        var lines = await DeviceBindLinesAsync(new LocalBinding("lo", "lo", null, 0, 1), deviceBinds: false, connects: true);

        Diagnostics.Assert("lines[0]", "  Trying 127.0.0.1:47599...", lines[0]);
        Assert.AreEqual("  Trying 127.0.0.1:47599...", lines[0]);
        CollectionAssert.DoesNotContain(lines, DeviceBoundLine);
    }

    private async Task<string[]> DeviceBindLinesAsync(LocalBinding binding, bool deviceBinds, bool connects)
    {
        var events = new RecordingTransferEvents();
        var inner = connects ? new FakeTcpDialer { DialOutcome = _ => new FakeConnection() } : new FakeTcpDialer();
        var connector = LocalBindingConnector(
            new FakeDeviceBindingTcpDialer(inner, deviceBinds),
            binding,
            Interfaces(("lo", [IPAddress.Loopback])));

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events });

        return LinesFromTheFirstTrying(events).ToArray();
    }
}
