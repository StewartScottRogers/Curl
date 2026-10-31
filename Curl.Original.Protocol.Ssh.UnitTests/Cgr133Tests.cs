using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins CGR-133 (BL-3662): an <c>sftp://</c> or <c>scp://</c> download whose output refuses
/// its first write (an <c>-o</c> file that cannot be created) ends with exit 23,
/// <c>Failure writing output to destination, passed N returned M</c>, as a failed result
/// rather than an escaping <see cref="IOException" />, and the session still ends with
/// <c>DISCONNECT</c> 11, as curl 8.22.0 ends it; the curl tool then words it as
/// <c>client returned ERROR on write of N bytes</c> and still prints <c>-w</c>.
/// </summary>
[TestClass]
public sealed class Cgr133Tests
{
    private const string User = "tester";

    private const string Password = "secret";

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task ExecuteAsync_OutputRefusesTheWrite_IsExit23AndTheSessionEndsWithDisconnect(string scheme)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();

        TransferResult result = await RunAsync(server, $"{scheme}://files.example/data/hello.txt", new RefusingOutput(new IOException("Could not create the output file nodir/x.txt.")));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 11 returned 0", result.ErrorMessage);
        CollectionAssert.Contains(server.Events.ToArray(), "disconnect 11 Shutdown");
    }

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task ExecuteAsync_OutputAcceptsPartOfTheWrite_ReportsTheBytesAccepted(string scheme)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();

        TransferResult result = await RunAsync(server, $"{scheme}://files.example/data/hello.txt", new RefusingOutput(new OutputWriteFailedException(4, "short write")));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 11 returned 4", result.ErrorMessage);
    }

    private static async Task<TransferResult> RunAsync(InMemorySshServer server, string url, Stream output)
    {
        SshProtocolHandler handler = new(
            server,
            new InMemoryKeyFileSystem(new Dictionary<string, string>()),
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent());
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Credentials = new NetworkCredential(User, Password),
        });
        await server.WhenSessionsEndAsync();
        return result;
    }

    // An output whose every non-empty write fails with the exception given, as an -o file
    // that cannot be created fails its first write.
    private sealed class RefusingOutput(IOException failure) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw failure;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            buffer.IsEmpty ? ValueTask.CompletedTask : ValueTask.FromException(failure);
    }
}
