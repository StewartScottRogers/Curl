using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Builds a certificate chain, and answers for the build .NET on Linux cannot finish: it throws
/// <see cref="CryptographicException" /> instead of reporting a status when OpenSSL's verification
/// ends with <c>X509_V_ERR_UNSPECIFIED</c> (1), as for a name constraint that does not decode
/// (ADR-1445). Such a chain has no elements, and curl reports its verify result as 1.
/// </summary>
internal static class CertificateChainBuild
{
    // How .NET's message ends for the one OpenSSL code, 1, behind which the chain is refused.
    private const string UnspecifiedErrorEnding = "unhandled error code '1'.";

    // The chains whose last build ended that way; an entry lives no longer than its chain.
    private static readonly ConditionalWeakTable<X509Chain, CryptographicException> EndedUnspecified = [];

    /// <summary>Builds the chain for a certificate.</summary>
    /// <param name="chain">The chain, its policy already set.</param>
    /// <param name="certificate">The certificate the chain is built for.</param>
    /// <returns>Whether the chain verified; <see langword="false" /> too when the build ended with error 1.</returns>
    internal static bool Build(X509Chain chain, X509Certificate2 certificate) => Build(chain, () => chain.Build(certificate));

    /// <summary>Runs a chain's build, remembering one that ends with OpenSSL's error 1.</summary>
    /// <param name="chain">The chain being built.</param>
    /// <param name="build">The build itself.</param>
    /// <returns>What the build returns, or <see langword="false" /> when it ended with error 1.</returns>
    /// <exception cref="CryptographicException">The build failed for any other reason.</exception>
    internal static bool Build(X509Chain chain, Func<bool> build)
    {
        try
        {
            return build();
        }
        catch (CryptographicException exception) when (exception.Message.EndsWith(UnspecifiedErrorEnding, StringComparison.Ordinal))
        {
            EndedUnspecified.AddOrUpdate(chain, exception);
            return false;
        }
    }

    /// <summary>Whether the chain is empty because its build ended with OpenSSL's error 1.</summary>
    /// <param name="chain">The chain, if there is one.</param>
    /// <returns><see langword="true" /> for such a chain.</returns>
    internal static bool EndedAsUnspecified(X509Chain? chain) =>
        chain is { ChainElements.Count: 0 } && EndedUnspecified.TryGetValue(chain, out _);
}
