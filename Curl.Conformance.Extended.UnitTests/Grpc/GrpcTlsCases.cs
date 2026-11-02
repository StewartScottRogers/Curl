using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// A <c>grpcs://</c> call: gRPC over HTTP/2 over TLS, which PROTOCOL-HTTP2.md runs as RFC 9113 <c>h2</c>, so curl offers
/// ALPN <c>h2</c> and the call goes ahead once the server selects it. The harness's TLS route offers no ALPN, so the case
/// serves TLS itself, selecting <c>h2</c>, over a TCP route whose exchange records curl's raw ClientHello.
/// </summary>
[TestClass]
public sealed class GrpcTlsCases
{
    private const int TlsPort = 50052;

    // The ClientHello's ALPN extension offering h2 alone: type 0x0010, length 5, list length 3, "h2" with its length byte.
    private static readonly byte[] AlpnOfferingH2 = [0x00, 0x10, 0x00, 0x05, 0x00, 0x03, 0x02, (byte)'h', (byte)'2'];

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Tls-1")]
    public Task Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Tls-1"],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("grpc-tls-").FullName;
                try
                {
                    string proto = Path.Combine(directory, "greeter.proto");
                    await File.WriteAllTextAsync(proto, GrpcUnaryCases.Proto);
                    GrpcReferenceServer grpc = new(new GrpcScriptedService(GrpcUnaryCases.MethodPath, GrpcReferenceReply.Ok([0x0A, 0x04, .. "hi x"u8])));
                    using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");

                    CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                        CurlRunner,
                        [ReferenceRoute.Tcp(TlsPort, new AlpnH2TlsServer(grpc, certificate))],
                        ["-sS", "-k", "-m", "10", "--grpc-proto", proto, "-d", """{"name":"x"}""", $"grpcs://localhost:{TlsPort}{GrpcUnaryCases.MethodPath}"]);

                    Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Tls-1");
                    Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Tls-1");
                    Assert.IsTrue(Contains(result.Exchanges.Single().ClientBytes, AlpnOfferingH2), "Grpc:Http2-Tls-1: the ClientHello offers ALPN h2");
                    Assert.AreEqual("https", grpc.Calls.Single().Header(":scheme"), "Grpc:Http2-Tls-1");
                    Assert.AreEqual("{\n  \"message\": \"hi x\"\n}\n", Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Tls-1");
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    private static bool Contains(byte[] bytes, byte[] sequence) => bytes.AsSpan().IndexOf(sequence) >= 0;

    // Serves TLS with ALPN h2 over the TCP route's connection, then the gRPC server over the TLS plaintext.
    private sealed class AlpnH2TlsServer(GrpcReferenceServer grpc, X509Certificate2 certificate) : IReferenceServer
    {
        public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
        {
            TlsServerOptions options = new(certificate, ["h2"], RequestClientCertificate: false);
            await using Stream tls = await TlsServerStream.AcceptAsync(new ReferenceConnectionStream(connection), options, TlsServerStream.PlatformServesTls13, cancellationToken);
            await grpc.ServeAsync(new ReferenceConnection(tls, new WireExchange(TlsPort, ReferenceTransport.Tls)), cancellationToken);
        }
    }

    // The TCP route's connection as the Stream TlsServerStream reads and writes.
    private sealed class ReferenceConnectionStream(ReferenceConnection connection) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => connection.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => connection.WriteAsync(buffer, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
