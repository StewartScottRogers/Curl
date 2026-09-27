namespace Curl.Console;

/// <summary>
/// curl 8.21.0's <c>Warning: Failed to set filetime &lt;seconds&gt; on outfile: CreateFile failed: GetLastError 0x&lt;code&gt;</c>
/// line, printed when <c>-R</c>/<c>--remote-time</c> cannot stamp the <c>-o</c> file and
/// <c>-s</c> was not given.
/// </summary>
/// <remarks>
/// <para>
/// Measured with curl 8.21.0 (Windows, Schannel build) on 2026-09-26:
/// <c>curl -R -z "1 Jan 2030" -o out2.txt file:///Z:/tmp/src.txt</c>, with no
/// <c>out2.txt</c>, exits 0 and prints this line for error 2. It is longer than curl's
/// default 79-column terminal, so curl wraps it after <c>failed: </c> into two
/// <c>Warning:</c> lines, the first keeping its trailing space;
/// <see cref="CurlCommandRunner" /> wraps it the same way with
/// <see cref="WarningLineWrapper" /> as it writes it.
/// </para>
/// <para>
/// The line is the Windows one on every platform. Upstream's POSIX build reports the
/// failure of <c>utimes</c> with <c>strerror</c> in a form this repository has not measured,
/// so rather than guess its text the measured form is kept everywhere.
/// </para>
/// <para>
/// .NET's <see cref="File.SetLastWriteTimeUtc(string, DateTime)" /> opens and stamps the
/// file in one call and does not say which step failed, so the line always names
/// <c>CreateFile</c>, the step that fails for a missing or refused file.
/// </para>
/// </remarks>
internal static class RemoteTimeFailureWarning
{
    /// <summary>
    /// Builds the warning line, without a line terminator.
    /// </summary>
    /// <param name="sourceLastWriteTimeUtc">
    /// The source's time that could not be set; printed as Unix seconds, truncated to whole
    /// seconds as curl's <c>time_t</c> is.
    /// </param>
    /// <param name="errorCode">The Win32 error code, printed as <c>0x</c> and eight lowercase hex digits.</param>
    /// <returns>The warning line.</returns>
    internal static string For(DateTimeOffset sourceLastWriteTimeUtc, int errorCode) =>
        $"Warning: Failed to set filetime {sourceLastWriteTimeUtc.ToUnixTimeSeconds()} on outfile: CreateFile failed: GetLastError 0x{errorCode:x8}";
}
