namespace Curl.Cli;

/// <summary>
/// Whether a transfer takes its credentials from a netrc file, as curl 8.21.0 decides it from
/// <c>-n</c> / <c>--netrc</c>, <c>--netrc-optional</c> and <c>--netrc-file</c>.
/// </summary>
public enum NetrcUse
{
    /// <summary>None of the three options is in effect: no netrc file is read.</summary>
    Ignored = 0,

    /// <summary>
    /// <c>--netrc-optional</c> is in effect, whatever else was given: a netrc file is read when there is
    /// one, and a missing file is not an error.
    /// </summary>
    Optional,

    /// <summary>
    /// <c>-n</c> / <c>--netrc</c> or <c>--netrc-file</c> is in effect and <c>--netrc-optional</c> is not:
    /// the netrc file must be there.
    /// </summary>
    Required,
}
