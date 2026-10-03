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

    /// <summary>Exit 55, a request the socket filter could not send because the peer reset the connection.</summary>
    public const string SendConnectionReset = "Send failure: Connection was reset";

    /// <summary>Exit 55, a request that could not be sent for any other reason.</summary>
    public const string SendFailed = "Failed sending data to the peer";

    /// <summary>Exit 56, a reply the socket filter could not receive because the peer reset the connection.</summary>
    public const string ReceiveConnectionReset = "Recv failure: Connection was reset";

    /// <summary>Exit 56, a message whose byte count runs past its frame, or a reply that could not be received for any other reason.</summary>
    public const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>Exit 56, a read response whose data runs past the bytes received.</summary>
    public const string InvalidInputPacket = "Invalid input packet";

    /// <summary>Exit 63, a session setup, tree connect or open whose bytes would pass 1024.</summary>
    public const string MessageTooLarge = "Maximum file size exceeded";

    /// <summary>Exit 67, no user given, or a session setup response with an error status.</summary>
    public const string LoginDenied = "Login denied";

    /// <summary>Exit 8, an open response giving the file a negative size, or file data arriving under <c>-I</c>.</summary>
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
    /// Exit 63, a download that delivered all the bytes <c>--max-filesize</c> allows with more
    /// still arriving: <c>lib/sendf.c</c>'s <c>cw_download_write</c> at <c>curl-8_21_0</c>.
    /// </summary>
    /// <param name="maxFileSize">The limit.</param>
    /// <param name="written">The bytes written before the download stopped.</param>
    /// <returns>curl's message.</returns>
    public static string MaxFileSizeExceeded(long maxFileSize, long written) =>
        string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({maxFileSize}) with {written} bytes");

    /// <summary>
    /// The texts above that are <c>curl_easy_strerror</c>'s for their exit code, which curl
    /// prints without a <c>failf</c> and so without a <c>-v</c> line of their own.
    /// </summary>
    private static readonly FrozenSet<string> StrerrorTexts = FrozenSet.Create(
        StringComparer.Ordinal,
        UrlMalformat,
        NegotiateFailed,
        UploadFailed,
        SendFailed,
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
