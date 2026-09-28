using System.Net.Security;
using System.Runtime.Versioning;

namespace Curl.Networking;

/// <summary>
/// Builds the <see cref="CipherSuitesPolicy" /> the OpenSSL build of <see cref="SslStreamTlsProvider" />
/// offers, or none where the platform cannot construct one. <see cref="CipherSuitesPolicy" />
/// throws <see cref="PlatformNotSupportedException" /> on Windows, so
/// <see cref="ForThisPlatform" /> builds none there (ADR-0011).
/// </summary>
/// <param name="platformSupportsCipherSuitesPolicy">
/// <see langword="true" /> to construct the policy, <see langword="false" /> to build none.
/// </param>
internal sealed class CipherSuitesPolicyFactory(bool platformSupportsCipherSuitesPolicy) : ICipherSuitesPolicyFactory
{
    /// <summary>Gets the factory for the platform this process runs on: none on Windows.</summary>
    internal static CipherSuitesPolicyFactory ForThisPlatform { get; } = new(!OperatingSystem.IsWindows());

    /// <summary>
    /// Gets whether <see cref="Create" /> constructs a policy. It is the platform guard the
    /// CA1416 analyzer reads, so it is <see langword="false" /> on Windows outside tests.
    /// </summary>
    [UnsupportedOSPlatformGuard("windows")]
    internal bool PlatformSupportsCipherSuitesPolicy { get; } = platformSupportsCipherSuitesPolicy;

    /// <inheritdoc />
    /// <param name="suites">The suites to offer.</param>
    /// <returns>
    /// The policy, or <see langword="null" /> when <see cref="PlatformSupportsCipherSuitesPolicy" />
    /// is <see langword="false" />.
    /// </returns>
    public CipherSuitesPolicy? Create(IEnumerable<TlsCipherSuite> suites) =>
        PlatformSupportsCipherSuitesPolicy ? new CipherSuitesPolicy(suites) : null;
}
