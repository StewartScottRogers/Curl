using System.Text;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class InProcessCurlCommandRunnerTests
{
    [TestMethod]
    public async Task RunAsync_Version_ExitsZeroAndWritesCurlsVersionLine()
    {
        using MemoryStream standardInput = new();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["--version"], standardInput, standardOutput, standardError, null, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.StartsWith("curl ", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_WorkingDirectoryIsTheCurrentDirectory_Runs()
    {
        using MemoryStream standardOutput = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["--version"], new MemoryStream(), standardOutput, new MemoryStream(), Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void RunAsync_WorkingDirectoryIsNotTheCurrentDirectory_RefusesToRun()
    {
        string elsewhere = Path.Combine(Directory.GetCurrentDirectory(), "elsewhere");

        Assert.ThrowsExactly<InvalidOperationException>(() => new InProcessCurlCommandRunner().RunAsync(["--version"], new MemoryStream(), new MemoryStream(), new MemoryStream(), elsewhere, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("-o", "nosuch-folder-bl5761/msg#1.eml", "pop3://127.0.0.1:1/[1-3]")]
    [DataRow("--no-such-option-bl5761", "http://127.0.0.1:1/", "-s")]
    [DataRow("-S", "http://", "-s")]
    public async Task RunAsync_FailingArguments_GiveAnExitCodeWithoutThrowing(string first, string second, string third)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync([first, second, third], new MemoryStream(), standardOutput, standardError, null, CancellationToken.None);

        Assert.AreNotEqual(0, exitCode);
        Assert.IsGreaterThan(0, standardError.Length);
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task RunAsync_GlobbedOutputIntoAMissingFolderAfterAConnection_GivesCurlsExitCodeWithoutThrowing()
    {
        using System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        Task server = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using System.Net.Sockets.TcpClient client = await listener.AcceptTcpClientAsync();
                    Stream stream = client.GetStream();
                    byte[] buffer = new byte[4096];
                    _ = await stream.ReadAsync(buffer);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nhi"));
                }
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ObjectDisposedException or InvalidOperationException)
            {
            }
        });
        using MemoryStream standardError = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["-sS", "-o", "nosuch-folder-bl5761/msg#1.eml", $"http://127.0.0.1:{port}/[1-3]"], new MemoryStream(), new MemoryStream(), standardError, null, CancellationToken.None);

        Assert.AreEqual(23, exitCode);
        Assert.IsGreaterThan(0, standardError.Length);
        listener.Stop();
        await server;
    }
}
