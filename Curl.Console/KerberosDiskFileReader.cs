using Curl.Kerberos;

namespace Curl.Console;

/// <summary>
/// Reads the hand-built Kerberos route's credential cache, keytab and <c>krb5.conf</c> files
/// from disk (ADR-0142). A file that cannot be read is treated as absent, as MIT's library
/// skips a <c>krb5.conf</c> it cannot open.
/// </summary>
internal sealed class KerberosDiskFileReader : IKerberosFileReader
{
    /// <inheritdoc />
    public byte[]? ReadAllBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string>? ListFileNames(string path) =>
        Directory.Exists(path) ? [.. Directory.EnumerateFiles(path).Select(Path.GetFileName).OfType<string>()] : null;
}
