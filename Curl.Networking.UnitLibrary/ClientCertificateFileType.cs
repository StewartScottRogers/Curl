using System.Collections.Frozen;

namespace Curl.Networking;

/// <summary>
/// The file type a <c>--cert-type</c> or <c>--key-type</c> value names, as curl reads it.
/// </summary>
internal enum ClientCertificateFileType
{
    /// <summary><c>PEM</c>, and the type when none is given.</summary>
    Pem,

    /// <summary><c>DER</c>.</summary>
    Der,

    /// <summary><c>P12</c>: PKCS#12.</summary>
    Pkcs12,

    /// <summary><c>ENG</c>: a key or certificate held by an OpenSSL engine.</summary>
    Engine,

    /// <summary><c>PROV</c>: a key or certificate held by an OpenSSL provider.</summary>
    Provider,

    /// <summary>Any other value.</summary>
    Unsupported,
}

/// <summary>
/// Reads a <c>--cert-type</c> or <c>--key-type</c> value into the
/// <see cref="ClientCertificateFileType" /> it names, ignoring case as curl does.
/// </summary>
internal static class ClientCertificateFileTypeName
{
    // Upper-casing with the invariant culture and comparing ordinally ignoring case use the
    // same simple case mapping, so this matches what curl accepts.
    private static readonly FrozenDictionary<string, ClientCertificateFileType> TypesByName =
        new Dictionary<string, ClientCertificateFileType>(StringComparer.OrdinalIgnoreCase)
        {
            ["PEM"] = ClientCertificateFileType.Pem,
            ["DER"] = ClientCertificateFileType.Der,
            ["P12"] = ClientCertificateFileType.Pkcs12,
            ["ENG"] = ClientCertificateFileType.Engine,
            ["PROV"] = ClientCertificateFileType.Provider,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads a type name.</summary>
    /// <param name="name">The value as given, or <see langword="null" /> when none was.</param>
    /// <returns>The type it names; <see cref="ClientCertificateFileType.Pem" /> for none.</returns>
    public static ClientCertificateFileType Parse(string? name) =>
        name is null
            ? ClientCertificateFileType.Pem
            : TypesByName.GetValueOrDefault(name, ClientCertificateFileType.Unsupported);
}
