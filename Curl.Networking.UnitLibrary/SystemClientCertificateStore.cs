using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IClientCertificateStore" />: opens the store with
/// <see cref="X509Store" />, which reaches the <c>CurrentUser</c> and <c>LocalMachine</c>
/// locations only (ADR-0066).
/// </summary>
internal sealed class SystemClientCertificateStore : IClientCertificateStore
{
    /// <inheritdoc />
    public X509Certificate2Collection? OpenCertificates(ClientCertificateStoreLocation location, string storeName)
    {
        StoreLocation storeLocation;
        switch (location)
        {
            case ClientCertificateStoreLocation.CurrentUser:
                storeLocation = StoreLocation.CurrentUser;
                break;
            case ClientCertificateStoreLocation.LocalMachine:
                storeLocation = StoreLocation.LocalMachine;
                break;
            default:
                return null;
        }

        try
        {
            using var store = new X509Store(storeName, storeLocation, OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            return store.Certificates;
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
