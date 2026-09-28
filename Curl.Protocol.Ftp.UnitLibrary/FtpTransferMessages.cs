using System.Globalization;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Every failure message the <c>ftp</c> scheme reports, as curl 8.21.0 words it, each
/// measured against a loopback server with <c>Record-CurlExchange.ps1 -Ftp</c> (BL-431).
/// </summary>
internal static class FtpTransferMessages
{
    /// <summary>The exit 56 message for a control connection closed or failed mid-reply.</summary>
    internal const string ResponseReadingFailed = "response reading failed (errno: 0)";

    /// <summary>
    /// The exit 100 message for a reply line of 65536 bytes or more: curl's text for the
    /// code, printed with no <c>failf</c> of its own.
    /// </summary>
    internal const string ReplyLineTooLarge = "A value or data field grew larger than allowed";

    /// <summary>The exit 28 message for a <c>421</c> reply before the data transfer.</summary>
    internal const string TimeoutReached = "Timeout was reached";

    /// <summary>The exit 28 message for a <c>421</c> reply that ends the data transfer.</summary>
    internal const string ControlConnectionLooksDead = "control connection looks dead";

    /// <summary>The exit 55 message for a command that could not be sent.</summary>
    internal const string SendFailed = "Failure when sending data to the peer";

    /// <summary>The exit 56 message for a data connection that failed mid-transfer.</summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>The exit 3 message for a path that decodes to a control character.</summary>
    internal const string PathHasControlCharacters = "path contains control characters";

    /// <summary>The exit 3 message for a <c>-T</c> upload to a URL ending in <c>/</c> (BL-439).</summary>
    internal const string UploadWithoutFileName = "Uploading to a URL without a filename";

    /// <summary>
    /// The exit 30 message for an active-mode port the server refused, with <c>EPRT</c> and
    /// then <c>PORT</c> (BL-437).
    /// </summary>
    internal const string FailedToDoPort = "Failed to do PORT";

    /// <summary>
    /// What sets curl 8.21.0's <c>-v</c> line for a bind on a <c>-P</c> address that is not
    /// local, <c>bind(port=N) on non-local address failed: reason</c>, apart from its exit 30
    /// message, <c>bind(port=N) failed: reason</c>: the listener words a bind that failed
    /// with <c>EADDRNOTAVAIL</c> the first way (ADR-0107).
    /// </summary>
    internal const string NonLocalBindFailed = " on non-local address failed: ";

    /// <summary>
    /// Gives the exit 30 message for a failed listen: a non-local bind's line reworded as
    /// curl 8.21.0's <c>bind(port=N) failed: reason</c>, any other message as it is (BL-464).
    /// </summary>
    /// <param name="listenMessage">The listener's message.</param>
    /// <returns>The message curl prints.</returns>
    internal static string BindFailed(string listenMessage) =>
        listenMessage.Replace(NonLocalBindFailed, " failed: ", StringComparison.Ordinal);

    /// <summary>
    /// The exit 12 message for an active-mode data connection the server did not open
    /// within curl's 60-second accept timeout (BL-437).
    /// </summary>
    internal const string AcceptTimeout = "Accept timeout occurred while waiting server connect";

    /// <summary>
    /// The exit 64 message for an <c>AUTH</c> refused under <c>--ssl-reqd</c> or
    /// <c>--ftp-ssl-control</c>, or a <c>PROT P</c> refused under <c>--ssl-reqd</c> (BL-437).
    /// </summary>
    internal const string RequestedSslLevelFailed = "Requested SSL level failed";

    /// <summary>The exit 67 message for a <c>332</c> reply to <c>PASS</c>.</summary>
    internal const string AccountRequested = "ACCT requested but none available";

    /// <summary>The exit 9 message for a <c>CWD</c> the server refused.</summary>
    internal const string ChangeDirectoryDenied = "Server denied you to change to the given directory";

    /// <summary>The exit 13 message for a <c>229</c> reply with no port curl can read.</summary>
    internal const string WeirdEpsvReply = "Weirdly formatted EPSV reply";

    /// <summary>The exit 14 message for a <c>227</c> reply with no port curl can read.</summary>
    internal const string Weird227Reply = "Could not interpret the 227-response";

    /// <summary>The exit 8 message for a <c>257</c> reply whose quoted directory never ends.</summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>The exit 17 message for a <c>TYPE</c> the server refused.</summary>
    internal const string CouldNotSetType = "Could not set desired mode";

    /// <summary>The exit 78 message for a <c>550</c> reply to <c>SIZE</c>.</summary>
    internal const string FileDoesNotExist = "The file does not exist";

    /// <summary>The exit 31 message for a <c>REST</c> answered with anything but <c>350</c>.</summary>
    internal const string CouldNotUseRest = "Could not use REST";

