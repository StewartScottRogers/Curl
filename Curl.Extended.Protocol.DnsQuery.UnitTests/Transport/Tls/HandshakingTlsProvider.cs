using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tls;

/// <summary>
/// A TLS provider whose handshake hands the plaintext connection back as the secure one, or fails with
/// <see cref="Failure" />; it records the host it was asked to verify, runs <see cref="OnHandshake" /> during the
/// handshake, and judges every certificate chain by <see cref="ChainVerdict" />.
/// </summary>
internal sealed class HandshakingTlsProvider : ITlsProvider
{
    public ConnectResult? Failure { get; init; }

    public Action OnHandshake { get; init; } = () => { };

    public ServerCertificateChainVerdict ChainVerdict { get; init; } = ServerCertificateChainVerdict.Accepted;

    public string? TargetHost { get; private set; }

    public IReadOnlyList<ReadOnlyMemory<byte>>? Certificates { get; private set; }

    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
    {
        TargetHost = targetHost;
        OnHandshake();
        return ValueTask.FromResult(Failure ?? ConnectResult.Connected(plaintext));
    }

    /// <summary>Gets the protocol ALPN selects, or <see langword="null" /> when the server selects none.</summary>
    public string? ApplicationProtocol { get; init; }

    /// <summary>Gets the protocols the client offered through ALPN, or <see langword="null" /> before an ALPN handshake.</summary>
    public IReadOnlyList<string>? OfferedApplicationProtocols { get; private set; }

    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, IReadOnlyList<string> applicationProtocols, CancellationToken cancellationToken)
    {
        OfferedApplicationProtocols = applicationProtocols;
        TargetHost = targetHost;
        OnHandshake();
        return ValueTask.FromResult(Failure ?? ConnectResult.Connected(plaintext, null, applicationProtocol: ApplicationProtocol));
    }

    public ServerCertificateChainVerdict VerifyServerCertificateChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates, string targetHost)
    {
        Certificates = certificates;
        TargetHost = targetHost;
        return ChainVerdict;
    }
}
