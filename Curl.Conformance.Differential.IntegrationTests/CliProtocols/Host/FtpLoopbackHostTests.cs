using System.Net.Sockets;
using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Ftp;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols.Host;

/// <summary>Drives the FTP responder over real loopback sockets, passive data connection included.</summary>
[TestClass]
[TestCategory("Integration")]
public sealed class FtpLoopbackHostTests
{
    private const int FtpPort = 18200;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Retr_OverLoopback_SendsTheFileAndLogsTheCommands()
    {
        var log = new ProtocolWireLog();
        var responder = new FtpResponder(log, new LoopbackPassiveDataListenerOpener());
        await using var host = ProtocolLoopbackHost.Start(FtpPort, log, responder.RespondAsync);

        using var control = new TcpClient();
        await control.ConnectAsync("127.0.0.1", host.Port);
        using var reader = new StreamReader(control.GetStream(), Encoding.Latin1);
        await using var writer = new StreamWriter(control.GetStream(), Encoding.Latin1) { NewLine = "\r\n", AutoFlush = true };
        Assert.AreEqual("220 mock ftp", await reader.ReadLineAsync());
        await writer.WriteLineAsync("EPSV");
        var epsv = await reader.ReadLineAsync() ?? string.Empty;
        var port = int.Parse(epsv[(epsv.IndexOf("|||", StringComparison.Ordinal) + 3)..^2], System.Globalization.CultureInfo.InvariantCulture);
        await writer.WriteLineAsync("RETR a.txt");
        using var data = new TcpClient();
        await data.ConnectAsync("127.0.0.1", port);
        using var dataReader = new StreamReader(data.GetStream(), Encoding.Latin1);

        Assert.AreEqual("alpha file\n", await dataReader.ReadToEndAsync());
        Assert.AreEqual("150 opening", await reader.ReadLineAsync());
        Assert.AreEqual("226 done", await reader.ReadLineAsync());
        await writer.WriteLineAsync("QUIT");
        Assert.AreEqual("221 bye", await reader.ReadLineAsync());
        Assert.AreEqual("EPSV\nRETR a.txt\nQUIT", Encoding.UTF8.GetString(log.Take()));
    }
}
