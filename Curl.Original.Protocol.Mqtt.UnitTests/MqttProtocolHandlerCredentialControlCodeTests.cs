using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Mqtt;

/// <summary>
/// Pins that an <c>mqtt://</c> user name or password holding a byte below 0x20 is refused as
/// curl 8.22.0 refuses it, measured on 2026-10-13 (BL-2179): <c>curl: (43) control code detected
/// in credentials</c>, before any connection is made, so no CONNECT is sent.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerCredentialControlCodeTests
{
    [TestMethod]
    [DataRow("a\tb", "p", DisplayName = "user name with a tab")]
    [DataRow("u", "p\r\nq", DisplayName = "password with CR LF")]
    [DataRow("u", "p\u001Fq", DisplayName = "password with 0x1F")]
    public async Task ExecuteAsync_CredentialsWithControlByte_FailsWithExit43WithoutConnecting(string user, string password)
    {
        var connector = new CountingConnector();

        TransferResult result = await new MqttProtocolHandler(connector).ExecuteAsync(Context(user, password));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadFunctionArgument, "control code detected in credentials"), result with { Report = null });
        Assert.AreEqual(0, connector.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsWithoutControlByte_Connects()
    {
        var connector = new CountingConnector();

        await new MqttProtocolHandler(connector).ExecuteAsync(Context("u ~", "p !"));

        Assert.AreEqual(1, connector.Calls);
    }

    private static TransferContext Context(string user, string password) => new()
    {
        Url = CurlUrl.Parse("mqtt://127.0.0.1/t"),
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
