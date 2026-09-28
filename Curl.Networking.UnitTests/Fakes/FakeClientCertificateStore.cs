using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IClientCertificateStore" /> that records the store it is asked to open and
/// answers with <see cref="Certificates" />, so no test reads the machine's stores.
/// </summary>
internal sealed class FakeClientCertificateStore : IClientCertificateStore
{
    /// <summary>
    /// Gets the certificates every store opened holds, or <see langword="null" /> to report
    /// that no such store exists.
    /// </summary>
    public X509Certificate2Collection? Certificates { get; init; } = [];

    /// <summary>Gets the location and name of every store asked for, in order.</summary>
    public List<(ClientCertificateStoreLocation Location, string StoreName)> Opened { get; } = [];

    /// <inheritdoc />
    public X509Certificate2Collection? OpenCertificates(ClientCertificateStoreLocation location, string storeName)
    {
        Opened.Add((location, storeName));
        return Certificates;
    }
}
