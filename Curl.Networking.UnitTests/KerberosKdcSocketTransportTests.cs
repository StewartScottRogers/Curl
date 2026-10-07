using System.Buffers.Binary;
using System.Net;
using Curl.Kerberos;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="KerberosKdcSocketTransport" />: a UDP exchange through the datagram seam with
/// its reply timeout, a TCP stream through the connector seam that disposes its connection,
/// every failure to reach the KDC as an <see cref="IOException" />, and, driven by
/// <c>Curl.Kerberos</c>'s KDC client, the switch from UDP to TCP on
/// <c>KRB_ERR_RESPONSE_TOO_BIG</c> with RFC 4120 section 7.2.2's length-prefixed framing.
/// </summary>
[TestClass]
public sealed class KerberosKdcSocketTransportTests
{
    private const string Realm = "EXAMPLE.TEST";

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExchangeDatagramAsync_KdcAnswers_SendsToTheServerAndReturnsTheReply()
    {
        DatagramKdc kdc = new([0xAA, 0xBB]);
        KerberosKdcSocketTransport transport = new(kdc, new FakeConnector(), TimeSpan.FromSeconds(1), new ManualTimeProvider());

        byte[] reply = await ExchangeDatagramAsync(transport, "kdc.example.test", 88, new byte[] { 0x01, 0x02 }, CancellationToken.None);

        Diagnostics.Diff("reply", new byte[] { 0xAA, 0xBB }, reply);
        Diagnostics.Assert("opened", "kdc.example.test:88", string.Join(", ", kdc.Opened));
        Diagnostics.Diff("sent", new byte[] { 0x01, 0x02 }, kdc.Channel.Sent.Single());
        Diagnostics.Assert("channel disposed", true, kdc.Channel.IsDisposed);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, reply);
        Assert.AreEqual("kdc.example.test:88", kdc.Opened.Single());
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, kdc.Channel.Sent.Single());
        Assert.IsTrue(kdc.Channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExchangeDatagramAsync_KdcSilentPastTheTimeout_ThrowsIOException()
    {
        DatagramKdc kdc = new(reply: null);
        ManualTimeProvider time = new();
        KerberosKdcSocketTransport transport = new(kdc, new FakeConnector(), TimeSpan.FromSeconds(1), time);

        Task<byte[]> exchange = ExchangeDatagramAsync(transport, "kdc.example.test", 88, new byte[] { 0x01 }, CancellationToken.None);
        time.Advance(1000);

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => exchange);
        Diagnostics.Diff("message", "The KDC kdc.example.test port 88 did not answer within 1 s.", failure.Message);
        Diagnostics.Assert("channel disposed", true, kdc.Channel.IsDisposed);
        Assert.AreEqual("The KDC kdc.example.test port 88 did not answer within 1 s.", failure.Message);
        Assert.IsTrue(kdc.Channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExchangeDatagramAsync_CallerCancels_ThrowsOperationCanceled()
    {
        DatagramKdc kdc = new(reply: null);
        KerberosKdcSocketTransport transport = new(kdc, new FakeConnector(), TimeSpan.FromSeconds(1), new ManualTimeProvider());
        using CancellationTokenSource cancel = new();

        Task<byte[]> exchange = ExchangeDatagramAsync(transport, "kdc.example.test", 88, new byte[] { 0x01 }, cancel.Token);
        await cancel.CancelAsync();

        var failure = await Assert.ThrowsAsync<OperationCanceledException>(() => exchange);

        Diagnostics.Assert("cancelled", true, failure is OperationCanceledException);
    }

    [TestMethod]
    public async Task ExchangeDatagramAsync_KdcDoesNotResolve_ThrowsIOExceptionWithTheConnectorsMessage()
    {
        DatagramKdc kdc = new([]) { Failure = DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: kdc.example.test") };
        KerberosKdcSocketTransport transport = new(kdc, new FakeConnector(), TimeSpan.FromSeconds(1), new ManualTimeProvider());

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => ExchangeDatagramAsync(transport, "kdc.example.test", 88, new byte[] { 0x01 }, CancellationToken.None));

        Diagnostics.Diff("message", "Could not resolve host: kdc.example.test", failure.Message);
        Assert.AreEqual("Could not resolve host: kdc.example.test", failure.Message);
    }

    [TestMethod]
    public async Task ConnectStreamAsync_KdcAccepts_ConnectsDirectlyWithoutTlsAndDisposesTheConnectionWithTheStream()
    {
        FakeConnector connector = new();
        KerberosKdcSocketTransport transport = new(new DatagramKdc([]), connector, TimeSpan.FromSeconds(1), new ManualTimeProvider());

        Stream stream = await ConnectStreamAsync(transport, "kdc.example.test", 88, CancellationToken.None);
        await stream.DisposeAsync();

        Diagnostics.Act("target", connector.Targets.Single());
        Diagnostics.Assert("target", new ConnectTarget("kdc.example.test", 88, UseTls: false), connector.Targets.Single());
        Diagnostics.Assert("connection disposed", true, connector.Opened.Single().IsDisposed);
        Assert.AreEqual(new ConnectTarget("kdc.example.test", 88, UseTls: false), connector.Targets.Single());
        Assert.IsTrue(connector.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task ConnectStreamAsync_KdcRefuses_ThrowsIOExceptionWithTheConnectorsMessage()
    {
        FakeConnector connector = new() { Failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to kdc.example.test port 88") };
        KerberosKdcSocketTransport transport = new(new DatagramKdc([]), connector, TimeSpan.FromSeconds(1), new ManualTimeProvider());

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => ConnectStreamAsync(transport, "kdc.example.test", 88, CancellationToken.None));

        Diagnostics.Diff("message", "Failed to connect to kdc.example.test port 88", failure.Message);
        Assert.AreEqual("Failed to connect to kdc.example.test port 88", failure.Message);
    }

    [TestMethod]
    public async Task KdcClient_UdpReplyTooBig_AsksAgainOverTcpWithALengthPrefix()
    {
        DatagramKdc udp = new(Error(KerberosErrorMessage.ResponseTooBig));
        byte[] tcpReply = Error(7);
        byte[] framedReply = new byte[4 + tcpReply.Length];
        BinaryPrimitives.WriteInt32BigEndian(framedReply, tcpReply.Length);
        tcpReply.CopyTo(framedReply, 4);
        TcpKdc tcp = new(framedReply);
        KerberosKdcSocketTransport transport = new(udp, tcp, TimeSpan.FromSeconds(1), new ManualTimeProvider());
        KerberosKdcClient client = new(Configuration(), new NoSrvRecords(), transport, new FixedTime(), new FixedRandom());
        using Curl.Kerberos.CredentialCache cache = new(null, new KerberosPrincipal(1, Realm, ["alice"]), [TicketGrantingTicket()]);
        Diagnostics.Arrange("UDP reply", "KRB-ERROR KRB_ERR_RESPONSE_TOO_BIG");
        Diagnostics.Bytes("TCP reply", framedReply);
        Diagnostics.Arrange("service", "HTTP/server.example.test@EXAMPLE.TEST");

        KerberosKdcException failure;
        using (Diagnostics.Phase("service ticket request"))
        {
            failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
                () => client.GetServiceTicketAsync(new KerberosPrincipal(3, Realm, ["HTTP", "server.example.test"]), cache, CancellationToken.None));
        }

        Diagnostics.Act("KDC error", failure.Error);
        Diagnostics.Assert("KDC error", KerberosKdcError.ServerPrincipalUnknown, failure.Error);
        Assert.AreEqual(KerberosKdcError.ServerPrincipalUnknown, failure.Error);
        byte[] udpRequest = udp.Channel.Sent.Single();
        byte[] tcpWritten = [.. tcp.Connection!.Written];
        Diagnostics.Bytes("UDP request", udpRequest);
        Diagnostics.Bytes("TCP written", tcpWritten);
        Diagnostics.Assert("length prefix", udpRequest.Length, BinaryPrimitives.ReadInt32BigEndian(tcpWritten));
        Diagnostics.Diff("TCP request after the prefix", udpRequest, tcpWritten.AsSpan(4));
        Diagnostics.Assert("TCP connection disposed", true, tcp.Connection.IsDisposed);
        Assert.AreEqual(udpRequest.Length, BinaryPrimitives.ReadInt32BigEndian(tcpWritten), "RFC 4120 section 7.2.2: four big-endian length bytes first.");
        CollectionAssert.AreEqual(udpRequest, tcpWritten[4..]);
        Assert.IsTrue(tcp.Connection.IsDisposed);
    }

    private async Task<byte[]> ExchangeDatagramAsync(KerberosKdcSocketTransport transport, string host, int port, byte[] request, CancellationToken cancellationToken)
    {
        Diagnostics.Arrange("KDC", $"{host} port {port} over UDP");
        Diagnostics.Bytes("request", request);
        try
        {
            byte[] reply = await transport.ExchangeDatagramAsync(host, port, request, cancellationToken);
            Diagnostics.Bytes("reply", reply);
            Diagnostics.Act("reply length", reply.Length);
            return reply;
        }
        catch (Exception exception) when (WriteFailure(exception))
        {
            throw;
        }
    }

    private async Task<Stream> ConnectStreamAsync(KerberosKdcSocketTransport transport, string host, int port, CancellationToken cancellationToken)
    {
        Diagnostics.Arrange("KDC", $"{host} port {port} over TCP");
        try
        {
            Stream stream = await transport.ConnectStreamAsync(host, port, cancellationToken);
            Diagnostics.Act("stream", "connected");
            return stream;
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

    private static byte[] Error(int code) => new KerberosErrorMessage
    {
        ServerTime = Now,
        ServerMicroseconds = 0,
        ErrorCode = code,
        Realm = Realm,
        ServerName = new KerberosPrincipalName(2, ["krbtgt", Realm]),
    }.Encode();

    private static KerberosConfiguration Configuration() =>
        new KerberosConfigurationStore(new Krb5Conf(), _ => null).Read();

    private static CachedCredential TicketGrantingTicket() => new()
    {
        Client = new KerberosPrincipal(1, Realm, ["alice"]),
        Server = KerberosKdcClient.TicketGrantingServer(Realm),
        SessionKey = new KerberosKey(18, new byte[32]),
        AuthenticationTime = Now.AddHours(-1),
        StartTime = DateTimeOffset.UnixEpoch,
        EndTime = Now.AddHours(1),
        RenewUntil = DateTimeOffset.UnixEpoch,
        IsEncryptedInSessionKey = false,
        Flags = KerberosTicketFlags.Forwardable,
        Addresses = [],
        AuthorizationData = [],
        Ticket = new KerberosTicket(Realm, new KerberosPrincipalName(2, ["krbtgt", Realm]), new KerberosEncryptedData(18, 1, [0xDE, 0xAD])).Encode(),
        SecondTicket = [],
    };

    /// <summary>A KDC reached over UDP: one channel per open, answering each datagram with <c>reply</c>, or never when it is null.</summary>
    private sealed class DatagramKdc(byte[]? reply) : IDatagramConnector
    {
        public List<string> Opened { get; } = [];

        public KdcChannel Channel { get; } = new(reply);

        public DatagramOpenResult? Failure { get; init; }

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            Opened.Add($"{host}:{port}");
            return ValueTask.FromResult(Failure ?? DatagramOpenResult.Opened(Channel));
        }
    }

    private sealed class KdcChannel(byte[]? reply) : IDatagramChannel
    {
        public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 88);

        public List<byte[]> Sent { get; } = [];

        public bool IsDisposed { get; private set; }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            Assert.AreSame(ServerEndPoint, destination);
            Sent.Add(datagram.ToArray());
            return ValueTask.CompletedTask;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] answer = reply ?? await NeverAsync(cancellationToken);
            answer.CopyTo(buffer);
            return new DatagramReceived(answer.Length, ServerEndPoint);
        }

        private static async Task<byte[]> NeverAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return [];
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A KDC reached over TCP: one connection that reads back <c>reply</c>.</summary>
    private sealed class TcpKdc(byte[] reply) : IConnector
    {
        public ScriptedConnection? Connection { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Connection = new ScriptedConnection(reply);
            return ValueTask.FromResult(ConnectResult.Connected(Connection));
        }
    }

    /// <summary>A <c>/etc/krb5.conf</c> naming one KDC for the realm.</summary>
    private sealed class Krb5Conf : IKerberosFileReader
    {
        public byte[]? ReadAllBytes(string path) =>
            path == "/etc/krb5.conf" ? System.Text.Encoding.UTF8.GetBytes($"[realms]\n {Realm} = {{\n kdc = kdc.example.test\n }}\n") : null;

        public IReadOnlyList<string>? ListFileNames(string path) => null;
    }

    private sealed class NoSrvRecords : IKerberosSrvLookup
    {
        public Task<IReadOnlyList<KerberosSrvRecord>> LookUpAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KerberosSrvRecord>>([]);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedRandom : IKerberosRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Fill(0x01);
    }
}
