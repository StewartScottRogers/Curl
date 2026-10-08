using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

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

    /// <summary>
    /// The exit 8 message for a reply line holding a NUL byte, as curl 8.21.0's
    /// <c>Curl_pp_readresp</c> fails it (BL-1117).
    /// </summary>
    internal const string NulByteInReplyLine = "Nul byte in server response line";

    /// <summary>The exit 28 message for a <c>421</c> reply before the data transfer.</summary>
    internal const string TimeoutReached = "Timeout was reached";

    /// <summary>
    /// The exit 28 message for <c>--connect-timeout</c> passing after the TCP connect, while
    /// the greeting, the login or <c>PWD</c> is still unanswered (BL-512).
    /// </summary>
    /// <param name="milliseconds">The time since the request started.</param>
    /// <returns>The message curl prints.</returns>
    internal static string OperationTimedOut(long milliseconds) =>
        string.Create(CultureInfo.InvariantCulture, $"Operation timed out after {milliseconds} milliseconds with 0 bytes received");

    /// <summary>The exit 28 message for a <c>421</c> reply that ends the data transfer.</summary>
    internal const string ControlConnectionLooksDead = "control connection looks dead";

    /// <summary>The <c>-v</c> line for an if-modified-since <c>-z</c> the <c>MDTM</c> time fails (BL-637).</summary>
    internal const string NotNewEnough = "The requested document is not new enough";

    /// <summary>The <c>-v</c> line for an if-unmodified-since <c>-z</c> the <c>MDTM</c> time fails (BL-637).</summary>
    internal const string NotOldEnough = "The requested document is not old enough";

    /// <summary>The <c>-v</c> line for a <c>-z</c> that cannot be applied, the time or the condition being unknown (BL-637).</summary>
    internal const string SkippingTimeComparison = "Skipping time comparison";

    /// <summary>The <c>-v</c> line for <c>MDTM</c> answered with <c>550</c>; the transfer goes on (BL-637).</summary>
    internal const string ModificationTimeRefused = "MDTM failed: file does not exist or permission problem, continuing";

    /// <summary>The <c>-v</c> line for <c>MDTM</c> answered with neither <c>213</c> nor <c>550</c>; the transfer goes on (BL-637).</summary>
    internal const string UnsupportedModificationTimeReply = "unsupported MDTM reply format";

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0 writes for a reply to <c>MDTM</c>: none for a <c>213</c>,
    /// <see cref="ModificationTimeRefused" /> for a <c>550</c> and
    /// <see cref="UnsupportedModificationTimeReply" /> for anything else (BL-637).
    /// </summary>
    /// <param name="code">The reply's code.</param>
    /// <returns>The line, or <see langword="null" /> for none.</returns>
    internal static string? ModificationTimeReply(int code) => code switch
    {
        213 => null,
        550 => ModificationTimeRefused,
        _ => UnsupportedModificationTimeReply,
    };

    /// <summary>
    /// The exit 55 message for a failed command or upload write with no socket error inside:
    /// curl 8.21.0's <c>curl_easy_strerror(CURLE_SEND_ERROR)</c>.
    /// </summary>
    internal const string SendFailedToPeer = "Failed sending data to the peer";

    /// <summary>
    /// The exit 55 message for a write that threw <paramref name="exception" />: curl 8.21.0's
    /// <c>Send failure: &lt;words&gt;</c> from <see cref="CurlSocketErrorText" /> when the
    /// exception directly wraps a <see cref="SocketException" />, otherwise
    /// <see cref="SendFailedToPeer" />.
    /// </summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The message.</returns>
    internal static string SendFailed(IOException exception) =>
        exception.InnerException is SocketException
            ? CurlSocketErrorText.SendFailure(exception)!
            : SendFailedToPeer;

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0's <c>ftp_readresp</c> writes for a <c>421</c> reply,
    /// before it fails the transfer with exit 28.
    /// </summary>
    internal const string Got421Timeout = "We got a 421 - timeout";

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0's <c>ftp_state_port_resp</c> writes when <c>EPRT</c> is
    /// refused and <c>PORT</c> follows.
    /// </summary>
    internal const string DisablingEprt = "disabling EPRT usage";

    /// <summary>
    /// The exit 56 message for a data connection that failed mid-transfer with no socket error
    /// inside: curl 8.21.0's <c>curl_easy_strerror(CURLE_RECV_ERROR)</c>.
    /// </summary>
    internal const string ReceiveFailedFromPeer = "Failure when receiving data from the peer";

    /// <summary>
    /// The exit 56 message for a data read that threw <paramref name="exception" />: curl
    /// 8.21.0's <c>Recv failure: &lt;words&gt;</c> from <see cref="CurlSocketErrorText" /> when
    /// the exception directly wraps a <see cref="SocketException" />, otherwise
    /// <see cref="ReceiveFailedFromPeer" />.
    /// </summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The message.</returns>
    internal static string ReceiveFailed(IOException exception) =>
        exception.InnerException is SocketException
            ? CurlSocketErrorText.ReceiveFailure(exception)!
            : ReceiveFailedFromPeer;

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

    /// <summary>
    /// The exit 81 message, and its <c>-v</c> line, for a <c>CCC</c> answered below 500 whose
    /// TLS shutdown failed, as it always does on curl 8.21.0's Schannel build (BL-636).
    /// </summary>
    internal const string ClearCommandChannelFailed = "Failed to clear the command channel (CCC)";

    /// <summary>The exit 67 message for a <c>332</c> reply to <c>USER</c> or <c>PASS</c> without <c>--ftp-account</c>.</summary>
    internal const string AccountRequested = "ACCT requested but none available";

    /// <summary>The exit 9 message for a <c>CWD</c> the server refused.</summary>
    internal const string ChangeDirectoryDenied = "Server denied you to change to the given directory";

    /// <summary>
    /// The exit 13 message for a <c>229</c> reply with no <c>(</c>, or whose <c>(</c> is not
    /// followed by three repeats of one delimiter and a digit.
    /// </summary>
    internal const string WeirdEpsvReply = "Weirdly formatted EPSV reply";

    /// <summary>
    /// The exit 13 message for a <c>229</c> reply whose port, after <c>(</c> and three
    /// delimiters, is above 65535 or is not followed by the delimiter (BL-1240).
    /// </summary>
    internal const string IllegalEpsvPort = "Illegal port number in EPSV reply";

    /// <summary>The exit 14 message for a <c>227</c> reply with no port curl can read.</summary>
    internal const string Weird227Reply = "Could not interpret the 227-response";

    /// <summary>The exit 8 message for a <c>257</c> reply whose quoted directory never ends.</summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>The exit 17 message for a <c>TYPE</c> the server refused.</summary>
    internal const string CouldNotSetType = "Could not set desired mode";

    /// <summary>The exit 78 message for a <c>550</c> reply to <c>SIZE</c>.</summary>
    internal const string FileDoesNotExist = "The file does not exist";

    /// <summary>
    /// The exit 63 message for a <c>SIZE</c> count larger than <c>--max-filesize</c>, which
    /// curl 8.21.0 reports before <c>REST</c> or <c>RETR</c> (BL-638).
    /// </summary>
    internal const string MaxFileSizeExceeded = "Maximum file size exceeded";

    /// <summary>
    /// The exit 63 message for a download whose size was not known up front and that
    /// delivered all <paramref name="maxFileSize" /> bytes <c>--max-filesize</c> allows with
    /// more still arriving, measured against curl 8.21.0:
    /// <c>Exceeded the maximum allowed file size (5) with 5 bytes</c> (BL-638).
    /// </summary>
    /// <param name="maxFileSize">The limit.</param>
    /// <param name="delivered">The bytes written before the transfer stopped.</param>
    /// <returns>The message to report.</returns>
    internal static string MaxFileSizeExceededWhileReading(long maxFileSize, long delivered) =>
        Format($"Exceeded the maximum allowed file size ({maxFileSize}) with {delivered} bytes");

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

    /// <summary>
    /// curl 8.21.0's <c>-v</c> line when an upload of a known size ends, on a failure that
    /// leaves the control connection usable, without its bytes all sent (BL-1395).
    /// </summary>
    /// <param name="bytesSent">The bytes written to the data connection.</param>
    /// <param name="uploadSize">The size of the upload.</param>
    /// <returns>The line, such as <c>Uploaded unaligned file size (0 out of 92 bytes)</c>.</returns>
    internal static string UploadedUnalignedFileSize(long bytesSent, long uploadSize) =>
        Format($"Uploaded unaligned file size ({bytesSent} out of {uploadSize} bytes)");

    /// <summary>The exit 23 message for an <c>-I</c> header line the header output refused.</summary>
    /// <param name="passed">The length of the refused line.</param>
    /// <returns>The message to report.</returns>
    internal static string HeaderWriteFailed(int passed) =>
        Format($"client returned ERROR on write of {passed} bytes");

    /// <summary>The exit 8 message for a greeting other than <c>220</c>.</summary>
    /// <param name="code">The greeting's code.</param>
    /// <returns>The message to report.</returns>
    internal static string UnexpectedGreeting(int code) =>
        Format($"Got a {code:D3} ftp-server response when 220 was expected");

    /// <summary>The exit 67 message for a refused <c>USER</c> or <c>PASS</c>.</summary>
    /// <param name="code">The refusing reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string AccessDenied(int code) => Format($"Access denied: {code:D3}");

    /// <summary>The exit 11 message for an <c>ACCT</c> answered with anything but <c>230</c>.</summary>
    /// <param name="code">The refusing reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string AccountRejected(int code) => Format($"ACCT rejected by server: {code:D3}");

    /// <summary>The exit 84 message for a <c>PRET</c> answered with anything but <c>200</c>.</summary>
    /// <param name="code">The refusing reply's code.</param>
    /// <returns>The message to report.</returns>
    internal static string PretNotAccepted(int code) => Format($"PRET command not accepted: {code:D3}");

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
    internal static string BadPassiveReply(int code) => Format($"Bad PASV/EPSV response: {code:D3}");

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
    internal static string RetrieveRefused(int code) => Format($"RETR response: {code:D3}");

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

    /// <summary>The <c>-v</c> line after the first <c>EPSV</c> or <c>PASV</c> is sent (BL-931).</summary>
    internal const string ConnectDataStreamPassively = "Connect data stream passively";

    /// <summary>The <c>-v</c> line after <c>EPRT</c> or <c>PORT</c> is accepted (BL-931).</summary>
    internal const string ConnectDataStreamActively = "Connect data stream actively";

    /// <summary>The <c>-v</c> line after an <c>EPSV</c> answered with anything but <c>229</c>, before <c>PASV</c> (BL-931).</summary>
    internal const string EpsvFailed = "Failed EPSV attempt. Disabling EPSV";

    /// <summary>The exit 8 message for an <c>EPSV</c> answered with anything but <c>229</c> over IPv6 (BL-903).</summary>
    internal const string EpsvFailedOverIPv6 = "Failed EPSV attempt, exiting";

    /// <summary>The first of the <c>-v</c> lines before an active-mode data connection is accepted (BL-931).</summary>
    internal const string DataConnectionNotAvailable = "Data conn was not available immediately";

    /// <summary>The second of the <c>-v</c> lines before an active-mode data connection is accepted (BL-931).</summary>
    internal const string ReadyToAccept = "Ready to accept data connection from server";

    /// <summary>The <c>-v</c> line once the server's active-mode data connection is accepted (BL-931).</summary>
    internal const string ConnectionAccepted = "Connection accepted from server";

    /// <summary>The <c>-v</c> line after the <c>ABOR</c> that ends a ranged download (BL-931).</summary>
    internal const string PartialDownloadClosing = "partial download completed, closing connection";

    /// <summary>
    /// The <c>-v</c> line for a <c>227</c> address set aside for the control connection's
    /// host, as without <c>--no-ftp-skip-pasv-ip</c> (BL-931).
    /// </summary>
    /// <param name="passiveAddress">The address the <c>227</c> reply named.</param>
    /// <param name="urlHost">The URL's host, as given.</param>
    /// <returns>The line, such as <c>Skip 127.0.0.1 for data connection, reuse localhost instead</c>.</returns>
    internal static string SkipPassiveAddress(string passiveAddress, string urlHost) =>
        "Skip " + passiveAddress + " for data connection, reuse " + urlHost + " instead";

    /// <summary>The <c>-v</c> line before a passive data connection is dialled (BL-931).</summary>
    /// <param name="host">The control connection's address, or the <c>227</c> address under <c>--no-ftp-skip-pasv-ip</c>.</param>
    /// <param name="port">The data port.</param>
    /// <returns>The line, such as <c>Connecting to 127.0.0.1 port 55801</c>.</returns>
    internal static string ConnectingTo(string host, int port) => Format($"Connecting to {host} port {port}");

    /// <summary>The <c>-v</c> line after a download's <c>150</c>: the range's byte count, or -1 for none (BL-931).</summary>
    /// <param name="maxDownload">The range's byte count, or <see langword="null" /> for none.</param>
    /// <returns>The line, such as <c>Maxdownload = -1</c>.</returns>
    internal static string MaxDownload(long? maxDownload) => Format($"Maxdownload = {maxDownload ?? -1}");

    /// <summary>The <c>-v</c> line after a file download's <c>150</c>: the bytes expected, or -1 when unknown (BL-931).</summary>
    /// <param name="size">The bytes expected, or <see langword="null" /> when unknown.</param>
    /// <returns>The line, such as <c>Getting file with size: 11</c>.</returns>
    internal static string GettingFile(long? size) => Format($"Getting file with size: {size ?? -1}");

    /// <summary>The <c>-v</c> line before a download's <c>REST</c> (BL-931).</summary>
    /// <param name="offset">The offset <c>REST</c> sends.</param>
    /// <returns>The line, such as <c>Instructs server to resume from offset 3</c>.</returns>
    internal static string ResumingFrom(long offset) => Format($"Instructs server to resume from offset {offset}");

    /// <summary>The <c>-v</c> line for the server's active-mode data connection, accepted (BL-931).</summary>
    /// <param name="remote">The server's end.</param>
    /// <param name="local">The listening end.</param>
    /// <returns>The line, with curl's trailing space.</returns>
    internal static string SecondConnectionEstablished(IPEndPoint remote, IPEndPoint local) =>
        Format($"Established 2nd connection to {remote.Address} ({remote.Address} port {remote.Port}) from {local.Address} port {local.Port} ");

    /// <summary>The <c>-v</c> line once an upload has been written and its data connection closed (BL-931).</summary>
    /// <param name="bytesSent">The bytes written.</param>
    /// <returns>The line, such as <c>upload completely sent off: 14 bytes</c>.</returns>
    internal static string UploadSent(long bytesSent) => Format($"upload completely sent off: {bytesSent} bytes");

    /// <summary>The <c>-v</c> line for a <c>257</c> reply to <c>PWD</c> that names no directory (BL-945).</summary>
    internal const string FailedToFigureOutPath = "Failed to figure out path";

    /// <summary>
    /// The <c>-v</c> line before the first command after login when the URL path's
    /// directory is the entry directory, so no <c>CWD</c> is needed (BL-945).
    /// </summary>
    internal const string SamePathAsPreviousTransfer = "Request has same path as previous transfer";

    /// <summary>The exit 70 message for an end-of-transfer reply of <c>552</c> (BL-1118).</summary>
    internal const string StorageAllocationExceeded = "Exceeded storage allocation";

    /// <summary>The <c>-v</c> line for a <c>-C</c> download whose offset leaves nothing to fetch (BL-1118).</summary>
    internal const string AlreadyCompletelyDownloaded = "File already completely downloaded";

    /// <summary>The <c>-v</c> line for a <c>-C</c> upload whose offset covers the whole source (BL-1118).</summary>
    internal const string AlreadyCompletelyUploaded = "File already completely uploaded";

    /// <summary>The <c>-v</c> line for a <c>-C</c> download whose <c>SIZE</c> gave no count (BL-1118).</summary>
    internal const string SizeNotSupported = "ftp server does not support SIZE";

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0 writes for a reply to <c>PWD</c> (BL-945): the
    /// directory it named, <see cref="FailedToFigureOutPath" /> for a <c>257</c> that names
    /// none, and none for any other reply.
    /// </summary>
    /// <param name="code">The reply's code.</param>
    /// <param name="entryPath">The directory the reply named, or <see langword="null" />.</param>
    /// <returns>The line, such as <c>Entry path is '/'</c>, or <see langword="null" /> for none.</returns>
    internal static string? EntryPathReply(int code, string? entryPath) =>
        entryPath is not null ? "Entry path is '" + entryPath + "'"
        : code == 257 ? FailedToFigureOutPath
        : null;

    /// <summary>The <c>-v</c> line before the end-of-transfer reply is read, or <c>ABOR</c> sent (BL-931).</summary>
    /// <param name="directory">The URL path's directories, each followed by <c>/</c>; empty for none.</param>
    /// <returns>The line, such as <c>Remembering we are in directory "dir/"</c>.</returns>
    internal static string RememberingDirectory(string directory) => "Remembering we are in directory \"" + directory + "\"";

    /// <summary>The <c>-v</c> line for a control connection kept after a transfer that ended with <c>226</c> (BL-931).</summary>
    /// <param name="connectionNumber">The control connection's number.</param>
    /// <param name="host">The URL's host.</param>
    /// <param name="port">The control connection's port.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:21 left intact</c>.</returns>
    internal static string ConnectionLeftIntact(long connectionNumber, string host, int port) =>
        Format($"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>The <c>-v</c> line after <see cref="PartialDownloadClosing" /> (BL-931).</summary>
    /// <param name="connectionNumber">The control connection's number.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDownConnection(long connectionNumber) => Format($"shutting down connection #{connectionNumber}");

    private static string Format(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);
}
