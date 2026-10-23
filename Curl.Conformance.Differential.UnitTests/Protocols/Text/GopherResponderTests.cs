using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Text;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Text;

[TestClass]
public sealed class GopherResponderTests
{
    [TestMethod]
    [DataRow("/sel\r\nignored", "b'/sel\\r\\n'")]
    [DataRow("\r\n", "b'\\r\\n'")]
    [DataRow("no newline", "b'no newline'")]
    [DataRow("", "b''")]
    public async Task RespondAsync_Selector_LogsTheReprOfTheFirstLineAndSendsTheMenu(string input, string logged)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new GopherResponder(log).RespondAsync(connection, CancellationToken.None);

        Assert.AreEqual("iHello gopher\tfake\t(NULL)\t0\r\n.\r\n", Encoding.Latin1.GetString(connection.Written));
        Assert.AreEqual(logged, Encoding.UTF8.GetString(log.Take()));
    }
}
