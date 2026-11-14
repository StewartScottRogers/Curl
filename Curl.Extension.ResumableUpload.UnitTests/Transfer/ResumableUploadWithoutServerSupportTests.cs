using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ResumableUpload.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Pins what a resumable upload does when the server creates no upload resource (EXT-045, ADR-1103).</summary>
[TestClass]
public sealed class ResumableUploadWithoutServerSupportTests
{
    private const string PlainOptions = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";
    private const string TusOptions = "HTTP/1.1 204 No Content\r\nTus-Version: 1.0.0\r\nTus-Extension: creation\r\n\r\n";
    private const string CreatedWithoutLocation = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    [DataRow(PlainOptions)]
    [DataRow(TusOptions)]
    public async Task ExecuteAsync_CreationAnsweredWithoutLocation_FailsAndSendsNoBodyAndReportsNoUpload(string options)
    {
        var connection = new RecordedConnection(options + CreatedWithoutLocation);
        var context = Context();

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "did not create an upload resource");
        Assert.IsNull(result.Report);
        Assert.IsFalse(connection.Written.Contains("hello", StringComparison.Ordinal));
        Assert.IsFalse(connection.Written.Contains("PATCH", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ExecuteAsync_CreationAnsweredWithLocation_StillAppendsTheBody()
    {
        var connection = new RecordedConnection(PlainOptions + "HTTP/1.1 201 Created\r\nLocation: /uploads/7\r\nContent-Length: 0\r\n\r\n" + "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ndone");
        var context = Context();

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.Report!.UploadSize);
        StringAssert.EndsWith(connection.Written, "hello");
        StringAssert.Contains(connection.Written, "PATCH /uploads/7 ");
    }

    private static ValueTask<TransferResult> NeverSent(ITransferContext context) => throw new AssertFailedException("The handler ran.");

    private static TransferHookServices Services(IConnector connector) => new(connector, new UnusedListener());

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("http://up.example.com/upload"),
        Output = new MemoryStream(),
        Upload = new MemoryStream(Encoding.ASCII.GetBytes("hello")),
    };

    private sealed class UnusedListener : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The listener was used.");
    }
}
