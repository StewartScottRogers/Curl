namespace Curl.Kerberos;

/// <summary>
/// Reads the whole of a credential cache or keytab file, so this library never touches
/// <c>System.IO.File</c> and its tests need no disk.
/// </summary>
public interface IKerberosFileReader
{
    /// <summary>Reads the whole file at <paramref name="path" />.</summary>
    /// <param name="path">The file's path, the part of a <c>FILE:</c> name after the prefix.</param>
    /// <returns>The file's bytes, or <see langword="null" /> when no file exists there.</returns>
    byte[]? ReadAllBytes(string path);
}
