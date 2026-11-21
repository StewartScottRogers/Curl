using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    [DataRow(UseSsl.None, TransportSecurityLevel.None, false, false)]
    [DataRow(UseSsl.Try, TransportSecurityLevel.Try, false, false)]
    [DataRow(UseSsl.Control, TransportSecurityLevel.Required, true, false)]
    [DataRow(UseSsl.All, TransportSecurityLevel.Required, false, true)]
    public void TransferSettingsOf_UseSsl_MapsToTheLevelTheConsoleGivesItsSslOptions(UseSsl useSsl, TransportSecurityLevel level, bool controlOnly, bool required)
    {
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions { Network = new EasyNetworkOptions { UseSsl = useSsl } });

        Assert.AreEqual(level, settings.SslLevel);
        Assert.AreEqual(controlOnly, settings.FtpSslControlOnly);
        Assert.AreEqual(required, settings.SslRequired);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1/file", "220 ready\r\n|534 no ssl\r\n|534 no tls\r\n", "AUTH SSL")]
    [DataRow("imap://127.0.0.1/INBOX", "* OK ready\r\n* CAPABILITY IMAP4rev1 AUTH=PLAIN\r\nA001 OK done\r\n", "A001 CAPABILITY")]
    public async Task PerformAsync_UseSslAllAndTheServerOffersNoTls_FailsWithUseSslFailedAndSendsNoCredentials(string url, string reply, string firstCommand)
    {
        ScriptedConnector connector = new(reply.Split("|").Select(Encoding.ASCII.GetBytes).ToArray());
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url, UseSsl = UseSsl.All },
            Authentication = new Curl.Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "user:secret" },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(CurlCode.UseSslFailed, code);
        Assert.IsTrue(written.Contains(firstCommand, StringComparison.Ordinal), written);
        Assert.IsFalse(written.Contains("secret", StringComparison.Ordinal), written);
        Assert.IsFalse(written.Contains("USER", StringComparison.Ordinal), written);
        Assert.IsFalse(written.Contains("LOGIN", StringComparison.Ordinal), written);
    }
}
