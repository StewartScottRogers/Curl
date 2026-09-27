namespace Curl.Protocol.Ftp;

/// <summary>
/// One complete reply read from an FTP control connection: its three-digit code and the
/// text of its last line, code included.
/// </summary>
/// <param name="Code">The reply code, such as 220 or 550.</param>
/// <param name="LastLine">
/// The last line of the reply without its line end, such as
/// <c>229 Entering Extended Passive Mode (|||40000|)</c>; a multi-line reply's last line
/// is the one that starts with the code and a space.
/// </param>
internal sealed record FtpReply(int Code, string LastLine)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Code" /> is a 2xx completion.
    /// </summary>
    public bool IsCompletion => Code / 100 == 2;
}
