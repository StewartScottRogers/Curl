namespace Curl.Cli;

/// <summary>
/// The IMAP system flags <c>--upload-flags</c> sets on a message it uploads with <c>APPEND</c>, one bit
/// each. curl 8.21.0 sends the set ones in this order, each as its IMAP name (<c>\Answered</c>,
/// <c>\Deleted</c>, <c>\Draft</c>, <c>\Flagged</c>, <c>\Seen</c>), and starts from
/// <see cref="Seen"/> alone (measured 2026-09-28, BL-535 Notes).
/// </summary>
[Flags]
public enum ImapUploadFlags
{
    /// <summary>No flag: <c>APPEND</c> carries no flag list.</summary>
    None = 0,

    /// <summary><c>\Answered</c>, named <c>answered</c> in <c>--upload-flags</c>.</summary>
    Answered = 1,

    /// <summary><c>\Deleted</c>, named <c>deleted</c> in <c>--upload-flags</c>.</summary>
    Deleted = 2,

    /// <summary><c>\Draft</c>, named <c>draft</c> in <c>--upload-flags</c>.</summary>
    Draft = 4,

    /// <summary><c>\Flagged</c>, named <c>flagged</c> in <c>--upload-flags</c>.</summary>
    Flagged = 8,

    /// <summary><c>\Seen</c>, named <c>seen</c> in <c>--upload-flags</c>; set unless a <c>-seen</c> clears it.</summary>
    Seen = 16,
}
