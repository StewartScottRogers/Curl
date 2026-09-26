using System.Net;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// The settings a command line asks for, filled in by <see cref="CommandLineParser"/>
/// through the rows of <see cref="CommandLineOptionTable"/>. Each option in the table
/// sets one property here. This is only what was asked for: nothing here validates a URL,
/// touches the file system or starts a transfer.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly List<string> urls = [];
    private readonly List<string> outputFiles = [];
    private readonly List<string> telnetOptions = [];

    /// <summary>
    /// The URLs to transfer, in command-line order: positional arguments and
    /// <c>--url</c> values interleaved as they were given.
    /// </summary>
    public IReadOnlyList<string> Urls => urls;

    /// <summary><see langword="true"/> when <c>-s</c> / <c>--silent</c> was given.</summary>
    public bool Silent { get; internal set; }

    /// <summary><see langword="true"/> when <c>-S</c> / <c>--show-error</c> was given.</summary>
    public bool ShowError { get; internal set; }

    /// <summary>The <c>-o</c> / <c>--output</c> file names, in command-line order.</summary>
    public IReadOnlyList<string> OutputFiles => outputFiles;

    /// <summary>
    /// The <c>-d</c> / <c>--data</c> value as UTF-8 bytes; <see langword="null"/> when not given.
    /// An empty value is empty data, not a refusal. When given more than once the last value wins,
    /// and <c>@file</c> is recorded as the literal text, not read.
    /// </summary>
    public ReadOnlyMemory<byte>? PostData { get; private set; }

    /// <summary>
    /// The <c>-u</c> / <c>--user</c> value split at its first colon into user name and password;
    /// <see langword="null"/> when not given. A value with no colon is a user name with an empty
    /// password; curl would prompt for the password instead.
    /// </summary>
    public NetworkCredential? Credentials { get; private set; }

    /// <summary>Every <c>-t</c> / <c>--telnet-option</c> value, verbatim and unvalidated, in command-line order.</summary>
    public IReadOnlyList<string> TelnetOptions => telnetOptions;

    /// <summary>
    /// The <c>--tftp-blksize</c> value as given, unclamped; <see langword="null"/> when not given.
    /// The TFTP handler clamps it to 8-65464.
    /// </summary>
    public int? TftpBlockSize { get; internal set; }

    /// <summary><see langword="true"/> when <c>--tftp-no-options</c> was given.</summary>
    public bool TftpNoOptions { get; internal set; }

    /// <summary>Appends <paramref name="url"/> to <see cref="Urls"/>, unchanged and unvalidated.</summary>
    /// <param name="url">A positional argument or a <c>--url</c> value.</param>
    internal void AddUrl(string url) => urls.Add(url);

    /// <summary>Appends <paramref name="outputFile"/> to <see cref="OutputFiles"/>; nothing is opened or created.</summary>
    /// <param name="outputFile">A <c>-o</c> / <c>--output</c> value.</param>
    internal void AddOutputFile(string outputFile) => outputFiles.Add(outputFile);

    /// <summary>Sets <see cref="PostData"/> to the UTF-8 bytes of <paramref name="data"/>, replacing any earlier value.</summary>
    /// <param name="data">A <c>-d</c> / <c>--data</c> value, possibly empty.</param>
    internal void SetPostData(string data) => PostData = Encoding.UTF8.GetBytes(data);

    /// <summary>Sets <see cref="Credentials"/> from <paramref name="userAndPassword"/>, split at its first colon.</summary>
    /// <param name="userAndPassword">A <c>-u</c> / <c>--user</c> value, possibly empty.</param>
    internal void SetCredentials(string userAndPassword)
    {
        int colon = userAndPassword.IndexOf(':', StringComparison.Ordinal);
        Credentials = colon < 0
            ? new NetworkCredential(userAndPassword, string.Empty)
            : new NetworkCredential(userAndPassword[..colon], userAndPassword[(colon + 1)..]);
    }

    /// <summary>Appends <paramref name="telnetOption"/> to <see cref="TelnetOptions"/>, unchanged and unvalidated.</summary>
    /// <param name="telnetOption">A <c>-t</c> / <c>--telnet-option</c> value, possibly empty.</param>
    internal void AddTelnetOption(string telnetOption) => telnetOptions.Add(telnetOption);
}
