namespace Curl.Console;

/// <summary>
/// curl 8.21.0's <c>Warning: Failed to set filetime ...</c> line, printed when
/// <c>-R</c>/<c>--remote-time</c> cannot stamp the <c>-o</c> file and <c>-s</c> was not given:
/// the Windows build's <c>GetLastError</c> forms and the POSIX build's <c>strerror</c> form.
/// </summary>
/// <remarks>
/// <para>
/// Windows measured with curl 8.21.0 (Schannel build) on 2026-09-26:
/// <c>curl -R -z "1 Jan 2030" -o out2.txt file:///Z:/tmp/src.txt</c>, with no
/// <c>out2.txt</c>, exits 0 and prints the <c>CreateFile failed</c> line for error 2. It is
/// longer than curl's default 79-column terminal, so curl wraps it after <c>failed: </c> into
/// two <c>Warning:</c> lines, the first keeping its trailing space;
/// <see cref="CurlCommandRunner" /> wraps it the same way with
/// <see cref="WarningLineWrapper" /> as it writes it.
/// </para>
/// <para>
/// The POSIX form is upstream's <c>src/tool_filetime.c</c> (tag <c>curl-8_21_0</c>, lines
/// 128-148): <c>warnf("Failed to set filetime %" CURL_FORMAT_CURL_OFF_T " on '%s': %s",
/// filetime, filename, curlx_strerror(errno))</c> after <c>utimes</c> fails (BL-1433).
/// </para>
/// <para>
/// .NET's <see cref="File.SetLastWriteTimeUtc(string, DateTime)" /> opens and stamps the
/// file in one call and does not say which step failed, so on Windows the runner always
/// prints <see cref="ForWindowsOpen" />'s <c>CreateFile</c> line, the step that fails for a
/// missing or refused file; <see cref="ForWindowsStamp" /> is the line for a
/// <c>SetFileTime</c> that fails once the file is open.
/// </para>
/// </remarks>
internal static class RemoteTimeFailureWarning
{
    /// <summary>
    /// Builds the Windows line for a file that could not be opened, without a line terminator.
    /// </summary>
    /// <param name="sourceLastWriteUnixSeconds">
    /// The source's time that could not be set, in Unix seconds as curl's <c>time_t</c> holds it.
    /// </param>
    /// <param name="errorCode">The Win32 error code, printed as <c>0x</c> and eight lowercase hex digits.</param>
    /// <returns>The warning line.</returns>
    internal static string ForWindowsOpen(long sourceLastWriteUnixSeconds, int errorCode) =>
        $"Warning: Failed to set filetime {sourceLastWriteUnixSeconds} on outfile: CreateFile failed: GetLastError 0x{errorCode:x8}";

    /// <summary>
    /// Builds the Windows line for a file that opened but whose time could not be set, without
    /// a line terminator.
    /// </summary>
    /// <param name="sourceLastWriteUnixSeconds">The source's time that could not be set, in Unix seconds.</param>
    /// <param name="errorCode">The Win32 error code, printed as <c>0x</c> and eight lowercase hex digits.</param>
    /// <returns>The warning line.</returns>
    internal static string ForWindowsStamp(long sourceLastWriteUnixSeconds, int errorCode) =>
        $"Warning: Failed to set filetime {sourceLastWriteUnixSeconds} on outfile: SetFileTime failed: GetLastError 0x{errorCode:x8}";

    /// <summary>
    /// Builds the POSIX line, without a line terminator.
    /// </summary>
    /// <param name="sourceLastWriteUnixSeconds">The source's time that could not be set, in Unix seconds.</param>
    /// <param name="outputFile">The <c>-o</c> file, as given.</param>
    /// <param name="errorNumber"><c>utimes</c>'s <c>errno</c>.</param>
    /// <param name="errorNumbers">The platform's C runtime, whose <c>strerror</c> words <paramref name="errorNumber" />.</param>
    /// <returns>The warning line.</returns>
    internal static string ForPosix(long sourceLastWriteUnixSeconds, string outputFile, int errorNumber, CRuntimeErrorNumbers errorNumbers) =>
        $"Warning: Failed to set filetime {sourceLastWriteUnixSeconds} on '{outputFile}': {errorNumbers.Describe(errorNumber)}";
}
