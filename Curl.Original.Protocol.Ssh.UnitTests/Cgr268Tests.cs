using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;
using Curl.Ssh.Fakes;
using static Curl.Ssh.Fakes.SshTestEncoding;

namespace Curl.Original.Protocol.Ssh.Sftp;

/// <summary>
/// Pins CGR-268 (BL-3797): an sftp <c>-Q pwd</c> shows <c>&gt; PWD</c> and
/// <c>&lt; 257 "path" is current directory.</c> under <c>-v</c>, as curl 8.22.0 does, because
/// curl reports the command and its answer as a request and a response header.
/// </summary>
[TestClass]
public sealed class Cgr268Tests
{
    [TestMethod]
    public async Task RunBeforeTransferAsync_Pwd_ReportsTheCommandAndItsAnswerAsHeaders()
    {
        ScriptedConnection connection = new(SftpServerScript.Started().Bytes);
        SftpSession session = await SftpSession.StartAsync(SftpSessionTests.Transport(connection), CancellationToken.None);
        HeaderRecorder events = new();

        await new SftpQuoteCommands(["pwd"], [], null, cLongIs32Bits: true, events).RunBeforeTransferAsync(session, Encoding.UTF8.GetBytes(SftpServerScript.Home), "/p"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "> PWD\n", "< 257 \"/p\" is current directory.\n" }, events.Lines);
    }

    private sealed class HeaderRecorder : ITransferEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string text)
        {
        }

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Lines.Add("> " + Encoding.ASCII.GetString(bytes));

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Lines.Add("< " + Encoding.ASCII.GetString(bytes));

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
