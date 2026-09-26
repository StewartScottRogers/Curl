namespace Curl.Cli;

/// <summary>
/// One <c>-b</c> / <c>--cookie</c> value as given: either a cookie string to send or the name of a
/// file to read cookies from.
/// </summary>
/// <remarks>
/// curl 8.21.0 reads a value containing <c>=</c> as a cookie string (<c>-b 'a=1; b=2'</c>) and any
/// other value, empty included, as a file name (<c>-b jar.txt</c>), per
/// <see href="https://curl.se/docs/manpage.html#-b"/>. Reading the file, and ignoring one that does
/// not exist as curl does, is the cookie engine's job.
/// </remarks>
/// <param name="Value">The value as given on the command line, possibly empty.</param>
public sealed record CommandLineCookie(string Value)
{
    /// <summary>
    /// <see langword="true"/> when <see cref="Value"/> contains <c>=</c>, so it is a cookie string;
    /// <see langword="false"/> when it names a cookie file.
    /// </summary>
    public bool IsCookieString => Value.Contains('=');
}
