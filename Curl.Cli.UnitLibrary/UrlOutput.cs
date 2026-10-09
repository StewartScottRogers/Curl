namespace Curl.Cli;

/// <summary>
/// Where one URL's response body goes, paired the way curl 8.21.0 pairs URLs with <c>-o</c> /
/// <c>--output</c>, <c>-O</c> / <c>--remote-name</c>, <c>--out-null</c> and <c>--remote-name-all</c>: the Nth URL takes
/// the Nth output option, whatever order they were given in. A pairing is made by whichever of the two
/// comes first, and one made while <c>--remote-name-all</c> is on starts out using the remote name.
/// <see cref="FileName"/>, when set, wins over <see cref="UsesRemoteName"/>; with neither, the body
/// goes to standard output. Nothing here is opened, created or checked.
/// </summary>
/// <remarks>
/// Measured with the local curl 8.21.0 on 2026-09-26 against <c>file:///C:/Windows/win.ini</c>:
/// <c>-o x -O u1 u2</c> writes <c>x</c> and <c>win.ini</c>; <c>--remote-name-all -o x u</c> and
/// <c>--remote-name-all u -o x</c> write only <c>x</c>; <c>u --remote-name-all</c> writes to standard
/// output and <c>u1 --remote-name-all u2</c> saves only <c>u2</c>; <c>--remote-name-all --no-remote-name u</c>
/// writes to standard output.
/// </remarks>
public sealed class UrlOutput
{
    internal UrlOutput(bool usesRemoteName) => UsesRemoteName = usesRemoteName;

    /// <summary>The URL, or <see langword="null"/> for an output option no URL was given for.</summary>
    public string? Url { get; internal set; }

    /// <summary>The <c>-o</c> / <c>--output</c> file name paired with the URL; <see langword="null"/> when none was.</summary>
    public string? FileName { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the body is saved under the file name taken from the URL, asked for by
    /// <c>-O</c> / <c>--remote-name</c> or by <c>--remote-name-all</c>; ignored when <see cref="FileName"/> is set.
    /// </summary>
    public bool UsesRemoteName { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--out-null</c> (or <c>--no-out-null</c>, which curl 8.21.0 treats
    /// the same) was paired with the URL: its body, and any <c>-i</c> header lines, are thrown away,
    /// no file is created and nothing reaches standard output. <see cref="FileName"/> is then
    /// <see langword="null"/> and <see cref="UsesRemoteName"/> <see langword="false"/>, even under
    /// <c>--remote-name-all</c> (measured 2026-09-28, BL-495 Notes).
    /// </summary>
    public bool DiscardsBody { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="Url"/> was read from a <c>--url @file</c> (or <c>@-</c>), which
    /// curl 8.21.0's <c>parse_url</c> adds with globbing off: its <c>{a,b}</c> and <c>[1-3]</c> are requested
    /// as written, whatever <c>-g</c> / <c>--globoff</c> says (BL-1832).
    /// </summary>
    public bool IsUnglobbed { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when an <c>-o</c>, <c>-O</c>, <c>--out-null</c> or <c>--no-remote-name</c> has been paired
    /// with this entry, so the next such option pairs with the entry after it.
    /// </summary>
    internal bool HasOutputOption { get; set; }
}
