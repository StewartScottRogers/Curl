using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Opens a Windows certificate store for the Schannel build's <c>--cert</c> store path
/// (<c>CurrentUser\MY\&lt;thumbprint&gt;</c>), the seam that keeps the tests off the
/// machine's stores.
/// </summary>
internal interface IClientCertificateStore
{
    /// <summary>Opens an existing store read-only and returns the certificates in it.</summary>
    /// <param name="location">The store location, as <see cref="ClientCertificateStorePath" /> parsed it.</param>
    /// <param name="storeName">The store name, such as <c>MY</c>, as written.</param>
    /// <returns>
    /// The store's certificates, or <see langword="null" /> when no such store exists or this
    /// platform cannot open it.
    /// </returns>
    X509Certificate2Collection? OpenCertificates(ClientCertificateStoreLocation location, string storeName);
}
