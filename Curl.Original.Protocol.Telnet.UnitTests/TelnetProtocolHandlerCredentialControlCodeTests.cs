using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Telnet;

/// <summary>
/// Pins that a <c>telnet://</c> user name or password holding a byte below 0x20 is refused as
/// curl 8.22.0 refuses it, measured on 2026-10-13 (BL-2179): <c>curl: (43) control code detected
/// in credentials</c>, before any connection is made, so nothing is sent.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerCredentialControlCodeTests
{
    [TestMethod]
    [DataRow("a\tb", "p", DisplayName = "user name with a tab")]
    [DataRow("u", "p\r\nq", DisplayName = "password with CR LF")]
    [DataRow("u", "p\u001Fq", DisplayName = "password with 0x1F")]
    public async Task ExecuteAsync_CredentialsWithControlByte_FailsWithExit43WithoutConnecting(string user, string password)
    {
        var connector = new CountingConnector();

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(Context(user, password));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadFunctionArgument, "control code detected in credentials"), result);
        Assert.AreEqual(0, connector.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsWithoutControlByte_Connects()
    {
        var connector = new CountingConnector();

        await new TelnetProtocolHandler(connector).ExecuteAsync(Context("u ~", "p !"));

        Assert.AreEqual(1, connector.Calls);
    }

    private static TransferContext Context(string user, string password) => new()
    {
        Url = CurlUrl.Parse("telnet://127.0.0.1/"),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential(user, password),
    };

    private sealed class CountingConnector : IConnector
    {
        public int Calls { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
        }
    }
}
