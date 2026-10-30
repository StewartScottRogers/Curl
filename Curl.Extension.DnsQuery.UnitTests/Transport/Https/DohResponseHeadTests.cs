using Curl.Extension.DnsQuery.Client;
using Curl.Extension.DnsQuery.Transport.Https;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Transport.Https;

/// <summary>Checks the DoH response's status line and headers (RFC 8484 section 4.2.1).</summary>
[TestClass]
public sealed class DohResponseHeadTests
{
    [TestMethod]
    public void Parse_Headers_ReadsContentTypeLengthAndChunkedInAnyCaseIgnoringOthers()
    {
        DohResponseHead head = DohResponseHead.Parse(["HTTP/1.1 200 OK", "content-TYPE:  application/dns-message ", "Content-Length: 42", "Transfer-Encoding: Chunked", "Server: x"]);

        Assert.AreEqual(new DohResponseHead(200, "application/dns-message", 42, true), head);
        Assert.IsFalse(head.IsInterim);
    }

    [TestMethod]
    public void Parse_TransferEncodingNotChunked_IsNotChunked() =>
        Assert.IsFalse(DohResponseHead.Parse(["HTTP/1.0 200", "Transfer-Encoding: identity"]).IsChunked);

    [TestMethod]
    [DataRow("HTTP/1.1 100 Continue", true)]
    [DataRow("HTTP/1.1 199 Other", true)]
    [DataRow("HTTP/1.1 099 Low", false)]
    [DataRow("HTTP/1.1 204 No Content", false)]
    public void IsInterim_StatusCode_IsTrueOnlyFor1xx(string statusLine, bool interim) =>
        Assert.AreEqual(interim, DohResponseHead.Parse([statusLine]).IsInterim);

    [TestMethod]
    [DataRow("HTTP/1.1")]
    [DataRow("HTTP/2 200 OK")]
    [DataRow("HTTP/1.1 2000 OK")]
    [DataRow("HTTP/1.1 2x0 OK")]
    public void Parse_MalformedStatusLine_FailsWithExit8(string statusLine)
    {
        DnsQueryException failure = Assert.ThrowsExactly<DnsQueryException>(() => DohResponseHead.Parse([statusLine]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    [DataRow("no colon here")]
    [DataRow(": no name")]
    [DataRow("Content-Length: -1")]
    [DataRow("Content-Length: 65536")]
    [DataRow("Content-Length: ten")]
    public void Parse_MalformedHeader_FailsWithExit8(string header)
    {
        DnsQueryException failure = Assert.ThrowsExactly<DnsQueryException>(() => DohResponseHead.Parse(["HTTP/1.1 200 OK", header]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    [DataRow("application/dns-message")]
    [DataRow("Application/DNS-Message; charset=binary")]
    public void EnsureDnsMessage_Rfc8484Section4_2_1_200WithTheDnsMessageType_Passes(string contentType) =>
        new DohResponseHead(200, contentType, null, false).EnsureDnsMessage();

    [TestMethod]
    public void EnsureDnsMessage_Rfc8484Section4_2_1_StatusOtherThan200_FailsWithExit22()
    {
        DnsQueryException failure = Assert.ThrowsExactly<DnsQueryException>(() => new DohResponseHead(415, DohRequest.MediaType, null, false).EnsureDnsMessage());

        Assert.AreEqual(CurlExitCode.HttpReturnedError, failure.ExitCode);
        Assert.AreEqual("The DoH server answered HTTP 415", failure.Message);
    }

    [TestMethod]
    [DataRow("text/html")]
    [DataRow(null)]
    public void EnsureDnsMessage_Rfc8484Section4_2_1_OtherContentType_FailsWithExit8(string? contentType)
    {
        DnsQueryException failure = Assert.ThrowsExactly<DnsQueryException>(() => new DohResponseHead(200, contentType, null, false).EnsureDnsMessage());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual($"The DoH server answered with content type \"{contentType}\", not application/dns-message", failure.Message);
    }
}
