namespace Curl.Console;

/// <summary>
/// A <c>--create-dirs</c> directory that could not be created, and why, which picks curl
/// 8.21.0's message for it (upstream <c>src/tool_dirhie.c</c>, <c>show_dir_errno</c>, BL-1433).
/// </summary>
/// <param name="Directory">The directory, as a leading part of the output path.</param>
/// <param name="ErrorNumber">The <c>errno</c> of the failed <c>mkdir</c>.</param>
internal sealed record DirectoryCreationFailure(string Directory, int ErrorNumber)
{
    /// <summary>
    /// Gives curl's message, without a line terminator: <c>curl: The directory name &lt;dir&gt; is too long</c>
    /// for <c>ENAMETOOLONG</c>, <c>curl: &lt;dir&gt; resides on a read-only file system</c> for
    /// <c>EROFS</c>, <c>curl: No space left on the file system that would contain the directory &lt;dir&gt;</c>
    /// for <c>ENOSPC</c>, <c>curl: Cannot create directory &lt;dir&gt; because you exceeded your quota</c>
    /// for <c>EDQUOT</c>, and <c>curl: Error creating directory &lt;dir&gt;</c> for anything else.
    /// </summary>
    /// <param name="errorNumbers">The platform's C runtime numbers.</param>
    /// <returns>The message.</returns>
    internal string Message(CRuntimeErrorNumbers errorNumbers) =>
        ErrorNumber == CRuntimeErrorNumbers.ReadOnlyFileSystem ? $"curl: {Directory} resides on a read-only file system"
        : ErrorNumber == CRuntimeErrorNumbers.NoSpaceLeft ? $"curl: No space left on the file system that would contain the directory {Directory}"
        : ErrorNumber == errorNumbers.NameTooLong ? $"curl: The directory name {Directory} is too long"
        : ErrorNumber == errorNumbers.QuotaExceeded ? $"curl: Cannot create directory {Directory} because you exceeded your quota"
        : $"curl: Error creating directory {Directory}";
}
