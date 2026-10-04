using System.Net;
using System.Text;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>ldap://</c> and <c>ldaps://</c> end to end through the production composition over
/// fake connectors: the handler <see cref="CurlComposition.CreateProtocolHandlers" /> registers,
/// the bytes it sends, its standard output, its <c>-v</c> lines and exit code, and the
/// <c>-V</c> protocol list. Recorded on 2026-09-29 with <c>Record-CurlExchange.ps1 -Script</c>
/// (BL-589 Notes), curl running <c>-sv -u cn=u,dc=x:secret ldap://127.0.0.1:18389/dc=example</c>
/// against a bind answered <c>success</c> (or <c>invalidCredentials</c>), one entry
/// <c>dc=example</c> with <c>ou: x</c> and a SearchResultDone: curl 8.21.0 with WinLDAP on
/// Windows, curl 8.18.0 with OpenLDAP 2.6.10 under WSL elsewhere, since the composition picks
/// the dialect by platform. The connector reports the <c>Trying</c> and
/// <c>Established connection</c> lines, as <c>TcpConnector</c> does; the <c>schannel:</c>
/// lines curl writes for <c>ldaps</c> are the TLS connector's and are not written here.
/// </summary>
[TestClass]
public sealed class CurlCompositionLdapTests
{
    private const string BindSuccess = "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00";

    private const string EntryOuX = "30 1e 02 01 02 64 19 04 0a 64 63 3d 65 78 61 6d 70 6c 65 30 0b 30 09 04 02 6f 75 31 03 04 01 78";

    private const string SearchDone = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    private const string InvalidCredentials1 = "30 0c 02 01 01 61 07 0a 01 31 04 00 04 00";

    private const string InvalidCredentials2 = "30 0c 02 01 02 61 07 0a 01 31 04 00 04 00";

    private const string WinLdapSent =
        "30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74 "
        + "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00 "
        + "30 84 00 00 00 05 02 01 03 42 00";

    private const string OpenLdapSent =
        "30 1b 02 01 01 60 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74 "
        + "30 2f 02 01 02 63 2a 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00 "
        + "30 05 02 01 03 42 00";

    private const string WinLdapEntry = "DN: dc=example\n\tou: x\n\n";

