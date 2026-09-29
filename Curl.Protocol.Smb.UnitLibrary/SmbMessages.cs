namespace Curl.Protocol.Smb;

/// <summary>
/// The error text curl 8.21.0 prints for each SMB failure. Where <c>lib/smb.c</c> calls
/// no <c>failf</c>, the text is <c>curl_easy_strerror</c>'s for the exit code, as measured.
/// </summary>
internal static class SmbMessages
{
    /// <summary>Exit 3, a path with a control character in it.</summary>
    public const string UrlMalformat = "URL using bad/illegal format or missing URL";

    /// <summary>Exit 3, a path with no share in it.</summary>
    public const string MissingShare = "missing share in URL path for SMB";

    /// <summary>Exit 7, a negotiate response that is short or carries an error status.</summary>
    public const string NegotiateFailed = "Could not connect to server";

    /// <summary>Exit 56, a message whose byte count runs past its frame.</summary>
    public const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>Exit 63, a session setup whose bytes would pass 1024.</summary>
    public const string SetupTooLarge = "Maximum file size exceeded";

    /// <summary>Exit 67, no user given, or a session setup response with an error status.</summary>
    public const string LoginDenied = "Login denied";
}
