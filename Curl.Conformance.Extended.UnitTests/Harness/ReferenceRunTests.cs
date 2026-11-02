using System.Net;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Harness;

[TestClass]
public sealed class ReferenceRunTests
{
    private const int Port = 1883;

    [TestMethod]
    public async Task EndAsync_ServerHasNotYetReadCurlsLastBytes_DrainsThemBeforeStopping()
    {
        using ReferenceRun run = new();
        LateReadingServer server = new();
        ReferenceTcpDialer dialer = new([ReferenceRoute.Tcp(Port, server)], run);
        var dialed = await dialer.DialAsync(new IPEndPoint(IPAddress.Loopback, Port), CancellationToken.None);
        await dialed.Connection.WriteAsync("last"u8.ToArray(), CancellationToken.None);

        server.StartReading.SetResult();
        IReadOnlyList<WireExchange> exchanges = await run.EndAsync();

        CollectionAssert.AreEqual("last"u8.ToArray(), exchanges.Single().ClientBytes);
        Assert.IsTrue(server.ReachedTheEndOfTheStream);
    }

    // Waits to be told before reading, as a server busy with an earlier packet does, then reads to the end.
    private sealed class LateReadingServer : IReferenceServer
    {
        public TaskCompletionSource StartReading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ReachedTheEndOfTheStream { get; private set; }

        public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
        {
            await StartReading.Task;
            byte[] buffer = new byte[16];
            while (await connection.ReadAsync(buffer, cancellationToken) > 0)
            {
            }

            ReachedTheEndOfTheStream = true;
        }
    }
}
