namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>
/// Maps <c>az://account/container/blob</c> to the Blob service endpoint ("Addressing Blob Service Resources") and
/// <c>abfs://filesystem@account.dfs.core.windows.net/path</c> (or <c>abfss://</c>) to the Data Lake Storage Gen2 DFS
/// endpoint (the ADLS Gen2 URI). A bare account name gets the public cloud's suffix; a host with a dot is used as it is.
/// An IPv4 or IPv6 address or <c>localhost</c> is a path-style emulator host (ADR-1291): the host is kept as it is
/// and the first path segment is the account, not the host.
/// </summary>
public static class AzureStorageUrlMapping
{
    /// <summary>The host suffix a bare account name gets for the Blob service.</summary>
    public const string BlobHostSuffix = ".blob.core.windows.net";

    /// <summary>The host suffix a bare account name gets for the DFS (Data Lake Storage Gen2) service.</summary>
    public const string DfsHostSuffix = ".dfs.core.windows.net";

    /// <summary>Maps an Azure Storage URL to the endpoint, path and query to request.</summary>
    /// <param name="url">The <c>az</c>, <c>abfs</c> or <c>abfss</c> URL.</param>
    /// <returns>The endpoint to request.</returns>
    /// <exception cref="AzureStorageRequestException">The scheme is not supported, or the URL names no account or no file system.</exception>
    public static AzureStorageEndpoint Map(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.Scheme switch
        {
            "az" => MapBlob(url),
            "abfs" or "abfss" => MapDfs(url),
            _ => throw new AzureStorageRequestException(AzureStorageRequestFailure.UnsupportedScheme, $"Azure Storage does not handle the {url.Scheme} scheme."),
        };
    }

    // A path-style emulator (Azurite) is an IP address or localhost: the account is the first path segment, not part of the host.
    private static AzureStorageEndpoint MapBlob(Uri url) =>
        IsPathStyleHost(url)
            ? new(url.Host, url.AbsolutePath, Query(url), PortOf(url), true)
            : new(AccountHost(url, BlobHostSuffix), url.AbsolutePath, Query(url), PortOf(url));

    private static bool IsPathStyleHost(Uri url) =>
        url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
        || string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || IsNumericIpv4Spelling(url.Host);

    // A numeric spelling of a routable IPv4 address (0x7f000001, 2130706433, 0177.0.0.1) the port's URL parser reads as 127.0.0.1 (EXT-105);
    // never an account name. A spelling that is no address (0xstore, a first octet of 0 as in 12345678 or 123, too many parts) is an
    // account name: Azure account names are 3 to 24 lowercase letters and digits (EXT-117).
    private static bool IsNumericIpv4Spelling(string host)
    {
        var parts = host.Split('.');
        return parts.Length <= 4 && TryParseParts(parts, out var values) && IsRoutableAddress(values);
    }

    private static bool TryParseParts(string[] parts, out ulong[] values)
    {
        values = new ulong[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!TryParseIpv4Part(parts[index], out values[index]))
            {
                return false;
            }
        }

        return true;
    }

    // The leading parts are octets, the last part fills the remaining bytes, and the first octet is not 0.
    private static bool IsRoutableAddress(ulong[] values)
    {
        var lastLimit = 1UL << (8 * (5 - values.Length));
        var leadingPartsInRange = values.Take(values.Length - 1).All(IsOctet);
        var firstOctet = values.Length == 1 ? values[0] >> 24 : values[0];
        return leadingPartsInRange && values[^1] < lastLimit && firstOctet != 0;
    }

    private static bool IsOctet(ulong value) => value <= 255;

    private static bool TryParseIpv4Part(string part, out ulong value)
    {
        value = 0;
        var (numberBase, digits) = NumberBaseAndDigits(part);
        if (digits.Length == 0 || digits.Length > 11)
        {
            return false;
        }

        foreach (var character in digits)
        {
            var digit = DigitValue(character);
            if (digit >= numberBase)
            {
                return false;
            }

            value = (value * numberBase) + digit;
        }

        return true;
    }

    private static (ulong NumberBase, string Digits) NumberBaseAndDigits(string part)
    {
        if (part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return (16UL, part[2..]);
        }

        return (part.Length > 1 && part[0] == '0' ? 8UL : 10UL, part);
    }

    // A hexadecimal digit's value; any other character is 16, above every digit of every base.
    private static ulong DigitValue(char character) => char.IsAsciiHexDigit(character) ? (ulong)Convert.ToInt32(character.ToString(), 16) : 16UL;

    private static AzureStorageEndpoint MapDfs(Uri url)
    {
        // An emulator host (IP address or localhost) is used as given, never given the public suffix (EXT-085).
        var host = IsPathStyleHost(url) ? url.Host : AccountHost(url, DfsHostSuffix);
        var fileSystem = url.UserInfo;
        if (fileSystem.Length == 0)
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.MissingFileSystem, $"The URL {url.OriginalString} names no file system before the @.");
        }

        var path = url.AbsolutePath == "/" ? string.Empty : url.AbsolutePath;
        return new AzureStorageEndpoint(host, "/" + fileSystem + path, Query(url), PortOf(url));
    }

    private static string AccountHost(Uri url, string suffix)
    {
        var host = url.Host;
        if (host.Length == 0)
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.MissingAccount, $"The URL {url.OriginalString} names no storage account.");
        }

        return host.Contains('.', StringComparison.Ordinal) ? host : host + suffix;
    }

    private static int PortOf(Uri url) => url.Port < 0 ? AzureStorageEndpoint.DefaultPort : url.Port;

    private static string Query(Uri url) => url.Query.TrimStart('?');
}
