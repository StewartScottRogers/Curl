using System.Collections.Frozen;
using System.Globalization;

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

    /// <summary>Exit 25, a write response that is short or carries an error status.</summary>
    public const string UploadFailed = "Upload failed (at start/before it took off)";

    /// <summary>Exit 55, an upload from a source whose size cannot be known, such as <c>-T -</c>.</summary>
    public const string UploadSizeUnknown = "SMB upload needs to know the size up front";

    /// <summary>Exit 56, a message whose byte count runs past its frame.</summary>
    public const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>Exit 56, a read response whose data runs past the bytes received.</summary>
    public const string InvalidInputPacket = "Invalid input packet";

    /// <summary>Exit 63, a session setup, tree connect or open whose bytes would pass 1024.</summary>
    public const string MessageTooLarge = "Maximum file size exceeded";

    /// <summary>Exit 67, no user given, or a session setup response with an error status.</summary>
    public const string LoginDenied = "Login denied";

    /// <summary>Exit 8, an open response giving the file a negative size.</summary>
    public const string WeirdServerReply = "Weird server reply";

    /// <summary>Exit 9, a tree connect or open refused with the DOS error <c>ERRnoaccess</c>.</summary>
    public const string RemoteAccessDenied = "Access denied to remote resource";

    /// <summary>Exit 78, a tree connect or open refused with any other status, or a short open response.</summary>
    public const string RemoteFileNotFound = "Remote file not found";

    /// <summary>Exit 23, the output refusing the bytes of a read response.</summary>
    /// <param name="passed">The bytes handed to the output.</param>
    /// <param name="returned">The bytes of them the output accepted.</param>
    /// <returns>curl's message.</returns>
    public static string OutputWriteFailed(int passed, int returned) =>
        string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {returned}");

    /// <summary>
    /// The texts above that are <c>curl_easy_strerror</c>'s for their exit code, which curl
    /// prints without a <c>failf</c> and so without a <c>-v</c> line of their own.
    /// </summary>
    private static readonly FrozenSet<string> StrerrorTexts = FrozenSet.Create(
        StringComparer.Ordinal,
        UrlMalformat,
        NegotiateFailed,
        UploadFailed,
        ReceiveFailed,
        MessageTooLarge,
        LoginDenied,
        WeirdServerReply,
        RemoteAccessDenied,
        RemoteFileNotFound);

    /// <summary>
    /// Whether curl 8.21.0 reports <paramref name="message" /> through <c>failf</c>, which
    /// also writes it as a <c>-v</c> line: every text but <c>curl_easy_strerror</c>'s
    /// (measured, BL-598 Notes).
    /// </summary>
    /// <param name="message">A failure's text.</param>
    /// <returns><see langword="true" /> when <c>-v</c> shows the text as an info line.</returns>
    public static bool IsVerboseLine(string message) => !StrerrorTexts.Contains(message);
}
