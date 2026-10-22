using System.Net;
using System.Net.Sockets;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--doh-url</c> on the dialing runner (BL-2928): the DoH POSTs dial through the runner's
/// <see cref="ITcpDialer" />, the DoH server's name resolving through the runner's resolver, so the
/// conformance harness's in-memory servers answer them; without it the runner's resolver answers every name.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDialingDohTests
{
    [TestMethod]
    public async Task RunAsync_WithADohUrl_DialsTheDohServerThroughTheGivenDialerAndResolver()
    {
        RecordingRefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dialer, "-s", "--doh-url", "http://doh.example:47115/dns-query", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntResolveHost, exitCode);
        Assert.IsNotEmpty(dialer.Dialled);
        Assert.IsTrue(dialer.Dialled.TrueForAll(endPoint => endPoint.Equals(new IPEndPoint(IPAddress.Parse("10.0.0.9"), 47115))));
    }

    [TestMethod]
    public async Task RunAsync_WithoutADohUrl_DialsTheAddressTheGivenResolverAnswers()
    {
        RecordingRefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dialer, "-s", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("10.0.0.9"), 47114) }, dialer.Dialled);
    }

    private static async Task<int> RunAsync(RecordingRefusingTcpDialer dialer, params string[] arguments) =>
        await CurlComposition.CreateRunner(
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                dialer,
                new FixedDnsResolver(IPAddress.Parse("10.0.0.9")),
                new NoDatagramConnector())
            .RunAsync(arguments);

    /// <summary>A resolver answering every name with one address.</summary>
    private sealed class FixedDnsResolver(IPAddress address) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([address]);
    }

    /// <summary>A datagram connector no test reaches.</summary>
    private sealed class NoDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No datagram channel is opened.");
    }

    /// <summary>A dialer that records each address and refuses it.</summary>
    private sealed class RecordingRefusingTcpDialer : ITcpDialer
    {
        public List<IPEndPoint> Dialled { get; } = [];

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            lock (Dialled)
            {
                Dialled.Add(endPoint);
            }

            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }
}
