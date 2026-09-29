using System.Globalization;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// Finds the default credential cache as MIT Kerberos does and reads a <c>FILE:</c> cache
/// through the injected file reader (ADR-0142, ADR-0158).
/// </summary>
/// <param name="files">Reads the cache file.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
/// <param name="readUserId">Gives the user's numeric id, the <c>&lt;uid&gt;</c> of <c>/tmp/krb5cc_&lt;uid&gt;</c>.</param>
public sealed class CredentialCacheStore(
    IKerberosFileReader files,
    Func<string, string?> readEnvironmentVariable,
    Func<uint> readUserId)
{
    /// <summary>The environment variable that names the credential cache.</summary>
    public const string CacheNameVariable = "KRB5CCNAME";

    /// <summary>
    /// Gets the default cache's name: <c>KRB5CCNAME</c> when set and not empty, otherwise
    /// MIT's built-in <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c>.
    /// </summary>
    /// <returns>The default cache's name, e.g. <c>FILE:/tmp/krb5cc_1000</c>.</returns>
    public string DefaultCacheName()
    {
        string? named = readEnvironmentVariable(CacheNameVariable);
        return string.IsNullOrEmpty(named)
            ? string.Create(CultureInfo.InvariantCulture, $"FILE:/tmp/krb5cc_{readUserId()}")
            : named;
    }

    /// <summary>Reads the default credential cache.</summary>
    /// <returns>The cache's default principal and credentials.</returns>
    /// <exception cref="KerberosFileException">The cache cannot be read; <see cref="KerberosFileException.Error" /> says why.</exception>
    public CredentialCache ReadDefault() => Read(DefaultCacheName());

    /// <summary>Reads the credential cache <paramref name="cacheName" /> names.</summary>
    /// <param name="cacheName">A cache name, e.g. <c>FILE:/tmp/krb5cc_1000</c> or a bare path.</param>
    /// <returns>The cache's default principal and credentials.</returns>
    /// <exception cref="KerberosFileException">
    /// The name's type is not <c>FILE</c> (<see cref="KerberosFileError.UnsupportedType" />),
    /// the file does not exist (<see cref="KerberosFileError.NotFound" />), or it is not a
    /// readable version 4 cache.
    /// </exception>
    public CredentialCache Read(string cacheName)
    {
        byte[] bytes = KerberosFileName.ReadFile(files, cacheName, KerberosFileName.FileType);
        try
        {
            return CredentialCacheReader.Read(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
