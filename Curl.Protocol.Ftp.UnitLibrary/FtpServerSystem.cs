namespace Curl.Protocol.Ftp;

/// <summary>
/// Reads the server system a reply to <c>SYST</c> names, as curl 8.21.0's
/// <c>ftp_state_syst_resp</c> does: only a <c>215</c> names one, as the first word after
/// the code, its space and any further spaces (BL-782).
/// </summary>
internal static class FtpServerSystem
{
    private const string Os400 = "OS/400";

    /// <summary>
    /// Tells whether <paramref name="syst" /> names <c>OS/400</c>, in any letter case, the
    /// one system curl changes its conversation for.
    /// </summary>
    /// <param name="syst">The reply to <c>SYST</c>.</param>
    /// <returns><see langword="true" /> for a <c>215</c> whose first word is <c>OS/400</c>.</returns>
    public static bool IsOs400(FtpReply syst)
    {
        if (syst.Code != 215)
        {
            return false;
        }

        string system = syst.LastLine[Math.Min(4, syst.LastLine.Length)..].TrimStart(' ');
        int end = system.IndexOf(' ');
        return string.Equals(end < 0 ? system : system[..end], Os400, StringComparison.OrdinalIgnoreCase);
    }
}
