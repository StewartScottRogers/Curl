namespace Curl.Kerberos;

/// <summary>
/// Appends to a credential cache file, as MIT's <c>cc_file.c</c> stores a credential by
/// writing it after the ones already there, so this library never touches <c>System.IO</c>
/// and its tests need no disk.
/// </summary>
public interface IKerberosFileWriter
{
    /// <summary>Appends <paramref name="bytes" /> to the end of the existing file at <paramref name="path" />.</summary>
    /// <param name="path">The file's path, e.g. the part of a <c>FILE:</c> name after the prefix.</param>
    /// <param name="bytes">The bytes to append.</param>
    /// <returns>
    /// <see langword="true" /> when the bytes were appended; <see langword="false" /> when they
    /// were not: no file exists there (none is created), or the file cannot be opened or written,
    /// in which case some of the bytes may already have been appended.
    /// </returns>
    bool AppendAllBytes(string path, ReadOnlySpan<byte> bytes);
}
