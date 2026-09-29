using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// Finds the default keytab as MIT Kerberos does and reads a <c>FILE:</c> or
/// <c>WRFILE:</c> keytab through the injected file reader (ADR-0158, BL-816).
/// </summary>
/// <param name="files">Reads the keytab file.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
/// <param name="readUserId">Gives the user's numeric id, for <c>%{uid}</c> in <c>default_keytab_name</c>.</param>
/// <param name="configuration">
/// <c>krb5.conf</c>, for <c>[libdefaults] default_keytab_name</c>; <see langword="null" />
/// reads as an empty file.
/// </param>
/// <param name="readUserName">
/// Gives the user's login name for <c>%{username}</c>; <see langword="null" /> uses
/// <see cref="Environment.UserName" />, which is <c>getpwuid(geteuid())</c>'s name off Windows.
/// </param>
public sealed class KeytabStore(
    IKerberosFileReader files,
    Func<string, string?> readEnvironmentVariable,
    Func<uint> readUserId,
    KerberosConfiguration? configuration = null,
    Func<string>? readUserName = null)
{
    /// <summary>The environment variable that names the keytab.</summary>
    public const string KeytabNameVariable = "KRB5_KTNAME";

    /// <summary>MIT's built-in default keytab name.</summary>
    public const string BuiltInKeytabName = "FILE:/etc/krb5.keytab";

    private const string WritableFileType = "WRFILE";

    /// <summary>
    /// Gets the default keytab's name: <c>KRB5_KTNAME</c> when set and not empty, else
    /// <c>[libdefaults] default_keytab_name</c> with MIT's <c>%{token}</c> parameters
    /// expanded, else <see cref="BuiltInKeytabName" />.
    /// </summary>
    /// <returns>The default keytab's name.</returns>
    /// <exception cref="KerberosFileException">
    /// <c>default_keytab_name</c> holds an unclosed or unknown token
    /// (<see cref="KerberosFileError.PathTokenInvalid" />).
    /// </exception>
    public string DefaultKeytabName()
    {
        string? named = readEnvironmentVariable(KeytabNameVariable);
        if (!string.IsNullOrEmpty(named))
        {
            return named;
        }

        string? configured = ConfiguredKeytabName();
        return configured is null ? BuiltInKeytabName : Expansion().Expand(configured);
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

    private string? ConfiguredKeytabName() =>
        configuration?.GetValues("libdefaults", "default_keytab_name") is [string value, ..] ? value : null;

    private KerberosPathExpansion Expansion() =>
        new(readUserId(), readEnvironmentVariable, readUserName ?? (() => Environment.UserName));
}
