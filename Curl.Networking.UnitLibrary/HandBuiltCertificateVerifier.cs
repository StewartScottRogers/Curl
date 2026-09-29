using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The hand-built client's <see cref="IServerCertificateVerifier" /> (ADR-0140, "The
/// verification hand-off"): builds the <see cref="X509Chain" /> <see cref="SslStream" />
/// would build for what the server sent, computes the <see cref="SslPolicyErrors" /> it would
/// pass to its validation callback, and hands both to the same
/// <see cref="ServerCertificateVerification" /> <see cref="SslStreamTlsProvider" /> uses. So
/// <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, exit 60 and every message are the same on
/// both paths.
/// </summary>
/// <param name="verification">The shared judgement.</param>
/// <param name="chainPolicy">The <c>--cacert</c> chain policy, or <see langword="null" /> for the system store.</param>
/// <param name="anchorsBesideSystemStore">The <c>--capath</c> roots trusted beside the system store.</param>
/// <param name="revocationLists">The <c>--crlfile</c> lists, or <see langword="null" /> when none are checked.</param>
/// <param name="targetHost">The host the certificate is checked against, IPv6 brackets and all.</param>
internal sealed class HandBuiltCertificateVerifier(
    ServerCertificateVerification verification,
    X509ChainPolicy? chainPolicy,
    X509Certificate2Collection anchorsBesideSystemStore,
    CertificateRevocationListFile? revocationLists,
    string targetHost) : IServerCertificateVerifier
{
    // What SslStream asks of a server certificate's chain: the server authentication usage.
    private static readonly Oid ServerAuthentication = new("1.3.6.1.5.5.7.3.1");

    /// <summary>Gets what the handshake event reports about the certificate once it has been judged.</summary>
    public PeerVerification Observed { get; private set; } = PeerVerification.Unobserved;

    /// <summary>Gets the DER of every certificate the server sent, its own first, once it has been judged.</summary>
    public ReadOnlyMemory<byte>[] PeerCertificates { get; private set; } = [];

    /// <inheritdoc />
    /// <remarks>
    /// A rejection carries the exit code and message as a <c>(CurlExitCode, string)</c> tuple,
    /// which <see cref="HandBuiltTlsProvider" /> reports as the handshake's failure.
    /// </remarks>
    public ServerCertificateVerdict Verify(ServerCertificateChain presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        PeerCertificates = [.. presented.Certificates.Select(der => new ReadOnlyMemory<byte>(der))];
        var sent = LoadAll(presented.Certificates);
        try
        {
            return Judge(sent);
        }
        finally
        {
            foreach (var certificate in sent)
            {
                certificate.Dispose();
            }
        }
    }

    // Every certificate the server sent, or none when its own does not parse, as though it
    // sent nothing; the others are only chain candidates, so one that does not parse is left out.
    private static List<X509Certificate2> LoadAll(IReadOnlyList<byte[]> certificates)
    {
        var loaded = new List<X509Certificate2>();
        foreach (var der in certificates)
        {
            try
            {
                loaded.Add(X509CertificateLoader.LoadCertificate(der));
            }
            catch (CryptographicException) when (loaded.Count > 0)
            {
                // A chain candidate that does not parse cannot help build the chain.
            }
            catch (CryptographicException)
            {
                return [];
            }
        }

        return loaded;
    }

    private ServerCertificateVerdict Judge(List<X509Certificate2> sent)
    {
        using var chain = new X509Chain();
        var errors = sent.Count == 0
            ? SslPolicyErrors.RemoteCertificateNotAvailable
            : BuildChain(chain, sent);
        var (observed, failure) = verification.Judge(
            errors, sent.Count == 0 ? null : chain, targetHost, anchorsBesideSystemStore, revocationLists, PeerCertificates);
        Observed = observed;
        return failure is { } rejected ? ServerCertificateVerdict.Rejected(rejected) : ServerCertificateVerdict.Accepted;
    }

    // The chain SslStream builds: the --cacert policy, or the system store for server
    // authentication, with what else the server sent as candidates; then its name check.
    private SslPolicyErrors BuildChain(X509Chain chain, List<X509Certificate2> sent)
    {
        if (chainPolicy is null)
        {
            chain.ChainPolicy.ApplicationPolicy.Add(ServerAuthentication);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        }
        else
        {
            chain.ChainPolicy = chainPolicy.Clone();
        }

        chain.ChainPolicy.ExtraStore.AddRange(sent.Skip(1).ToArray());
        var errors = chain.Build(sent[0]) ? SslPolicyErrors.None : SslPolicyErrors.RemoteCertificateChainErrors;
        return sent[0].MatchesHostname(SslStreamTlsProvider.VerifiedHostName(targetHost, insecure: false)!)
            ? errors
            : errors | SslPolicyErrors.RemoteCertificateNameMismatch;
    }
}
