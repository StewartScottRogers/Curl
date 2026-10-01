using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Kerberos;

namespace Curl.Networking;

/// <summary>
/// Loads a realm's <c>krb5.conf</c> <c>http_anchors</c> into the roots an MS-KKDCP proxy's
/// certificate must lead to, as MIT's <c>load_anchor</c> loads them (ADR-0300): every
/// certificate of a <c>FILE:</c> PEM file; every certificate of each file in a <c>DIR:</c>
/// directory whose name does not start with a dot, at least one file loading; and for
/// <c>ENV:</c> the value the environment variable holds. Any value that cannot be loaded fails
/// the whole set, as it fails MIT's TLS setup, so the KDC is never tried against fewer roots.
/// </summary>
internal static class KerberosHttpAnchorLoader
{
    /// <summary>Loads every anchor in <paramref name="httpAnchors" />.</summary>
    /// <param name="httpAnchors">The realm's <c>http_anchors</c> values as written.</param>
    /// <param name="readEnvironmentVariable">Reads an <c>ENV:</c> anchor's variable, <see langword="null" /> when unset.</param>
    /// <returns>Every root the anchors name.</returns>
    /// <exception cref="IOException">An anchor has no known prefix, names a missing variable or directory, or a file that cannot be read.</exception>
    internal static X509Certificate2Collection Load(IReadOnlyList<string> httpAnchors, Func<string, string?> readEnvironmentVariable)
    {
        X509Certificate2Collection roots = [];
        foreach (string written in httpAnchors)
        {
            LoadOne(written, readEnvironmentVariable, roots, environmentAllowed: true);
        }

        return roots;
    }

    // MIT resolves ENV: by loading the variable's value as another anchor; a value that is
    // itself ENV: is refused here rather than followed, so a variable naming itself cannot loop.
    private static void LoadOne(string written, Func<string, string?> readEnvironmentVariable, X509Certificate2Collection roots, bool environmentAllowed)
    {
        KerberosHttpAnchor? anchor = KerberosHttpAnchor.Parse(written);
        switch (anchor?.Kind)
        {
            case KerberosHttpAnchorKind.File:
                roots.AddRange(ReadPemFile(anchor.Location));
                break;
            case KerberosHttpAnchorKind.Directory:
                roots.AddRange(ReadPemDirectory(anchor.Location));
                break;
            case KerberosHttpAnchorKind.EnvironmentVariable when environmentAllowed:
                string value = readEnvironmentVariable(anchor.Location)
                    ?? throw new IOException($"The http_anchors environment variable {anchor.Location} is not set.");
                LoadOne(value, readEnvironmentVariable, roots, environmentAllowed: false);
                break;
            default:
                throw new IOException($"The http_anchors value {written} is not a FILE:, DIR: or ENV: anchor.");
        }
    }

    private static X509Certificate2Collection ReadPemFile(string path)
    {
        try
        {
            X509Certificate2Collection certificates = [];
            certificates.ImportFromPemFile(path);
            return certificates;
        }
        catch (Exception exception) when (exception is CryptographicException or UnauthorizedAccessException)
        {
            throw new IOException($"The http_anchors file {path} cannot be loaded: {exception.Message}", exception);
        }
    }

    private static X509Certificate2Collection ReadPemDirectory(string path)
    {
        X509Certificate2Collection roots = [];
        bool anyLoaded = false;
        foreach (string file in Directory.EnumerateFiles(path).Where(file => !Path.GetFileName(file).StartsWith('.')))
        {
            try
            {
                roots.AddRange(ReadPemFile(file));
                anyLoaded = true;
            }
            catch (IOException)
            {
                // MIT's load_anchor_dir skips a file it cannot load and needs only one that loads.
            }
        }

        return anyLoaded ? roots : throw new IOException($"The http_anchors directory {path} has no file that loads.");
    }
}