    /// <summary>
    /// The exit 36 message for a <c>-C</c> offset past the <c>SIZE</c> count, or a
    /// <c>-r -n</c> suffix longer than it.
    /// </summary>
    /// <param name="offset">The requested offset, negative for a suffix.</param>
    /// <param name="size">The <c>SIZE</c> count.</param>
    /// <returns>The message to report.</returns>
    internal static string OffsetBeyondFileSize(long offset, long size) =>
        Format($"Offset ({offset}) was beyond file size ({size})");

    /// <summary>
    /// The exit 18 message for a ranged download whose data connection closed short of
    /// the bytes the range and the <c>SIZE</c> count expected.
    /// </summary>
    /// <param name="missing">The bytes still expected.</param>
    /// <returns>The message to report.</returns>
    internal static string EndOfResponseWithBytesMissing(long missing) =>
        Format($"end of response with {missing} bytes missing");

    /// <summary>
    /// The exit 25 message for a <c>STOR</c> or <c>APPE</c> answered with 400 or more (BL-439).
    /// </summary>
    /// <param name="code">The reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string UploadRefused(int code) => Format($"Failed FTP upload: {code}");

    /// <summary>The exit 23 message for an <c>-I</c> header line the header output refused.</summary>
    /// <param name="passed">The length of the refused line.</param>
    /// <returns>The message to report.</returns>
    internal static string HeaderWriteFailed(int passed) =>
        Format($"client returned ERROR on write of {passed} bytes");

    /// <summary>The exit 8 message for a greeting other than <c>220</c>.</summary>
    /// <param name="code">The greeting's code.</param>
    /// <returns>The message to report.</returns>
    internal static string UnexpectedGreeting(int code) =>
        Format($"Got a {code} ftp-server response when 220 was expected");

    /// <summary>The exit 67 message for a refused <c>USER</c> or <c>PASS</c>.</summary>
    /// <param name="code">The refusing reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string AccessDenied(int code) => Format($"Access denied: {code}");

    /// <summary>
    /// The exit 6 message, and the first of two <c>-v</c> lines, for a <c>-P</c> name that
    /// does not resolve (ADR-0108).
    /// </summary>
    internal static string CouldNotResolveHost(string name) => "Could not resolve host: " + name;

    /// <summary>
    /// The second <c>-v</c> line curl 8.21.0 prints for a <c>-P</c> name that does not
    /// resolve (ADR-0108).
    /// </summary>
    internal static string PortAddressNotResolved(string name) => "failed to resolve the address provided to PORT: " + name;

    /// <summary>The exit 13 message when neither <c>EPSV</c> nor <c>PASV</c> was accepted.</summary>
    /// <param name="code">The code of the reply to <c>PASV</c>.</param>
    /// <returns>The message to report.</returns>
    internal static string BadPassiveReply(int code) => Format($"Bad PASV/EPSV response: {code}");

    /// <summary>
    /// The exit 21 message for a <c>-Q</c> command sent before the transfer, with no prefix
    /// or with <c>+</c>, answered with 400 or more (BL-436).
    /// </summary>
    /// <param name="code">The refusing reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string QuoteCommandFailed(int code) => Format($"QUOT command failed with {code}");

    /// <summary>
    /// The exit 21 message for a <c>-Q -</c> command, sent after the transfer, answered with
    /// 400 or more (BL-436).
    /// </summary>
    /// <param name="command">The command as sent, its prefixes removed.</param>
    /// <returns>The message to report.</returns>
    internal static string QuoteNotAccepted(string command) => "QUOT string not accepted: " + command;

    /// <summary>The exit 19 or 78 message for a refused <c>RETR</c>, <c>LIST</c> or <c>NLST</c>.</summary>
    /// <param name="code">The refusing reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string RetrieveRefused(int code) => Format($"RETR response: {code}");

    /// <summary>The exit 18 message for a transfer that ended with a code other than 226 or 250.</summary>
    /// <param name="code">The code of the reply that ended the transfer.</param>
    /// <returns>The message to report.</returns>
    internal static string TransferNotOk(int code) => Format($"server did not report OK, got {code}");

    /// <summary>The exit 18 message for a data connection that closed short of <c>SIZE</c>'s count.</summary>
    /// <param name="remaining">The bytes still expected.</param>
    /// <returns>The message to report.</returns>
    internal static string ClosedWithBytesRemaining(long remaining) =>
        Format($"transfer closed with {remaining} bytes remaining to read");

    /// <summary>The exit 23 message for an output that stopped accepting bytes.</summary>
    /// <param name="passed">The number of bytes offered to the output: one read's worth.</param>
    /// <param name="returned">The number of those bytes the output accepted before it failed.</param>
    /// <returns>The message to report.</returns>
    internal static string OutputWriteFailed(int passed, int returned) =>
        Format($"Failure writing output to destination, passed {passed} returned {returned}");

    private static string Format(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);
}
