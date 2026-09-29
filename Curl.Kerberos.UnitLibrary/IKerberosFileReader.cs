namespace Curl.Kerberos;

/// <summary>
/// Reads the whole of a credential cache, keytab or <c>krb5.conf</c> file, and lists the
/// files an <c>includedir</c> directive names, so this library never touches
/// <c>System.IO</c> and its tests need no disk.
/// </summary>
public interface IKerberosFileReader
{
    /// <summary>Reads the whole file at <paramref name="path" />.</summary>
    /// <param name="path">The file's path, e.g. the part of a <c>FILE:</c> name after the prefix.</param>
    /// <returns>The file's bytes, or <see langword="null" /> when no file exists there.</returns>
    byte[]? ReadAllBytes(string path);

    /// <summary>Lists the names of the files in the directory at <paramref name="path" />.</summary>
    /// <param name="path">The directory's path.</param>
    /// <returns>
    /// The files' names without their directory, in any order, or <see langword="null" /> when
    /// no directory exists there.
    /// </returns>
    IReadOnlyList<string>? ListFileNames(string path);
}
