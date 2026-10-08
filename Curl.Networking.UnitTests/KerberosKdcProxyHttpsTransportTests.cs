using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="KerberosKdcProxyHttpsTransport" />: MIT's <c>POST /path HTTP/1.0</c> request
/// over a plain connection from the connector seam secured by the transport's own TLS client, the
/// <c>200</c> reply's body, and every other
/// outcome - another status, a reply with no blank line, one too long, a refused connection,
/// a proxy silent past the timeout - as an <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed partial class KerberosKdcProxyHttpsTransportTests
{
    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task PostAsync_ProxyAnswers200_SendsMitsRequestOverItsOwnTlsAndReturnsTheBody()
    {
        FakeConnector connector = new();
        connector.BytesToRead.Add(Reply("HTTP/1.1 200 OK\r\nContent-Type: application/kerberos\r\nContent-Length: 2\r\n\r\n", [0xAA, 0xBB]));
        KerberosKdcProxyHttpsTransport transport = OverPlaintext(connector, new ManualTimeProvider());

        byte[] reply = await PostAsync(transport, "kdcproxy.example.test", 443, "KdcProxy", new byte[] { 0x30, 0x01, 0x02 }, CancellationToken.None);

        Diagnostics.Diff("body", new byte[] { 0xAA, 0xBB }, reply);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, reply);
        Assert.AreEqual(new ConnectTarget("kdcproxy.example.test", 443, UseTls: false), connector.Targets.Single());
        byte[] expected = Reply(
            "POST /KdcProxy HTTP/1.0\r\n" +
            "Host: kdcproxy.example.test\r\n" +
            "Cache-Control: no-cache\r\n" +
            "Pragma: no-cache\r\n" +
            "User-Agent: kerberos/1.0\r\n" +
            "Content-type: application/kerberos\r\n" +
            "Content-Length: 3\r\n" +
            "\r\n",
            [0x30, 0x01, 0x02]);
        ScriptedConnection connection = connector.Opened.Single();
        Diagnostics.Bytes("request", connection.Written.ToArray());
        Diagnostics.Diff("request", expected, connection.Written.ToArray());
        Diagnostics.Assert("flush count", 1, connection.FlushCount);
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        CollectionAssert.AreEqual(expected, connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public void BuildRequest_EmptyPathAndIpv6Host_PostsToTheRootWithABracketedHost()
    {
        Diagnostics.Arrange("host", "2001:db8::1");
        Diagnostics.Arrange("path", "");
        Diagnostics.Arrange("body", "0 bytes");

        string request = Encoding.ASCII.GetString(KerberosKdcProxyHttpsTransport.BuildRequest("2001:db8::1", "", ReadOnlyMemory<byte>.Empty));

        Diagnostics.Act("request", request.ReplaceLineEndings("\\r\\n"));
        Diagnostics.Assert("starts with the root and a bracketed host", true, request.StartsWith("POST / HTTP/1.0\r\nHost: [2001:db8::1]\r\n", StringComparison.Ordinal));
        Diagnostics.Assert("ends with Content-Length 0", true, request.EndsWith("Content-Length: 0\r\n\r\n", StringComparison.Ordinal));
        StringAssert.StartsWith(request, "POST / HTTP/1.0\r\nHost: [2001:db8::1]\r\n");
        StringAssert.EndsWith(request, "Content-Length: 0\r\n\r\n");
    }

    [TestMethod]
    public async Task PostAsync_ProxyAnswers404_ThrowsIOExceptionNamingTheStatus()
    {
        FakeConnector connector = new();
        connector.BytesToRead.Add(Reply("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n", []));
        KerberosKdcProxyHttpsTransport transport = OverPlaintext(connector, new ManualTimeProvider());

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => PostAsync(transport, "kdcproxy.example.test", 443, "KdcProxy", new byte[] { 0x30 }, CancellationToken.None));

        Diagnostics.Diff("message", "The KDC proxy kdcproxy.example.test port 443 answered HTTP/1.1 404 Not Found.", failure.Message);
        Diagnostics.Assert("connection disposed", true, connector.Opened.Single().IsDisposed);
        Assert.AreEqual("The KDC proxy kdcproxy.example.test port 443 answered HTTP/1.1 404 Not Found.", failure.Message);
        Assert.IsTrue(connector.Opened.Single().IsDisposed);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200\r\n\r\n", true, DisplayName = "status line with no reason")]
    [DataRow("HTTP/1.0 200 OK\r\n\r\n", true, DisplayName = "HTTP/1.0")]
    [DataRow("HTTP/2 200 OK\r\n\r\n", false, DisplayName = "not HTTP/1.x")]
    [DataRow("HTTP/1.1\r\n\r\n", false, DisplayName = "no status code")]
    [DataRow("HTTP/1.1 2000 OK\r\n\r\n", false, DisplayName = "another code")]
    public void ReadBody_StatusLine_AcceptsOnlyHttp1With200(string head, bool accepted)
    {
        byte[] reply = Reply(head, [0x01]);
        Diagnostics.Bytes("reply", reply);
        Diagnostics.Arrange("accepted", accepted);

        if (accepted)
        {
            byte[] body = ReadBody(reply);
            Diagnostics.Diff("body", new byte[] { 0x01 }, body);
            CollectionAssert.AreEqual(new byte[] { 0x01 }, KerberosKdcProxyHttpsTransport.ReadBody("proxy", 443, reply));
        }
        else
        {
            IOException failure = Assert.ThrowsExactly<IOException>(() => ReadBody(reply));
            Diagnostics.Assert("exception", nameof(IOException), failure.GetType().Name);
        }
    }

    [TestMethod]
    public void ReadBody_NoBlankLine_ThrowsIOException()
    {
        Diagnostics.Arrange("reply", "HTTP/1.1 200 OK\\r\\n with no blank line");

        IOException failure = Assert.ThrowsExactly<IOException>(() => ReadBody(Reply("HTTP/1.1 200 OK\r\n", [])));

        Diagnostics.Diff("message", "The KDC proxy proxy port 443 sent no complete HTTP reply.", failure.Message);
        Assert.AreEqual("The KDC proxy proxy port 443 sent no complete HTTP reply.", failure.Message);
    }

    [TestMethod]
    public async Task PostAsync_ReplyLongerThanTheLimit_ThrowsIOException()
    {
        FakeConnector connector = new();
        connector.BytesToRead.Add(new byte[KerberosKdcProxyHttpsTransport.MaximumReplyLength + 1]);
        KerberosKdcProxyHttpsTransport transport = OverPlaintext(connector, new ManualTimeProvider());

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => PostAsync(transport, "proxy", 443, "", new byte[] { 0x30 }, CancellationToken.None));

        Diagnostics.Diff("message", "The KDC proxy proxy port 443 sent a reply longer than 1048576 bytes.", failure.Message);
        Assert.AreEqual("The KDC proxy proxy port 443 sent a reply longer than 1048576 bytes.", failure.Message);
    }

    [TestMethod]
    public async Task PostAsync_ProxyRefusesOrFailsTls_ThrowsIOExceptionWithTheConnectorsMessage()
    {
        FakeConnector connector = new() { Failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "TLS connect error") };
        KerberosKdcProxyHttpsTransport transport = OverPlaintext(connector, new ManualTimeProvider());

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => PostAsync(transport, "proxy", 443, "", new byte[] { 0x30 }, CancellationToken.None));

        Diagnostics.Diff("message", "TLS connect error", failure.Message);
        Assert.AreEqual("TLS connect error", failure.Message);
    }

    [TestMethod]
    public async Task PostAsync_ProxySilentPastTheTimeout_ThrowsIOException()
    {
        ManualTimeProvider time = new();
        StallingConnection connection = new() { OnStalled = () => time.Advance(10000) };
        KerberosKdcProxyHttpsTransport transport = OverPlaintext(new SingleConnectionConnector(connection), time);

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => PostAsync(transport, "proxy", 443, "", new byte[] { 0x30 }, CancellationToken.None));

        Diagnostics.Diff("message", "The KDC proxy proxy port 443 did not answer within 10 s.", failure.Message);
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual("The KDC proxy proxy port 443 did not answer within 10 s.", failure.Message);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task PostAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cancel = new();
        StallingConnection connection = new() { OnStalled = cancel.Cancel };
        KerberosKdcProxyHttpsTransport transport = OverPlaintext(new SingleConnectionConnector(connection), new ManualTimeProvider());

        var failure = await Assert.ThrowsAsync<OperationCanceledException>(() => PostAsync(transport, "proxy", 443, "", new byte[] { 0x30 }, cancel.Token));

        Diagnostics.Assert("cancelled", true, failure is OperationCanceledException);
    }

    private async Task<byte[]> PostAsync(KerberosKdcProxyHttpsTransport transport, string host, int port, string path, byte[] body, CancellationToken cancellationToken)
    {
        Diagnostics.Arrange("proxy", $"{host} port {port}");
        Diagnostics.Arrange("path", path);
        Diagnostics.Bytes("KDC request", body);
        try
        {
            byte[] reply;
            using (Diagnostics.Phase("post"))
            {
                reply = await transport.PostAsync(host, port, path, body, cancellationToken);
            }

            Diagnostics.Bytes("reply body", reply);
            Diagnostics.Act("reply body length", reply.Length);
            return reply;
        }
        catch (Exception exception) when (WriteFailure(exception))
        {
            throw;
        }
    }

    private byte[] ReadBody(byte[] reply)
    {
        try
        {
            byte[] body = KerberosKdcProxyHttpsTransport.ReadBody("proxy", 443, reply);
            Diagnostics.Act("body length", body.Length);
            return body;
        }
        catch (Exception exception) when (WriteFailure(exception))
        {
            throw;
        }
    }

    private bool WriteFailure(Exception exception)
    {
        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        return false;
    }

    private static byte[] Reply(string head, byte[] body) => [.. Encoding.ASCII.GetBytes(head), .. body];

    private static KerberosKdcProxyHttpsTransport OverPlaintext(IConnector connector, TimeProvider time) =>
        new(connector, TimeSpan.FromSeconds(10), time, new PassThroughTlsClient(), _ => null);

    /// <summary>Hands the plaintext stream back unsecured, so a scripted connection's bytes are the exchange.</summary>
    private sealed class PassThroughTlsClient : IKerberosKdcProxyTlsClient
    {
        public Task<Stream> AuthenticateAsync(Stream plaintext, string host, System.Security.Cryptography.X509Certificates.X509Certificate2Collection? anchors, CancellationToken cancellationToken) =>
            Task.FromResult(plaintext);
    }

    private sealed class SingleConnectionConnector(IConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connection));
    }
}
