namespace Curl.Console;

/// <summary>
/// What became of a <c>--remove-on-error</c> output file, as curl 8.21.0's
/// <c>post_per_transfer</c> tells the three cases apart (ADR-0332).
/// </summary>
internal enum OutputFileRemoval
{
    /// <summary>The file was a regular file and is gone: curl's <c>Note: Removed output file</c>.</summary>
    Removed,

    /// <summary>The file was there to delete and could not be: curl's <c>Warning: Failed removing</c>.</summary>
    Failed,

    /// <summary>
    /// <c>stat</c> found no regular file there, such as <c>/dev/null</c>, so nothing was deleted:
    /// curl's <c>Warning: Skipping removal; not a regular file</c>. Only off Windows, where curl's
    /// <c>stat</c> is measured to tell one apart.
    /// </summary>
    NotRegularFile,
}
