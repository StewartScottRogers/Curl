using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// curl's <c>-v</c> line <c>Local Interface &lt;name&gt; is ip &lt;address&gt; using address family &lt;n&gt;</c>
/// (BL-1079), written after <c>Trying</c> when a plain or <c>if!</c> name's <c>SO_BINDTODEVICE</c> is refused and
/// the interface has an address of the family dialled: curl 8.22.0 on Linux (Docker, a seccomp profile refusing
/// <c>setsockopt(SOL_SOCKET, SO_BINDTODEVICE)</c> with <c>EPERM</c>), <c>--interface lo http://127.0.0.1:1/</c>
/// and the rest, BL-1079 Notes. A plain name also writes the address resolved as the host; <c>if!</c> does not.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithAPlainNameTheDeviceBindRefuses_ReportsTheLocalInterfaceThenItsAddressResolved()
    {
        // curl 8.22.0 Linux, --interface lo http://127.0.0.1:1/: "Local Interface lo is ip 127.0.0.1 using
        // address family 2", "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2", "Local port: 0".
        var lines = await DeviceBindLinesAsync(new LocalBinding("lo", "lo", null, 0, 1), deviceBinds: false, connects: true);

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:47599...",
                "Local Interface lo is ip 127.0.0.1 using address family 2",
                "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2",
            },
            lines.Take(3).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnIfNameTheDeviceBindRefuses_ReportsTheLocalInterfaceAlone()
    {
        // curl 8.22.0 Linux, --interface if!lo http://127.0.0.1:1/: "Local Interface lo is ip 127.0.0.1 using
        // address family 2", then "Local port: 0" with no Name line.
        var lines = await DeviceBindLinesAsync(new LocalBinding("lo", null, null, 0, 1), deviceBinds: false, connects: true);

        Assert.AreEqual("Local Interface lo is ip 127.0.0.1 using address family 2", lines[1]);
        Assert.IsFalse(lines.Any(line => line.StartsWith("Name ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "device bound")]
    [DataRow(false, DisplayName = "device refused")]
    public async Task ConnectAsync_WithIfhost_ReportsNoLocalInterface(bool deviceBinds)
    {
        // curl 8.22.0 Linux, --interface ifhost!lo!127.0.0.1: the Name line alone.
        var lines = await DeviceBindLinesAsync(new LocalBinding(null, "127.0.0.1", "lo", 0, 1), deviceBinds, connects: true);

        Assert.IsFalse(lines.Any(line => line.StartsWith("Local Interface ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_WithANameTheDeviceBindTakes_ReportsNoLocalInterface()
    {
        var lines = await DeviceBindLinesAsync(new LocalBinding("lo", "lo", null, 0, 1), deviceBinds: true, connects: true);

        Assert.IsFalse(lines.Any(line => line.StartsWith("Local Interface ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task ConnectAsync_WithAPlainNameTheDeviceBindRefusesToIPv6OnLinux_ReportsAddressFamily10()
    {
        // curl 8.22.0 Linux, --interface lo http://[::1]:1/: "Local Interface lo is ip ::1 using address
        // family 10", "Name '::1' family 10 resolved to '::1' family 10".
        var events = new RecordingTransferEvents();
        var connector = LocalBindingConnector(
            new FakeDeviceBindingTcpDialer(new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, deviceBinds: false),
            new LocalBinding("lo", "lo", null, 0, 1),
            Interfaces(("lo", [IPAddress.IPv6Loopback, IPAddress.Loopback])));

        await connector.ConnectAsync(new ConnectTarget("::1", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying [::1]:47599...",
                "Local Interface lo is ip ::1 using address family 10",
                "Name '::1' family 10 resolved to '::1' family 10",
            },
            LinesFromTheFirstTrying(events).Take(3).ToArray());
    }
}
