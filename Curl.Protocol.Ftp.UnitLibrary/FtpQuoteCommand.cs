namespace Curl.Protocol.Ftp;

/// <summary>
/// One <c>-Q</c>/<c>--quote</c> command, its prefixes removed.
/// </summary>
/// <param name="Command">The command line sent, such as <c>DELE f.txt</c>; may be empty.</param>
/// <param name="IgnoreFailure">
/// Whether the value carried a <c>*</c> prefix, so a reply of 400 or more is read and
/// the conversation carries on.
/// </param>
internal sealed record FtpQuoteCommand(string Command, bool IgnoreFailure);
