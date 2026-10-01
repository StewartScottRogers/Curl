using Curl.Kerberos;

namespace Curl.Console;

/// <summary>
/// Appends a credential to the hand-built Kerberos route's <c>FILE:</c> or <c>DIR:</c>
/// credential cache on disk, as MIT's <c>cc_file.c</c> stores one (ADR-0208, BL-892). The file
/// is opened, never created; one that does not exist or cannot be opened for writing is
/// treated as absent, so the ticket is used unstored, as MIT ignores
/// <c>krb5_cc_store_cred</c>'s failure.
/// </summary>
internal sealed class KerberosDiskFileWriter : IKerberosFileWriter
{
    /// <inheritdoc />
    public bool AppendAllBytes(string path, ReadOnlySpan<byte> bytes)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.Read);
            stream.Seek(0, SeekOrigin.End);
            stream.Write(bytes);
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
