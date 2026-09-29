namespace Curl.Tls;

/// <summary>An <see cref="IServerCertificateVerifier" /> that records every chain it is shown and returns a fixed verdict.</summary>
internal sealed class RecordingCertificateVerifier(ServerCertificateVerdict verdict) : IServerCertificateVerifier
{
    public RecordingCertificateVerifier()
        : this(ServerCertificateVerdict.Accepted)
    {
    }

    public List<ServerCertificateChain> Presented { get; } = [];

    public ServerCertificateVerdict Verify(ServerCertificateChain presented)
    {
        Presented.Add(presented);
        return verdict;
    }
}
