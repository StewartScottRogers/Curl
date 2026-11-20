namespace Curl.Api;

/// <summary>The URL schemes to which a request body is uploaded as <c>curl -T</c> uploads it (ADR-1316).</summary>
internal static class UploadSchemes
{
    private static readonly string[] Schemes = ["ftp", "ftps", "sftp", "scp", "smb", "smbs", "smtp", "smtps", "imap", "imaps", "tftp", "telnet", "s3", "az", "kafka", "kafkas", "grpc", "grpcs", "signalr", "signalrs"];

    /// <summary>Whether a body sent to <paramref name="url"/> is an upload rather than HTTP post fields.</summary>
    /// <param name="url">The request's URL.</param>
    /// <returns><see langword="true"/> when the scheme is one curl uploads to with <c>-T</c>.</returns>
    public static bool UploadsWithBody(string url)
    {
        int colon = url.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && Schemes.Contains(url[..colon], StringComparer.OrdinalIgnoreCase);
    }
}
