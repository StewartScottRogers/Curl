namespace Curl.Kerberos;

/// <summary>
/// A credential cache or keytab name split as MIT's <c>krb5_cc_resolve</c> and
/// <c>krb5_kt_resolve</c> split it: the type before the first colon and the residual after
/// it, or type <c>FILE</c> and the whole name when there is no colon.
/// </summary>
/// <param name="Type">The type, e.g. <c>FILE</c>, <c>DIR</c> or <c>KCM</c>; matched case-sensitively, as MIT does.</param>
/// <param name="Residual">What follows the type, e.g. a path for <c>FILE</c>.</param>
public sealed record KerberosFileName(string Type, string Residual)
{
    /// <summary>The type of a name that names a file directly.</summary>
    public const string FileType = "FILE";

    /// <summary>Splits <paramref name="name" /> into its type and residual.</summary>
    /// <param name="name">A name such as <c>FILE:/tmp/krb5cc_1000</c> or <c>/tmp/krb5cc_1000</c>.</param>
    /// <returns>The type and residual.</returns>
    public static KerberosFileName Parse(string name)
    {
        int colon = name.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new KerberosFileName(FileType, name)
            : new KerberosFileName(name[..colon], name[(colon + 1)..]);
    }

    /// <summary>
    /// Reads the file <paramref name="name" /> names through <paramref name="files" />, when
    /// its type is one of <paramref name="fileTypes" />.
    /// </summary>
    /// <exception cref="KerberosFileException">
    /// The type is not one of <paramref name="fileTypes" />
    /// (<see cref="KerberosFileError.UnsupportedType" />) or no file exists
    /// (<see cref="KerberosFileError.NotFound" />).
    /// </exception>
    internal static byte[] ReadFile(IKerberosFileReader files, string name, params string[] fileTypes)
    {
        KerberosFileName parsed = Parse(name);
        if (!fileTypes.Contains(parsed.Type, StringComparer.Ordinal))
        {
            throw new KerberosFileException(KerberosFileError.UnsupportedType);
        }

        return files.ReadAllBytes(parsed.Residual) ?? throw new KerberosFileException(KerberosFileError.NotFound);
    }
}
