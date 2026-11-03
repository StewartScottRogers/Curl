using System.Security.Cryptography.X509Certificates;
using Curl.Conformance;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5.Transports;
using Curl.Http2;

namespace Curl.Conformance.Extended.DnsQuery.Encrypted;

/// <summary>
/// A reference DNS over HTTPS server over HTTP/2 (RFC 8484 section 5.2, BL-4690, ADR-1231), for a plain TCP route: it
/// serves the TLS handshake with <paramref name="certificate" /> selecting ALPN <c>h2</c> (a TLS route's front end offers
/// no ALPN), then HTTP/2 through the gRPC reference server's session, which records each request in
/// <see cref="Received" />. Once a request's stream ends, its body is decoded as an <c>application/dns-message</c>
/// query and the first reply <paramref name="respond" /> gives is sent as a 200 of that media type.
/// </summary>
/// <param name="respond">Gives the replies to each query; the first is sent.</param>
/// <param name="certificate">The server certificate with its private key.</param>
public sealed class DohHttp2ReferenceServer(DnsReferenceResponder respond, X509Certificate2 certificate) : IReferenceServer
{
    private readonly GrpcReferenceServer http2 = new(new DohAnswerService(respond));

    /// <summary>Gets the HTTP/2 requests curl sent, in order, each with its header fields and DATA.</summary>
    public IReadOnlyList<GrpcReferenceCall> Received => http2.Calls;

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        DelegatingDuplexStream wire = new(connection.ReadAsync, connection.WriteAsync);
        await using Stream tls = await TlsServerStream.AcceptAsync(wire, new TlsServerOptions(certificate, ["h2"], RequestClientCertificate: false), TlsServerStream.PlatformServesTls13, cancellationToken)
            .ConfigureAwait(false);
        await http2.ServeAsync(new ReferenceConnection(tls, new WireExchange(0, ReferenceTransport.Tls)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Answers each DoH request once its stream has ended.</summary>
    private sealed class DohAnswerService(DnsReferenceResponder respond) : IGrpcReferenceService
    {
        private int answered;

        public GrpcReferenceReply? Answer(GrpcReferenceCall call)
        {
            if (!call.IsRequestEnded)
            {
                return null;
            }

            DnsReferenceQuery query = DnsReferenceQuery.Decode([.. call.DataFrames.SelectMany(static frame => frame)]);
            byte[] answer = DnsReferenceMessageWriter.WriteReply(query, respond(query, answered++)[0]);
            return new GrpcReferenceReply([new(":status", "200"), new("content-type", "application/dns-message")], [answer], []);
        }
    }
}
