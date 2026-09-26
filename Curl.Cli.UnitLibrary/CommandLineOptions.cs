using System.Net;
using System.Security.Authentication;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// The settings a command line asks for, filled in by <see cref="CommandLineParser"/>
/// through the rows of <see cref="CommandLineOptionTable"/>. Each option in the table
/// sets one property here. This is only what was asked for: nothing here validates a URL,
/// touches the file system or starts a transfer. The TLS settings are recorded, not applied;
/// the console layer maps them onto the TLS provider.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly List<string> urls = [];
    private readonly List<string> outputFiles = [];
    private readonly List<string> telnetOptions = [];
    private readonly List<string> warningLines = [];

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

    /// <summary>
    /// The <c>--create-file-mode</c> value, read as octal and at most <c>0777</c>;
    /// <see langword="null"/> when not given, where curl's default of <c>0644</c> applies.
    /// When given more than once the last value wins.
    /// </summary>
    public UnixFileMode? CreateFileMode { get; internal set; }

    /// <summary><see langword="true"/> when <c>-k</c> / <c>--insecure</c> was given: skip server certificate verification.</summary>
    public bool Insecure { get; internal set; }

    /// <summary>
    /// The <c>--cacert</c> file, verbatim; <see langword="null"/> when not given. The parser has
    /// already refused a value at which nothing exists, and records a directory here unchanged.
    /// The last value wins.
    /// </summary>
    public string? CaCertificateFile { get; internal set; }

    /// <summary>The <c>--capath</c> directory, verbatim and unchecked; <see langword="null"/> when not given. The last value wins.</summary>
    public string? CaCertificateDirectory { get; internal set; }

    /// <summary>
    /// The <c>-E</c> / <c>--cert</c> value, verbatim, with <c>certificate[:password]</c> not yet split;
    /// <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? ClientCertificate { get; internal set; }

    /// <summary>The <c>--key</c> private key file, verbatim and unchecked; <see langword="null"/> when not given. The last value wins.</summary>
    public string? PrivateKey { get; internal set; }

    /// <summary>
    /// The lowest TLS version to accept: <see cref="SslProtocols.Tls12"/> for <c>--tlsv1.2</c> (1.2 or later),
    /// <see cref="SslProtocols.Tls13"/> for <c>--tlsv1.3</c> (1.3 or later); <see langword="null"/> when neither
    /// was given. When both are given the last one wins, as in curl 8.21.0.
    /// </summary>
    public SslProtocols? MinimumTlsVersion { get; internal set; }

    /// <summary>The <c>--ciphers</c> list, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? Ciphers { get; internal set; }

    /// <summary>The <c>--tls13-ciphers</c> list, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? Tls13Ciphers { get; internal set; }

    /// <summary>
    /// The <c>-r</c> / <c>--range</c> text as curl keeps it, not yet parsed; <see langword="null"/>
    /// when not given. A value that starts with a digit and has no dash is kept as that leading
    /// number with a dash appended (<c>5abc</c> becomes <c>5-</c>); anything else is kept verbatim.
    /// <c>ByteRangeParser</c> in <c>Curl.Core.UnitLibrary</c> turns it into the range a handler
    /// receives. The last value wins.
    /// </summary>
    public string? Range { get; internal set; }

    /// <summary>
    /// The <c>-C</c> / <c>--continue-at</c> byte offset; <see langword="null"/> when not given, or
    /// when <c>-C -</c> asked for the offset to be worked out (<see cref="ResumeFromOutputSize"/>).
    /// The last value wins.
    /// </summary>
    public long? ResumeFrom { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last <c>-C</c> / <c>--continue-at</c> was <c>-</c>: resume
    /// from the size of the output file.
    /// </summary>
    public bool ResumeFromOutputSize { get; internal set; }

    /// <summary>
    /// The <c>--max-filesize</c> limit in bytes, units and fractions already applied;
    /// <see langword="null"/> when not given. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public long? MaxFileSize { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-s</c> / <c>--silent</c> has been read and <c>-S</c> /
    /// <c>--show-error</c> has not, so far: curl then hides error messages.
    /// </summary>
    internal bool ErrorsHidden => Silent && !ShowError;

    /// <summary>
    /// The warning lines met while reading the command line, in command-line order, without
    /// line terminators. <see cref="CommandLineParser"/> hands them to <see cref="CommandLineParseResult.WarningLines"/>.
    /// </summary>
    internal IReadOnlyList<string> WarningLines => warningLines;

    /// <summary>
    /// Appends <paramref name="lines"/> to <see cref="WarningLines"/> unless <c>-s</c> /
    /// <c>--silent</c> has already been read: curl 8.21.0 drops a warning raised while <c>-s</c> is in
    /// effect, even with <c>-S</c> and even if <c>--no-silent</c> follows, and keeps one raised
    /// before a later <c>-s</c>.
    /// </summary>
    /// <param name="lines">One warning's lines, without line terminators.</param>
    internal void AddWarningLinesUnlessSilent(IReadOnlyList<string> lines)
    {
        if (!Silent)
        {
            warningLines.AddRange(lines);
        }
    }

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
