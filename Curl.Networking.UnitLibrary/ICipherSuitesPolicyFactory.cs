using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// Builds the <see cref="CipherSuitesPolicy" /> the OpenSSL build of
/// <see cref="SslStreamTlsProvider" /> offers, or none where the platform cannot apply one.
/// </summary>
internal interface ICipherSuitesPolicyFactory
{
    /// <summary>Builds the policy that offers <paramref name="suites" />, in order.</summary>
    /// <param name="suites">The suites to offer.</param>
    /// <returns>The policy, or <see langword="null" /> where the platform cannot apply one.</returns>
    CipherSuitesPolicy? Create(IEnumerable<TlsCipherSuite> suites);
}
