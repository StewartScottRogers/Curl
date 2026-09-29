using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// Finds the default keytab as MIT Kerberos does and reads a <c>FILE:</c> or
/// <c>WRFILE:</c> keytab through the injected file reader (ADR-0158).
/// </summary>
/// <param name="files">Reads the keytab file.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
public sealed class KeytabStore(IKerberosFileReader files, Func<string, string?> readEnvironmentVariable)
{
    /// <summary>The environment variable that names the keytab.</summary>
    public const string KeytabNameVariable = "KRB5_KTNAME";

    /// <summary>MIT's built-in default keytab name.</summary>
    public const string BuiltInKeytabName = "FILE:/etc/krb5.keytab";

    private const string WritableFileType = "WRFILE";

    /// <summary>
    /// Gets the default keytab's name: <c>KRB5_KTNAME</c> when set and not empty, otherwise
    /// <see cref="BuiltInKeytabName" />.
    /// </summary>
    /// <returns>The default keytab's name.</returns>
    public string DefaultKeytabName()
    {
        string? named = readEnvironmentVariable(KeytabNameVariable);
        return string.IsNullOrEmpty(named) ? BuiltInKeytabName : named;
    }

    /// <summary>Reads the default keytab.</summary>
    /// <returns>The keytab's entries.</returns>
    /// <exception cref="KerberosFileException">The keytab cannot be read; <see cref="KerberosFileException.Error" /> says why.</exception>
    public Keytab ReadDefault() => Read(DefaultKeytabName());

    /// <summary>Reads the keytab <paramref name="keytabName" /> names.</summary>
    /// <param name="keytabName">A keytab name, e.g. <c>FILE:/etc/krb5.keytab</c> or a bare path.</param>
    /// <returns>The keytab's entries.</returns>
    /// <exception cref="KerberosFileException">
    /// The name's type is not <c>FILE</c> or <c>WRFILE</c>
    /// (<see cref="KerberosFileError.UnsupportedType" />), the file does not exist
    /// (<see cref="KerberosFileError.NotFound" />), or it is not a readable version 2 keytab.
    /// </exception>
    public Keytab Read(string keytabName)
    {
        byte[] bytes = KerberosFileName.ReadFile(files, keytabName, KerberosFileName.FileType, WritableFileType);
        try
        {
            return KeytabReader.Read(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
