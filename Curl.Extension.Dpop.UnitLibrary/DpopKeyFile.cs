using System.Security.Cryptography;

namespace Curl.Extension.Dpop;

/// <summary>Reads the <c>--dpop</c> key file.</summary>
internal static class DpopKeyFile
{
    /// <summary>Reads the private key in a PEM file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Ends the read.</param>
    /// <returns>The key, or <see langword="null" /> when the file cannot be read or holds no EC or RSA private key on a JWS curve.</returns>
    public static async Task<DpopKey?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return DpopKey.FromPem(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
