namespace Curl.Protocol.Pop3;

/// <summary>
/// Ends a <see cref="Pop3Session" /> when a response line holds a NUL byte, which the
/// session reports as exit 8, as curl 8.21.0's <c>Curl_pp_readresp</c> does (BL-1120).
/// </summary>
internal sealed class Pop3NulByteInLineException() : Exception(Pop3SessionMessages.NulByteInLine);