    private const string OpenLdapEntry = "DN: dc=example\n\tou: x\n\n\n";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateRunner_WinLdapVerboseSearch_WritesTheMeasuredLinesAndTheEntry()
    {
        (int exitCode, string standardOutput, string standardError, ScriptedConnector connector) =
            await RunAsync("ldap://127.0.0.1:18389/dc=example", 59226, BindSuccess, EntryOuX, SearchDone);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WinLdapEntry, standardOutput);
        Assert.AreEqual(
            Lines(
                "*   Trying 127.0.0.1:18389...",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18389) from 127.0.0.1 port 59226 ",
                "* LDAP local: LDAP Vendor = Microsoft Corporation. ; LDAP Version = 510",
                "* LDAP local: ldap://127.0.0.1:18389/dc=example",
                "* LDAP local: trying to establish cleartext connection",
                "{ [4 bytes data]",
                "* shutting down connection #0"),
            standardError);
        Assert.AreEqual(WinLdapSent, Hex(connector.Written));
        Assert.AreEqual(("127.0.0.1", 18389, false), connector.Targets.Select(target => (target.Host, target.Port, target.UseTls)).Single());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateRunner_WinLdapsVerboseSearch_ConnectsWithTlsAndSaysTheConnectionIsEncrypted()
    {
        (int exitCode, string standardOutput, string standardError, ScriptedConnector connector) =
            await RunAsync("ldaps://127.0.0.1:18636/dc=example", 50261, BindSuccess, EntryOuX, SearchDone);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WinLdapEntry, standardOutput);
        Assert.AreEqual(
            Lines(
                "*   Trying 127.0.0.1:18636...",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18636) from 127.0.0.1 port 50261 ",
                "* LDAP local: LDAP Vendor = Microsoft Corporation. ; LDAP Version = 510",
                "* LDAP local: ldaps://127.0.0.1:18636/dc=example",
                "* LDAP local: trying to establish encrypted connection",
                "{ [4 bytes data]",
                "* shutting down connection #0"),
            standardError);
        Assert.AreEqual(("127.0.0.1", 18636, true), connector.Targets.Select(target => (target.Host, target.Port, target.UseTls)).Single());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateRunner_WinLdapVerboseBindRefused_WritesTheFailureAndExits38()
    {
        (int exitCode, string standardOutput, string standardError, _) =
            await RunAsync("ldap://127.0.0.1:18389/dc=example", 54983, InvalidCredentials1, InvalidCredentials2);

        Assert.AreEqual(38, exitCode);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                "*   Trying 127.0.0.1:18389...",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18389) from 127.0.0.1 port 54983 ",
                "* LDAP local: LDAP Vendor = Microsoft Corporation. ; LDAP Version = 510",
                "* LDAP local: ldap://127.0.0.1:18389/dc=example",
                "* LDAP local: trying to establish cleartext connection",
                "* LDAP local: bind via ldap_win_bind Invalid Credentials",
                "* shutting down connection #0"),
            standardError);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CreateRunner_OpenLdapVerboseSearch_WritesTheMeasuredLinesAndTheEntry()
    {
        (int exitCode, string standardOutput, string standardError, ScriptedConnector connector) =
            await RunAsync("ldap://127.0.0.1:18389/dc=example", 44296, BindSuccess, EntryOuX, SearchDone);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(OpenLdapEntry, standardOutput);
        Assert.AreEqual(
            Lines(
                "*   Trying 127.0.0.1:18389...",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18389) from 127.0.0.1 port 44296 ",
                "* LDAP local: ldap://127.0.0.1:18389/dc=example",
                "{ [4 bytes data]",
                "* Connection #0 to host 127.0.0.1:18389 left intact"),
            standardError);
        Assert.AreEqual(OpenLdapSent, Hex(connector.Written));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CreateRunner_OpenLdapsVerboseSearch_ConnectsWithTls()
    {
        (int exitCode, string standardOutput, string standardError, ScriptedConnector connector) =
            await RunAsync("ldaps://127.0.0.1:18636/dc=example", 44300, BindSuccess, EntryOuX, SearchDone);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(OpenLdapEntry, standardOutput);
        StringAssert.Contains(standardError, "* LDAP local: ldaps://127.0.0.1:18636/dc=example" + Environment.NewLine);
        StringAssert.EndsWith(standardError, "* Connection #0 to host 127.0.0.1:18636 left intact" + Environment.NewLine);
        Assert.AreEqual(("127.0.0.1", 18636, true), connector.Targets.Select(target => (target.Host, target.Port, target.UseTls)).Single());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CreateRunner_OpenLdapVerboseLoginDenied_OnlyClosesTheConnectionAndExits67()
    {
        (int exitCode, string standardOutput, string standardError, _) =
            await RunAsync("ldap://127.0.0.1:18389/dc=example", 44298, InvalidCredentials1);

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                "*   Trying 127.0.0.1:18389...",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18389) from 127.0.0.1 port 44298 ",
                "* closing connection #0"),
            standardError);
    }

    [TestMethod]
    public async Task CreateRunner_Version_ListsLdapAndLdaps()
    {
        using MemoryStream standardOutput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, new MemoryStream(), new MemoryStream(), new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-V"]);

        string protocols = Encoding.ASCII.GetString(standardOutput.ToArray()).Split(Environment.NewLine).Single(line => line.StartsWith("Protocols:", StringComparison.Ordinal));
        Assert.AreEqual(CurlVersionText.ProtocolsLine, protocols);
        StringAssert.Contains(protocols, " ipns ldap ldaps mqtt ");
        Assert.AreEqual(0, exitCode);
    }

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + Environment.NewLine));

    private static string Hex(byte[] bytes) => string.Join(' ', bytes.Select(octet => octet.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));

    private static byte[] Bytes(string hex) => [.. hex.Split(' ').Select(pair => Convert.ToByte(pair, 16))];

    private static async Task<(int ExitCode, string StandardOutput, string StandardError, ScriptedConnector Connector)> RunAsync(
        string url, int localPort, params string[] replies)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        ScriptedConnector connector = new([.. replies.Select(Bytes)]);

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, new MemoryStream(), new ReportingConnector(connector, localPort), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-sv", "-u", "cn=u,dc=x:secret", url]);

        return (exitCode, Encoding.Latin1.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()), connector);
    }

    /// <summary>
    /// Reports the <c>Trying</c> and <c>Established connection</c> lines for each connect, as
    /// <c>TcpConnector</c> does, then connects through <paramref name="inner" />.
    /// </summary>
    private sealed class ReportingConnector(IConnector inner, int localPort) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            target.Events.ReportInfo($"  Trying {target.Host}:{target.Port}...");
            target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = target.Host,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, localPort),
                ConnectionNumber = 0,
            });
            return inner.ConnectAsync(target, cancellationToken);
        }
    }
}
