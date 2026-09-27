namespace Curl.Networking;

/// <summary>
/// A <c>--cert</c> path naming a certificate in a Windows store,
/// <c>&lt;location&gt;\&lt;store name&gt;\&lt;thumbprint&gt;</c>, parsed as curl 8.21.0's
/// Schannel build parses it (<c>get_cert_location</c> in <c>lib/vtls/schannel.c</c>,
/// measured 2026-09-27; ADR-0066).
/// </summary>
/// <param name="Location">The store location.</param>
/// <param name="StoreName">The store name as written; Windows matches it ignoring case.</param>
/// <param name="Thumbprint">The 40 characters after the second backslash, not yet checked as hex.</param>
internal sealed record ClientCertificateStorePath(
    ClientCertificateStoreLocation Location,
    string StoreName,
    string Thumbprint)
{
    private const char Separator = '\\';

    private const int ThumbprintLength = 40;

    // curl compares the text before the first backslash with each name, in this order, for
    // as many characters as that text has: case-sensitive, and any prefix of a name
    // (nothing at all included) selects the first name it begins.
    private static readonly ClientCertificateStoreLocation[] LocationsInCurlOrder =
    [
        ClientCertificateStoreLocation.CurrentUser,
        ClientCertificateStoreLocation.LocalMachine,
        ClientCertificateStoreLocation.CurrentService,
        ClientCertificateStoreLocation.Services,
        ClientCertificateStoreLocation.Users,
        ClientCertificateStoreLocation.CurrentUserGroupPolicy,
        ClientCertificateStoreLocation.LocalMachineGroupPolicy,
        ClientCertificateStoreLocation.LocalMachineEnterprise,
    ];

    /// <summary>
    /// Parses a <c>--cert</c> path as a store path. A path that is not one is read as a file.
    /// </summary>
    /// <param name="path">The certificate path, as split from the <c>--cert</c> value.</param>
    /// <returns>The store path, or <see langword="null" /> when the path is not one.</returns>
    public static ClientCertificateStorePath? Parse(string path)
    {
        var locationEnd = path.IndexOf(Separator, StringComparison.Ordinal);
        if (locationEnd < 0)
        {
            return null;
        }

        var locationText = path[..locationEnd];
        var location = Array.FindIndex(LocationsInCurlOrder, candidate => candidate.ToString().StartsWith(locationText, StringComparison.Ordinal));
        var storeNameEnd = path.IndexOf(Separator, locationEnd + 1);
        if (location < 0 || storeNameEnd < 0 || path.Length - storeNameEnd - 1 != ThumbprintLength)
        {
            return null;
        }

        return new ClientCertificateStorePath(
            LocationsInCurlOrder[location],
            path[(locationEnd + 1)..storeNameEnd],
            path[(storeNameEnd + 1)..]);
    }
}
