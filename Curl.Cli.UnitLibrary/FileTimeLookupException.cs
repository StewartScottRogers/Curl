namespace Curl.Cli;

/// <summary>
/// A modification-time lookup that failed in a named Windows call, carrying that call's
/// <c>GetLastError</c> code, so the filetime warning can name the call as curl's Windows build
/// does: <c>CreateFile failed: GetLastError 0x%08x</c> or <c>GetFileTime failed: GetLastError 0x%08x</c>.
/// </summary>
public sealed class FileTimeLookupException : IOException
{
    private const int Win32HResultFacility = unchecked((int)0x80070000);

    /// <summary>
    /// Creates the failure of <paramref name="failedCall"/> with <paramref name="errorCode"/>.
    /// </summary>
    /// <param name="failedCall">The Windows call that failed, as curl names it: <c>CreateFile</c> or <c>GetFileTime</c>.</param>
    /// <param name="errorCode">The call's <c>GetLastError</c> code.</param>
    public FileTimeLookupException(string failedCall, int errorCode)
        : base($"{failedCall} failed: GetLastError 0x{errorCode:x8}", Win32HResultFacility | (errorCode & 0xFFFF))
    {
        FailedCall = failedCall;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the Windows call that failed: <c>CreateFile</c> or <c>GetFileTime</c>.</summary>
    public string FailedCall { get; }

    /// <summary>Gets the call's <c>GetLastError</c> code.</summary>
    public int ErrorCode { get; }
}
