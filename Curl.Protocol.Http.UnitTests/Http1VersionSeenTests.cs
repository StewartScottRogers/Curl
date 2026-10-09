using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="Http1VersionSeen" />: the HTTP/1.0 downgrade a connection keeps after an
/// HTTP/1.0 reply (upstream test1074) and the exit 8 a reused connection gives a reply naming
/// another major version (test471), as curl 8.21.0 keeps <c>conn->httpversion_seen</c>.
/// </summary>
[TestClass]
public sealed class Http1VersionSeenTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void DowngradesToHttp10_AfterAnHttp10Reply_IsTrue()
    {
        SessionHoldingConnection connection = NewConnection();
        Diagnostics.Arrange("recorded version", HttpVersion.Version10);

        Http1VersionSeen.Record(connection, HttpVersion.Version10);
        bool downgrades = Http1VersionSeen.DowngradesToHttp10(connection);

        Diagnostics.Act("downgrades", downgrades);
        Diagnostics.Assert("downgrades", true, downgrades);
        Assert.IsTrue(downgrades);
    }

    [TestMethod]
    public void DowngradesToHttp10_AfterAnHttp11ReplyThenAnHttp10Reply_IsTrue()
    {
        SessionHoldingConnection connection = NewConnection();
        Http1VersionSeen.Record(connection, HttpVersion.Version11);
        Diagnostics.Arrange("first recorded version", HttpVersion.Version11);

        Http1VersionSeen.Record(connection, HttpVersion.Version10);
        bool downgrades = Http1VersionSeen.DowngradesToHttp10(connection);

        Diagnostics.Act("downgrades", downgrades);
        Diagnostics.Assert("downgrades", true, downgrades);
        Assert.IsTrue(downgrades);
    }

    [TestMethod]
    public void DowngradesToHttp10_OnAConnectionWithNoReply_IsFalse()
    {
        SessionHoldingConnection connection = NewConnection();
        Diagnostics.Arrange("recorded version", "none");

        bool downgrades = Http1VersionSeen.DowngradesToHttp10(connection);

        Diagnostics.Act("downgrades", downgrades);
        Diagnostics.Assert("downgrades", false, downgrades);
        Assert.IsFalse(downgrades);
    }

    [TestMethod]
    public void Record_AnHttp2Reply_HoldsNothing()
    {
        SessionHoldingConnection connection = NewConnection();
        Diagnostics.Arrange("recorded version", HttpVersion.Version20);

        Http1VersionSeen.Record(connection, HttpVersion.Version20);

        Diagnostics.Act("held session", connection.Session);
        Diagnostics.Assert("held session", null, connection.Session);
        Assert.IsNull(connection.Session);
    }

    [TestMethod]
    public void Record_OnAConnectionHoldingAnotherSession_KeepsThatSession()
    {
        SessionHoldingConnection connection = NewConnection();
        IConnectionSession other = new OtherSession();
        connection.TryHoldSession(other);
        Diagnostics.Arrange("held session", other);

        Http1VersionSeen.Record(connection, HttpVersion.Version10);

        Diagnostics.Act("held session", connection.Session);
        Diagnostics.Assert("held session", other, connection.Session);
        Assert.AreSame(other, connection.Session);
    }

    [TestMethod]
    public void ThrowIfMismatched_Http2AfterHttp11_FailsWithExit8()
    {
        SessionHoldingConnection connection = NewConnection();
        Http1VersionSeen.Record(connection, HttpVersion.Version11);
        Diagnostics.Arrange("recorded version", HttpVersion.Version11);

        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(
            () => Http1VersionSeen.ThrowIfMismatched(connection, HttpVersion.Version20));

        Diagnostics.Act("exit code", failure.ExitCode);
        Diagnostics.Act("message", failure.Message);
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("Version mismatch (from HTTP/1 to HTTP/2)", failure.Message);
    }

    [TestMethod]
    public void ThrowIfMismatched_Http10AfterHttp11_DoesNotFail()
    {
        SessionHoldingConnection connection = NewConnection();
        Http1VersionSeen.Record(connection, HttpVersion.Version11);
        Diagnostics.Arrange("recorded version", HttpVersion.Version11);

        Http1VersionSeen.ThrowIfMismatched(connection, HttpVersion.Version10);

        Diagnostics.Act("checked version", HttpVersion.Version10);
        Diagnostics.Assert("downgrades", false, Http1VersionSeen.DowngradesToHttp10(connection));
        Assert.IsFalse(Http1VersionSeen.DowngradesToHttp10(connection));
    }

    [TestMethod]
    [DataRow("HTTP/2 200 OK\r\n", "2.0", DisplayName = "HTTP/2")]
    [DataRow("HTTP/3 200 OK\r\n", "3.0", DisplayName = "HTTP/3")]
    [DataRow("HTTP/1.1 200 OK\r\n", "1.0", DisplayName = "HTTP/1.x keeps the parsed version")]
    public void VersionNamed_HeadBytes_GivesTheVersionTheStatusLineNames(string head, string expected)
    {
        Diagnostics.Arrange("head", head);

        Version version = Http1VersionSeen.VersionNamed(Encoding.ASCII.GetBytes(head), HttpVersion.Version10);

        Diagnostics.Act("version", version);
        Diagnostics.Assert("version", Version.Parse(expected), version);
        Assert.AreEqual(Version.Parse(expected), version);
    }

    [TestMethod]
    public async Task ShutDownAsync_Always_Completes()
    {
        Http1VersionSeen seen = new() { Version = HttpVersion.Version11 };
        Diagnostics.Arrange("version", seen.Version);

        await seen.ShutDownAsync(TestContext.CancellationToken);

        Diagnostics.Act("shut down", true);
        Diagnostics.Assert("version kept", HttpVersion.Version11, seen.Version);
        Assert.AreEqual(HttpVersion.Version11, seen.Version);
    }

    private static SessionHoldingConnection NewConnection() => new(new ScriptedConnection([], chunkSize: 1));

    private sealed class OtherSession : IConnectionSession
    {
        public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
