using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

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
    private readonly List<UrlOutput> urlOutputs = [];
    private readonly List<string> uploadFiles = [];
    private readonly List<string> telnetOptions = [];
    private readonly List<string> resolveEntries = [];
    private readonly List<string> connectToEntries = [];
    private readonly List<string> headers = [];
    private readonly List<CommandLineCookie> cookies = [];
    private readonly List<string> warningLines = [];
    private readonly List<FormPartSpecification> formParts = [];
    private readonly Dictionary<string, byte[]> variables = new(StringComparer.Ordinal);
    private readonly Stack<FormPartSpecification> openMultiparts = new();
    private string? userAwaitingPassword;
    private string? proxyUserAwaitingPassword;
    private HttpAuthSchemes wantedAuthSchemes;

    /// <summary>
    /// The URLs to transfer, in command-line order: positional arguments and
    /// <c>--url</c> values interleaved as they were given.
    /// </summary>
    public IReadOnlyList<string> Urls => urls;

    /// <summary>
    /// The <c>-T</c> / <c>--upload-file</c> values in command-line order, each unchanged: the Nth is
    /// uploaded to the Nth URL of <see cref="Urls"/>, wherever each was given, and a URL past the end
    /// uploads nothing, as curl 8.21.0 pairs them. An empty value keeps its place and uploads nothing.
    /// </summary>
    public IReadOnlyList<string> UploadFiles => uploadFiles;

    /// <summary>
    /// <see langword="true"/> when <c>-g</c> / <c>--globoff</c> was given and no <c>--no-globoff</c>
    /// came after it: take each URL as written, with <c>UrlGlob.Unglobbed</c>, instead of expanding
    /// <c>{a,b}</c> sets and <c>[1-3]</c> ranges with <c>UrlGlob.TryParse</c>.
    /// </summary>
    public bool GlobOff { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-V</c> / <c>--version</c> was given on the command line. Parsing
    /// stops there, as curl 8.21.0's does, so every option after it is unread; the console prints
    /// <see cref="CurlVersionText"/>'s lines and exits 0 instead of transferring. A <c>version</c>
    /// line in a <c>-K</c> file does not set it: curl ignores it there.
    /// </summary>
    public bool VersionRequested { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-h</c> / <c>--help</c> was given on the command line. Parsing stops
    /// there, as curl 8.21.0's does, so every option after it is unread; the console prints
    /// <see cref="CurlHelpText"/>'s lines for <see cref="HelpSubject"/> and exits 0 instead of transferring.
    /// A <c>help</c> line in a <c>-K</c> file does not set it (curl prints the usage page there and carries
    /// on, which task BL-369 matches).
    /// </summary>
    public bool HelpRequested { get; private set; }

    /// <summary>
    /// The subject <c>--help</c> was given: its attached value, or else the argument after it, whatever
    /// it looks like; <see langword="null"/> when there was none or it was empty, which asks for the
    /// usage page. Set only with <see cref="HelpRequested"/>.
    /// </summary>
    public string? HelpSubject { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>-M</c> / <c>--manual</c> was given on the command line and no
    /// <c>--no-manual</c> came after it. Parsing stops there, as curl 8.21.0's does; the console prints
    /// <see cref="CurlManual"/>'s lines and exits 0 instead of transferring. A <c>manual</c> line in a
    /// <c>-K</c> file does not set it: curl ignores it there.
    /// </summary>
    public bool ManualRequested { get; internal set; }

    /// <summary>
    /// Whether an option has asked for information instead of a transfer (<see cref="VersionRequested"/>,
    /// <see cref="HelpRequested"/> or <see cref="ManualRequested"/>), which ends parsing where it stands.
    /// </summary>
    internal bool InformationRequested => VersionRequested || HelpRequested || ManualRequested;

    /// <summary>Records <c>--help</c> and its subject, an empty one read as none.</summary>
    /// <param name="subject">The subject as given, empty when there was none.</param>
    internal void RequestHelp(string subject)
    {
        HelpRequested = true;
        HelpSubject = subject.Length == 0 ? null : subject;
    }

    /// <summary>Forgets any request for information, as curl does for one made in a <c>-K</c> file.</summary>
    internal void ForgetInformationRequests()
    {
        VersionRequested = false;
        HelpRequested = false;
        HelpSubject = null;
        ManualRequested = false;
    }

    /// <summary><see langword="true"/> when <c>-s</c> / <c>--silent</c> was given and no <c>--no-silent</c> came after it.</summary>
    public bool Silent { get; internal set; }

    /// <summary><see langword="true"/> when <c>-S</c> / <c>--show-error</c> was given and no <c>--no-show-error</c> came after it.</summary>
    public bool ShowError { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--no-progress-meter</c> was given and no <c>--progress-meter</c>
    /// came after it. In curl 8.21.0 it turns the meter off whatever its form, so it outranks
    /// <see cref="ProgressBar"/> in either order.
    /// </summary>
    public bool ProgressMeterOff { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-#</c> / <c>--progress-bar</c> was given and no
    /// <c>--no-progress-bar</c> came after it: the meter, when shown, is the bar form.
    /// </summary>
    public bool ProgressBar { get; internal set; }

    /// <summary>
    /// Which of <c>-v</c> / <c>--verbose</c>, <c>--trace</c> and <c>--trace-ascii</c> came last, or
    /// <see cref="TraceKind.None"/> when none did or <c>--no-verbose</c> came after it.
    /// </summary>
    public TraceKind Trace { get; private set; }

    /// <summary>
    /// The file the last <c>--trace</c> or <c>--trace-ascii</c> names, <c>-</c> for standard output, while
    /// <see cref="Trace"/> is <see cref="TraceKind.HexDump"/> or <see cref="TraceKind.AsciiDump"/>;
    /// otherwise <see langword="null"/>, as <c>-v</c> writes to standard error.
    /// </summary>
    public string? TraceFile { get; private set; }

    /// <summary>
    /// How many times <c>-v</c> was given in a row, 0 to 4, as curl 8.21.0 counts it: the letters of
    /// one argument add up (<c>-vv</c> is 2, and so is <c>-vsv</c>), but a <c>-v</c> or <c>--verbose</c>
    /// that is the first option of its argument starts again at 1 (<c>-v -v</c> is 1, <c>-vv -v</c> is
    /// 1, <c>-vv -sv</c> is 3). A fifth <c>v</c> changes nothing and <c>--no-verbose</c> sets 0. From 2
    /// curl adds transfer and connection IDs and times to its verbose lines, from 3 protocol
    /// details and from 4 every component's trace.
    /// </summary>
    public int Verbosity { get; private set; }

    /// <summary>
    /// <see langword="true"/> when every verbose or trace line starts with the time of day: set by
    /// <c>--trace-time</c> and by the second <c>v</c> of <c>-vv</c>, cleared by <c>--no-trace-time</c>,
    /// by <c>--no-verbose</c> and by a <c>-v</c> or <c>--verbose</c> that is the first option of its
    /// argument (<c>--trace-time -v</c> shows no times; <c>--trace-time -sv</c> and <c>-v --trace-time</c> do).
    /// </summary>
    public bool TraceTime { get; internal set; }

    /// <summary>
    /// The file the last <c>--stderr</c> names, to which curl writes what it would write to standard
    /// error: <c>-</c> for standard output; <see langword="null"/> when none was given. An empty name is
    /// kept, not refused: curl 8.21.0 fails to open it, warns and carries on writing to standard error,
    /// which the console layer does when it opens the file.
    /// </summary>
    public string? StandardErrorFile { get; internal set; }

    /// <summary>
    /// The <c>-o</c> / <c>--output</c> file name of each entry of <see cref="UrlOutputs"/>, in the same
    /// order and up to the last entry that has one, <see langword="null"/> for an entry before it that
    /// has none: the Nth element is the <c>-o</c> file paired with the Nth URL, and a URL past the end
    /// has none. Empty when no <c>-o</c> was given.
    /// </summary>
    public IReadOnlyList<string?> OutputFiles =>
        urlOutputs[..(urlOutputs.FindLastIndex(output => output.FileName is not null) + 1)].ConvertAll(output => output.FileName);

    /// <summary>
    /// Where each URL's body goes, one <see cref="UrlOutput"/> per URL or output option, in the order
    /// curl 8.21.0 pairs them: the Nth URL with the Nth <c>-o</c>, <c>-O</c> or kept
    /// <c>--no-remote-name</c>. Entries past the last URL have no <see cref="UrlOutput.Url"/>.
    /// </summary>
    public IReadOnlyList<UrlOutput> UrlOutputs => urlOutputs;

    /// <summary>
    /// <see langword="true"/> when <c>--remote-name-all</c> was given and no <c>--no-remote-name-all</c>
    /// came after it. It applies to each URL or output option read while it is on, not to earlier ones.
    /// </summary>
    public bool RemoteNameAll { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-J</c> / <c>--remote-header-name</c> was given and no
    /// <c>--no-remote-header-name</c> came after it: a remote-named file takes its name from the
    /// <c>Content-Disposition</c> header when there is one.
    /// </summary>
    public bool RemoteHeaderName { get; internal set; }

    /// <summary>
    /// The <c>--output-dir</c> directory, verbatim and unchecked; <see langword="null"/> when not given.
    /// The last value wins.
    /// </summary>
    public string? OutputDirectory { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--create-dirs</c> was given and no <c>--no-create-dirs</c> came
    /// after it: missing directories in an output path are created.
    /// </summary>
    public bool CreateDirectories { get; internal set; }

    /// <summary>
    /// The <c>-w</c> / <c>--write-out</c> template, unexpanded; <see langword="null"/> when not given or
    /// when the last <c>-w @file</c> named an empty file. The last value wins. An <c>@file</c> or
    /// <c>@-</c> value is the file's (or standard input's) text with every carriage return, line feed and
    /// NUL removed, as curl 8.21.0 reads it.
    /// </summary>
    public string? WriteOut { get; internal set; }

    /// <summary>
    /// The request body built from every <c>-d</c> / <c>--data</c>, <c>--data-ascii</c>, <c>--data-binary</c>,
    /// <c>--data-raw</c>, <c>--data-urlencode</c> and <c>--json</c> value, as bytes; <see langword="null"/>
    /// when none was given. An empty value is empty data, not a refusal. The pieces are joined in
    /// command-line order, as in curl 8.21.0: a <c>--json</c> piece is appended as it is, and any other
    /// piece after a single <c>&amp;</c> when the body so far is not empty. Text is taken as UTF-8.
    /// A <c>-d</c> or <c>--data-ascii</c> value <c>@file</c> (or <c>@-</c>) contributes the file's (or
    /// standard input's) bytes with every carriage return, line feed and NUL removed; a
    /// <c>--data-binary</c> or <c>--json</c> one contributes them unchanged; <c>--data-raw</c> never
    /// reads a file. With <see cref="DataInQuery"/> the body is sent as the URL query instead
    /// (see <see cref="QueryUrl"/>).
    /// </summary>
    public ReadOnlyMemory<byte>? PostData { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>--json</c> was given at least once: curl 8.21.0 then sends
    /// <c>Content-Type: application/json</c> and <c>Accept: application/json</c>, even when a later
    /// <c>-d</c> adds to the body.
    /// </summary>
    public bool SendsJson { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>-G</c> / <c>--get</c> was given and no <c>--no-get</c> came after it:
    /// the request is a GET and <see cref="PostData"/>, when given, is sent as the URL query
    /// (see <see cref="QueryUrl"/>).
    /// </summary>
    public bool DataInQuery { get; internal set; }

    /// <summary>
    /// The <c>--url-query</c> values joined in command-line order with a <c>&amp;</c> between each
    /// two, even when one is empty, as curl 8.21.0 does; <see langword="null"/> when not given.
    /// Each value is encoded as <c>--data-urlencode</c> encodes it, except that one starting with
    /// <c>+</c> is kept verbatim without its <c>+</c>. Appended to the URL by <see cref="QueryUrl"/>.
    /// </summary>
    public string? UrlQuery { get; private set; }

    /// <summary>
    /// The <c>-D</c> / <c>--dump-header</c> file, verbatim and unchecked; <see langword="null"/> when
    /// not given. <c>-</c> means standard output. Nothing is opened or created here. The last value
    /// wins, as in curl 8.21.0.
    /// </summary>
    public string? DumpHeaderFile { get; internal set; }

    /// <summary>
    /// The <c>-u</c> / <c>--user</c> value split at its first colon into user name and password;
    /// <see langword="null"/> when not given. A value with no colon that does not start with <c>;</c>
    /// is a user name whose password <see cref="CommandLineParser"/> asks for through its
    /// <see cref="IPasswordPrompt"/> once the whole command line is read; <c>;</c> with no colon
    /// (<c>-u ;opt</c>) is a user name with an empty password, as in curl 8.21.0.
    /// </summary>
    public NetworkCredential? Credentials { get; private set; }

    /// <summary>
    /// The <c>-U</c> / <c>--proxy-user</c> value split at its first colon into user name and
    /// password, for the proxy; <see langword="null"/> when not given. A user with no password is
    /// asked for as <see cref="Credentials"/> is, with curl 8.21.0's proxy prompt. An empty value is
    /// accepted: curl 8.21.0 asks for the password of the user <c>''</c>.
    /// </summary>
    public NetworkCredential? ProxyCredentials { get; private set; }

    /// <summary>
    /// The HTTP authentication schemes to allow for the origin, as curl 8.21.0's tool asks libcurl
    /// for them: <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c> and <c>--negotiate</c> add their scheme and their <c>--no-</c> spellings
    /// remove it; <c>--anyauth</c> replaces the set with every scheme; <c>--oauth2-bearer</c> adds
    /// Bearer. <see cref="HttpAuthSchemes.Bearer"/> is only ever allowed with a
    /// <see cref="BearerToken"/>, so <c>--anyauth</c> alone gives <see cref="HttpAuthSchemes.Any"/>.
    /// When nothing is left, which is also when no scheme option was given, the set is
    /// <see cref="HttpAuthSchemes.Basic"/>, libcurl's default.
    /// </summary>
    /// <remarks>
    /// Measured with the reference curl 8.21.0 (<c>Record-CurlExchange.ps1</c>, a 401 offering Bearer
    /// and Basic, 2026-09-26): <c>-u u:p --no-basic</c> and <c>-u u:p --digest --no-digest</c> send
    /// <c>Basic dTpw</c> at once; <c>-u u:p --basic --no-basic --digest</c> sends nothing;
    /// <c>-u u:p --oauth2-bearer tok --basic</c> sends nothing, then <c>Bearer tok</c>;
    /// <c>-u u:p --anyauth --basic</c> and <c>-u u:p --anyauth</c> send nothing, then
    /// <c>Basic dTpw</c>; <c>--oauth2-bearer tok --anyauth</c> sends nothing, then <c>Bearer tok</c>;
    /// <c>--oauth2-bearer tok --no-basic</c> sends <c>Bearer tok</c> at once. Against a plain 200
    /// (2026-09-26): <c>-u u:p --ntlm</c> sends an NTLM type-1 message at once; <c>-u u:p --negotiate</c>,
    /// <c>--basic --ntlm</c> and <c>--ntlm --negotiate</c> send nothing; <c>--ntlm --no-ntlm</c> and
    /// <c>--negotiate --no-negotiate</c> send <c>Basic dTpw</c>. See ADR-0026.
    /// </remarks>
    public HttpAuthSchemes AuthSchemes
    {
        get
        {
            HttpAuthSchemes allowed = BearerToken is null ? wantedAuthSchemes & ~HttpAuthSchemes.Bearer : wantedAuthSchemes;
            return allowed == HttpAuthSchemes.None ? HttpAuthSchemes.Basic : allowed;
        }
    }

    /// <summary>
    /// The last <c>--oauth2-bearer</c> token; <see langword="null"/> when not given. An empty value is
    /// refused as blank. While it is set, a <c>-u</c> user with no password is not prompted for.
    /// </summary>
    public string? BearerToken { get; private set; }

    /// <summary>
    /// The last <c>-x</c> / <c>--proxy</c>, <c>--socks4</c>, <c>--socks4a</c>, <c>--socks5</c> or
    /// <c>--socks5-hostname</c> value, with the kind of proxy that option names; <see langword="null"/>
    /// when none was given. curl 8.21.0 keeps one proxy: the last of these options wins, value and kind
    /// together, and a scheme in the value outranks the option's kind. An empty <c>-x ''</c> is kept:
    /// it asks for no proxy at all, the environment's included.
    /// </summary>
    public CommandLineProxy? Proxy { get; private set; }

    /// <summary>
    /// The last <c>--noproxy</c> value, verbatim: the hosts to reach without a proxy. Empty is
    /// accepted. <see langword="null"/> when not given. Matching hosts against it is the proxy
    /// selector's job.
    /// </summary>
    public string? NoProxy { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-p</c> / <c>--proxytunnel</c> was given and no
    /// <c>--no-proxytunnel</c> came after it: tunnel through an HTTP proxy with CONNECT.
    /// </summary>
    public bool ProxyTunnel { get; internal set; }

    /// <summary>Every <c>-t</c> / <c>--telnet-option</c> value, verbatim and unvalidated, in command-line order.</summary>
    public IReadOnlyList<string> TelnetOptions => telnetOptions;

    /// <summary>
    /// Every <c>--resolve</c> value (<c>[+]host:port:addr[,addr]...</c>, or <c>-host:port</c> to drop an
    /// entry), verbatim and unvalidated, in command-line order. curl 8.21.0 checks the syntax only when a
    /// transfer starts, failing it with exit code 49 (<c>Could not parse CURLOPT_RESOLVE entry</c>), so the
    /// parser never refuses one.
    /// </summary>
    public IReadOnlyList<string> ResolveEntries => resolveEntries;

    /// <summary>
    /// Every <c>--connect-to</c> value (<c>host1:port1:host2:port2</c>, any part possibly empty), verbatim
    /// and unvalidated, in command-line order. curl 8.21.0 reads an entry only when a transfer starts, so
    /// the parser never refuses one.
    /// </summary>
    public IReadOnlyList<string> ConnectToEntries => connectToEntries;

    /// <summary>
    /// The <c>--tftp-blksize</c> value as given, unclamped, except that a value past
    /// <see cref="int.MaxValue"/> (accepted where a C <c>long</c> is 64 bits) is recorded as
    /// <see cref="int.MaxValue"/>; <see langword="null"/> when not given. The TFTP handler clamps
    /// it to 8-65464, so the two mean the same.
    /// </summary>
    public int? TftpBlockSize { get; internal set; }

    /// <summary><see langword="true"/> when <c>--tftp-no-options</c> was given and no <c>--no-tftp-no-options</c> came after it.</summary>
    public bool TftpNoOptions { get; internal set; }

    /// <summary>
    /// The <c>--create-file-mode</c> value, read as octal and at most <c>0777</c>;
    /// <see langword="null"/> when not given, where curl's default of <c>0644</c> applies.
    /// When given more than once the last value wins.
    /// </summary>
    public UnixFileMode? CreateFileMode { get; internal set; }

    /// <summary><see langword="true"/> when <c>-k</c> / <c>--insecure</c> was given and no <c>--no-insecure</c> came after it: skip server certificate verification.</summary>
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
    /// The <c>--cert-type</c> value (for example <c>PEM</c>, <c>DER</c> or <c>P12</c>), verbatim; the TLS layer
    /// compares it case-insensitively. <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? ClientCertificateType { get; internal set; }

    /// <summary>
    /// The <c>--key-type</c> value (for example <c>PEM</c> or <c>DER</c>), verbatim; the TLS layer compares it
    /// case-insensitively. <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? PrivateKeyType { get; internal set; }

    /// <summary>
    /// The <c>--pass</c> passphrase for the private key, verbatim; <see langword="null"/> when not given.
    /// The last value wins.
    /// </summary>
    public string? Passphrase { get; internal set; }

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
    /// The <c>--connect-timeout</c> limit, to the millisecond, at most about 29,000 years (see
    /// <see cref="CommandLineNumber.ParseSeconds"/>); <see langword="null"/> when not given.
    /// Past about 49.7 days it is longer than a .NET timer accepts, so cap it before waiting on
    /// it. Zero is recorded as given and means no limit, as it does to curl. The last value wins.
    /// </summary>
    public TimeSpan? ConnectTimeout { get; internal set; }

    /// <summary>
    /// The <c>-m</c> / <c>--max-time</c> limit on the whole transfer, to the millisecond, at most
    /// about 29,000 years (see <see cref="CommandLineNumber.ParseSeconds"/>); <see langword="null"/>
    /// when not given. Past about 49.7 days it is longer than a .NET timer accepts, so cap it
    /// before waiting on it. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public TimeSpan? MaxTime { get; internal set; }

    /// <summary>
    /// The <c>--retry</c> count: how many times a transient failure is retried, from 0 to the
    /// platform's C <c>LONG_MAX</c> (<see cref="CommandLineNumber.PlatformLongMaximum"/>). Zero,
    /// the default, retries nothing. The last value wins.
    /// </summary>
    public long RetryCount { get; internal set; }

    /// <summary>
    /// The <c>--retry-delay</c> wait between retries, to the millisecond, read as
    /// <see cref="CommandLineNumber.ParseSeconds"/> reads <c>-m</c>; <see langword="null"/> when not
    /// given, which leaves curl's own backoff in place. The last value wins.
    /// </summary>
    public TimeSpan? RetryDelay { get; internal set; }

    /// <summary>
    /// The <c>--retry-max-time</c> limit on the time spent retrying, to the millisecond, read as
    /// <see cref="CommandLineNumber.ParseSeconds"/> reads <c>-m</c>; <see langword="null"/> when not
    /// given. Zero is recorded as given and means no limit, as it does to curl. The last value wins.
    /// </summary>
    public TimeSpan? RetryMaxTime { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--retry-all-errors</c> was given and no
    /// <c>--no-retry-all-errors</c> came after it: <c>--retry</c> retries after any error.
    /// </summary>
    public bool RetryAllErrors { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--retry-connrefused</c> was given and no
    /// <c>--no-retry-connrefused</c> came after it: <c>--retry</c> counts a refused connection as
    /// transient.
    /// </summary>
    public bool RetryConnectionRefused { get; internal set; }

    /// <summary>
    /// The <c>--limit-rate</c> ceiling in bytes per second, for both download and upload, read as
    /// <see cref="CommandLineNumber.ParseSize"/> reads <c>--max-filesize</c> (<c>b</c>, <c>k</c>,
    /// <c>m</c>, <c>g</c>, <c>t</c> and <c>p</c> in either case, and fractions);
    /// <see langword="null"/> when not given. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public long? LimitRate { get; internal set; }

    /// <summary>
    /// The <c>-Y</c> / <c>--speed-limit</c> in bytes per second below which a transfer is too slow;
    /// <see langword="null"/> when not given. Curl 8.21.0 aborts a transfer that stays slower than
    /// this for <see cref="SpeedTimeSeconds"/>, or for 30 seconds when that is not given (measured
    /// through <c>--libcurl</c>). The last value wins.
    /// </summary>
    public long? SpeedLimit { get; internal set; }

    /// <summary>
    /// The <c>-y</c> / <c>--speed-time</c> in whole seconds a transfer may stay slower than
    /// <see cref="SpeedLimit"/>; <see langword="null"/> when not given. When it is given and
    /// <see cref="SpeedLimit"/> is not, curl 8.21.0 uses a limit of 1 byte per second (measured
    /// through <c>--libcurl</c>). The last value wins.
    /// </summary>
    public long? SpeedTimeSeconds { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-R</c> / <c>--remote-time</c> was given and no
    /// <c>--no-remote-time</c> came after it: give the output file the remote file's time.
    /// </summary>
    public bool RemoteTime { get; internal set; }

    /// <summary>
    /// The <c>-z</c> / <c>--time-cond</c> condition: the date read by <see cref="CurlDateParser"/> and
    /// its direction; <see langword="null"/> when not given, or when the last value was not a date,
    /// which curl 8.21.0 warns about and then transfers unconditionally. The last value wins.
    /// </summary>
    public TimeCondition? TimeCondition { get; internal set; }

    /// <summary>
    /// The <c>-X</c> / <c>--request</c> method, verbatim and never empty; <see langword="null"/> when
    /// not given. The last value wins.
    /// </summary>
    public string? RequestMethod { get; internal set; }

    /// <summary>
    /// The <c>-H</c> / <c>--header</c> values in command-line order, each verbatim, empty included,
    /// with an <c>@file</c> value replaced by the file's non-empty lines in file order.
    /// </summary>
    public IReadOnlyList<string> Headers => headers;

    /// <summary>
    /// The multipart form <c>-F</c> / <c>--form</c> and <c>--form-string</c> values describe, one
    /// top-level part per value in command-line order, parts given between <c>name=(</c> and <c>=)</c>
    /// inside the part that opened them; empty when neither option was given. A multipart part still
    /// open when the command line ends is simply closed there.
    /// </summary>
    public IReadOnlyList<FormPartSpecification> FormParts => formParts;

    /// <summary>
    /// The <c>-A</c> / <c>--user-agent</c> value, verbatim; empty when given empty, which curl 8.21.0
    /// sends as no <c>User-Agent</c> header at all; <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? UserAgent { get; internal set; }

    /// <summary>
    /// The <c>-e</c> / <c>--referer</c> value, verbatim, empty included, with a trailing <c>;auto</c>
    /// removed; <see langword="null"/> when not given, or when the value was <c>;auto</c> alone. The
    /// last value wins.
    /// </summary>
    public string? Referer { get; internal set; }

    /// <summary>
    /// Whether the last <c>-e</c> / <c>--referer</c> value ended in <c>;auto</c>: when on, a followed
    /// redirect sends the previous URL as <c>Referer</c>, per curl 8.21.0. A later value without the
    /// suffix turns it off again.
    /// </summary>
    public bool AutoReferer { get; internal set; }

    /// <summary>
    /// Every <c>-b</c> / <c>--cookie</c> value, cookie strings and cookie file names alike, in
    /// command-line order. Empty is accepted, as a file name.
    /// </summary>
    public IReadOnlyList<CommandLineCookie> Cookies => cookies;

    /// <summary>
    /// The last <c>-c</c> / <c>--cookie-jar</c> value, verbatim and never empty: the file to write
    /// every cookie to after the transfer (<c>-</c> for standard output). <see langword="null"/>
    /// when not given.
    /// </summary>
    public string? CookieJar { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-j</c> / <c>--junk-session-cookies</c> was given and no
    /// <c>--no-junk-session-cookies</c> came after it: drop the session cookies read from a
    /// <c>-b</c> file.
    /// </summary>
    public bool JunkSessionCookies { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-L</c> / <c>--location</c> or <c>--location-trusted</c> was
    /// given and no <c>--no-location</c> or <c>--no-location-trusted</c> came after it: follow redirects.
    /// </summary>
    public bool FollowRedirects { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--location-trusted</c> was given and no <c>--no-location-trusted</c>
    /// came after it: send the <c>-u</c> credentials and any <c>Authorization</c> header to every host
    /// a redirect leads to, not only the first. <c>-L</c> and <c>--no-location</c> leave it as it is,
    /// as in curl 8.21.0.
    /// </summary>
    public bool SendCredentialsToRedirectHosts { get; internal set; }

    /// <summary>
    /// The <c>--max-redirs</c> limit on redirects followed: 50 when not given, as in curl 8.21.0,
    /// and <c>-1</c> for no limit. A limit past <see cref="int.MaxValue"/> (accepted where a C
    /// <c>long</c> is 64 bits) is recorded as <see cref="int.MaxValue"/>, which no transfer reaches
    /// either. The last value wins.
    /// </summary>
    public int MaxRedirects { get; internal set; } = 50;

    /// <summary><see langword="true"/> when <c>--post301</c> was given and no <c>--no-post301</c> came after it: keep a POST a POST after a 301.</summary>
    public bool KeepPostAfter301 { get; internal set; }

    /// <summary><see langword="true"/> when <c>--post302</c> was given and no <c>--no-post302</c> came after it: keep a POST a POST after a 302.</summary>
    public bool KeepPostAfter302 { get; internal set; }

    /// <summary><see langword="true"/> when <c>--post303</c> was given and no <c>--no-post303</c> came after it: keep a POST a POST after a 303.</summary>
    public bool KeepPostAfter303 { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>-i</c> / <c>--show-headers</c> / <c>--include</c>,
    /// <c>-I</c> / <c>--head</c> and their <c>--no-</c> spellings turned it on: write the response
    /// headers to the output before the body.
    /// </summary>
    public bool ShowHeaders { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-I</c> / <c>--head</c> was given and no <c>--no-head</c> came
    /// after it: ask for the headers only (a <c>HEAD</c> request over HTTP). It maps onto the
    /// transfer's <c>NoBody</c>.
    /// </summary>
    public bool NoBody { get; internal set; }

    /// <summary>
    /// How an HTTP error response ends the transfer: <see cref="HttpFailMode.Fail"/> for <c>-f</c> /
    /// <c>--fail</c>, <see cref="HttpFailMode.FailWithBody"/> for <c>--fail-with-body</c>, whichever came
    /// last; <see cref="HttpFailMode.None"/> when neither was given or <c>--no-fail</c> or
    /// <c>--no-fail-with-body</c> came after it. Either <c>--no-</c> spelling turns off both, as in
    /// curl 8.21.0.
    /// </summary>
    public HttpFailMode FailMode { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--fail-early</c> was given and no <c>--no-fail-early</c> came after
    /// it: stop at the first transfer that fails instead of going on to the next URL.
    /// </summary>
    public bool FailEarly { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--compressed</c> was given and no <c>--no-compressed</c> came after
    /// it: ask for a compressed response and decompress it.
    /// </summary>
    public bool Compressed { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--raw</c> was given and no <c>--no-raw</c> came after it: pass
    /// content and transfer encodings through undecoded.
    /// </summary>
    public bool Raw { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--tr-encoding</c> was given and no <c>--no-tr-encoding</c> came
    /// after it: ask for a compressed transfer encoding and decode it.
    /// </summary>
    public bool TransferEncoding { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ignore-content-length</c> was given and no
    /// <c>--no-ignore-content-length</c> came after it: ignore the response's <c>Content-Length</c>.
    /// </summary>
    public bool IgnoreContentLength { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--path-as-is</c> was given and no <c>--no-path-as-is</c> came after
    /// it: send the URL path without squashing <c>/../</c> and <c>/./</c>.
    /// </summary>
    public bool PathAsIs { get; internal set; }

    /// <summary>
    /// The last <c>--request-target</c>, sent in place of the URL's path in the request line;
    /// <see langword="null"/> when not given. An empty value is refused as blank.
    /// </summary>
    public string? RequestTarget { get; internal set; }

    /// <summary>
    /// The last <c>--ipfs-gateway</c>, verbatim, for <c>Curl.Core</c>'s <c>IpfsGatewayRewriter</c>;
    /// <see langword="null"/> when not given. An empty value is refused as blank; any other value is
    /// accepted, because curl 8.21.0 checks the gateway only when it rewrites an <c>ipfs://</c> or
    /// <c>ipns://</c> URL, not while it reads the options.
    /// </summary>
    public string? IpfsGateway { get; internal set; }

    /// <summary>
    /// The HTTP version the last <c>-0</c> / <c>--http1.0</c> or <c>--http1.1</c> asked for;
    /// <see langword="null"/> when neither was given, which means curl's default, HTTP/1.1.
    /// </summary>
    public HttpVersionPreference? HttpVersion { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>--http0.9</c> was given and no <c>--no-http0.9</c> came after it:
    /// accept an HTTP/0.9 reply, one with no status line, instead of refusing it.
    /// </summary>
    public bool AllowHttp09Reply { get; internal set; }

    /// <summary>
    /// The HTTP request method <c>-I</c> / <c>--head</c> (<see cref="SelectedHttpMethod.Head"/>),
    /// <c>--no-head</c> (<see cref="SelectedHttpMethod.Get"/>) or <c>-F</c> / <c>--form</c> and
    /// <c>--form-string</c> (<see cref="SelectedHttpMethod.MultipartFormPost"/>) selected first; once one
    /// is selected, selecting another is refused, as curl 8.21.0 does. <see cref="SelectedHttpMethod.None"/>
    /// when none of them was given. Transfer setup reads it to refuse a <see cref="PostData"/> body
    /// sent with <c>HEAD</c> or <c>GET</c>.
    /// </summary>
    public SelectedHttpMethod HttpMethodSelected { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-s</c> / <c>--silent</c> has been read and <c>-S</c> /
    /// <c>--show-error</c> has not, so far: curl then hides error messages.
    /// </summary>
    internal bool ErrorsHidden => Silent && !ShowError;

    /// <summary>
    /// The path of the default config file (<c>.curlrc</c>) read before the command line when every
    /// line of it was applied; <see langword="null"/> when none was found, when <c>-q</c> /
    /// <c>--disable</c> came first, or when a line of it was refused. curl 8.21.0 names it with
    /// <c>-v</c> as <c>Note: Read config file from '&lt;path&gt;'</c>.
    /// </summary>
    public string? DefaultConfigFile { get; internal set; }

    /// <summary>
    /// How many <c>-K</c> / <c>--config</c> files are being read right now, one inside another; curl
    /// refuses to open one more once <see cref="CommandLineRefusal.MaximumConfigFileDepth"/> are open.
    /// </summary>
    internal int OpenConfigFileCount { get; set; }

    /// <summary>
    /// The warning lines met while reading the command line, in command-line order, without
    /// line terminators. <see cref="CommandLineParser"/> hands them to <see cref="CommandLineParseResult.WarningLines"/>.
    /// </summary>
    internal IReadOnlyList<string> WarningLines => warningLines;

    /// <summary>
    /// Sets <see cref="HttpVersion"/>, first adding <see cref="CommandLineWarning.OverridesPreviousHttpVersion"/>,
    /// unless <c>-s</c> came first, when an earlier option asked for a different version, as curl 8.21.0 does.
    /// </summary>
    /// <param name="version">The version the option asks for.</param>
    internal void SelectHttpVersion(HttpVersionPreference version)
    {
        if (HttpVersion is not null && HttpVersion != version)
        {
            AddWarningLinesUnlessSilent(CommandLineWarning.OverridesPreviousHttpVersion);
        }

        HttpVersion = version;
    }

    /// <summary>
    /// <see langword="true"/> while the option being applied is the first one of its argument
    /// (<c>--verbose</c>, or the <c>v</c> of <c>-v</c> and of <c>-vs</c>, but not of <c>-sv</c>); set by
    /// <see cref="CommandLineParser"/> before each option it applies.
    /// </summary>
    internal bool FirstOptionOfArgument { get; set; }

    /// <summary>
    /// Applies <c>-v</c> / <c>--verbose</c>, or <c>--no-verbose</c> when <paramref name="on"/> is
    /// <see langword="false"/>, as curl 8.21.0 does: see <see cref="Verbosity"/> and <see cref="TraceTime"/>.
    /// The first <c>v</c> after a reset selects <see cref="TraceKind.Verbose"/>, first adding
    /// <see cref="CommandLineWarning.VerboseOverridesTrace"/>, unless <c>-s</c> came first, when a
    /// <c>--trace</c> or <c>--trace-ascii</c> was in effect.
    /// </summary>
    /// <param name="on"><see langword="false"/> for <c>--no-verbose</c>.</param>
    internal void SetVerbose(bool on)
    {
        if (!on || FirstOptionOfArgument)
        {
            Verbosity = 0;
            TraceTime = false;
        }

        if (!on)
        {
            Trace = TraceKind.None;
            TraceFile = null;
            return;
        }

        RaiseVerbosity();
    }

    /// <summary>Takes <see cref="Verbosity"/> one step up, to at most 4, as one more <c>v</c> does.</summary>
    private void RaiseVerbosity()
    {
        const int MostVerbose = 4;
        if (Verbosity == 0)
        {
            SelectTrace(TraceKind.Verbose, null, CommandLineWarning.VerboseOverridesTrace);
        }
        else if (Verbosity == 1)
        {
            TraceTime = true;
        }

        Verbosity = Math.Min(Verbosity + 1, MostVerbose);
    }

    /// <summary>
    /// Applies <c>--trace</c> (<see cref="TraceKind.HexDump"/>) or <c>--trace-ascii</c>
    /// (<see cref="TraceKind.AsciiDump"/>) to <paramref name="file"/>, first adding
    /// <see cref="CommandLineWarning.TraceOverridesEarlierTrace"/>, unless <c>-s</c> came first, when
    /// <c>-v</c> or the other kind of trace was in effect. <see cref="Verbosity"/> is left as it is.
    /// </summary>
    /// <param name="dump">The kind of dump the option asks for.</param>
    /// <param name="file">The non-empty file name, <c>-</c> for standard output.</param>
    /// <param name="longName">The option's long name with its <c>--</c>, for the warning.</param>
    internal void SelectTraceDump(TraceKind dump, string file, string longName) =>
        SelectTrace(dump, file, CommandLineWarning.TraceOverridesEarlierTrace(longName));

    /// <summary>Sets <see cref="Trace"/> and <see cref="TraceFile"/>, warning when another kind was in effect.</summary>
    private void SelectTrace(TraceKind trace, string? file, IReadOnlyList<string> overrideWarning)
    {
        if (Trace != TraceKind.None && Trace != trace)
        {
            AddWarningLinesUnlessSilent(overrideWarning);
        }

        Trace = trace;
        TraceFile = file;
    }

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

    /// <summary>
    /// Appends <paramref name="lines"/> to <see cref="WarningLines"/> as they are: error lines curl
    /// prints while reading its default config file, already hidden, or not, by the caller.
    /// </summary>
    /// <param name="lines">The lines, without line terminators.</param>
    internal void AddErrorLines(IReadOnlyList<string> lines) => warningLines.AddRange(lines);

    /// <summary>
    /// Sets the <c>--variable</c> <paramref name="name"/> to <paramref name="content"/>, replacing any earlier
    /// content, and adds curl 8.21.0's <c>Note: Overwriting variable '&lt;name&gt;'</c> line when it had some
    /// and <c>-v</c>, <c>--trace</c> or <c>--trace-ascii</c> is in effect, <c>-s</c> or not (measured 2026-09-27).
    /// </summary>
    /// <param name="name">The variable's name: letters, digits and underscores, case-sensitive.</param>
    /// <param name="content">The variable's bytes.</param>
    internal void SetVariable(string name, byte[] content)
    {
        if (variables.ContainsKey(name) && Trace != TraceKind.None)
        {
            AddErrorLines(WrappedMessage.Lines("Note: ", $"Overwriting variable '{name}'"));
        }

        variables[name] = content;
    }

    /// <summary>Looks up the bytes of the <c>--variable</c> <paramref name="name"/>.</summary>
    /// <param name="name">The variable's name, case-sensitive.</param>
    /// <returns>The variable's bytes; <see langword="null"/> when no variable has that name.</returns>
    internal byte[]? FindVariable(string name) => variables.GetValueOrDefault(name);

    /// <summary>Appends <paramref name="url"/> to <see cref="Urls"/>, unchanged and unvalidated.</summary>
    /// <param name="url">A positional argument or a <c>--url</c> value.</param>
    internal void AddUrl(string url)
    {
        urls.Add(url);
        (urlOutputs.Find(output => output.Url is null) ?? AddUrlOutput()).Url = url;
    }

    /// <summary>Appends <paramref name="uploadFile"/> to <see cref="UploadFiles"/>; nothing is opened.</summary>
    /// <param name="uploadFile">A <c>-T</c> / <c>--upload-file</c> value, which may be empty.</param>
    internal void AddUploadFile(string uploadFile) => uploadFiles.Add(uploadFile);

    /// <summary>
    /// Pairs <paramref name="outputFile"/> with the next URL in <see cref="UrlOutputs"/>; nothing is
    /// opened or created.
    /// </summary>
    /// <param name="outputFile">A <c>-o</c> / <c>--output</c> value.</param>
    internal void AddOutputFile(string outputFile)
    {
        UrlOutput output = urlOutputs.Find(output => !output.HasOutputOption) ?? AddUrlOutput();
        output.FileName = outputFile;
        output.HasOutputOption = true;
    }

    /// <summary>
    /// Pairs <c>-O</c> / <c>--remote-name</c> (<paramref name="on"/> <see langword="true"/>) or
    /// <c>--no-remote-name</c> with the next URL in <see cref="UrlOutputs"/>. When no entry is left
    /// without an output option and <see cref="RemoteNameAll"/> is off, <c>--no-remote-name</c> is
    /// dropped, as curl 8.21.0 drops it: <c>--no-remote-name --no-remote-name u</c> gives no warning
    /// about more output options than URLs.
    /// </summary>
    /// <param name="on"><see langword="false"/> for the <c>--no-</c> spelling.</param>
    internal void PairRemoteName(bool on)
    {
        UrlOutput? output = urlOutputs.Find(output => !output.HasOutputOption);
        if (output is null)
        {
            if (!on && !RemoteNameAll)
            {
                return;
            }

            output = AddUrlOutput();
        }

        output.UsesRemoteName = on;
        output.HasOutputOption = true;
    }

    /// <summary>
    /// <see langword="true"/> when an output option has no URL to pair with, so curl 8.21.0 warns
    /// with <see cref="CommandLineWarning.MoreOutputOptionsThanUrls"/>.
    /// </summary>
    internal bool HasMoreOutputOptionsThanUrls =>
        urlOutputs.Exists(output => output.HasOutputOption && output.Url is null);

    private UrlOutput AddUrlOutput()
    {
        UrlOutput output = new(RemoteNameAll);
        urlOutputs.Add(output);
        return output;
    }

    /// <summary>
    /// Appends the UTF-8 bytes of <paramref name="data"/> to <see cref="PostData"/>, after a single
    /// <c>&amp;</c> when <see cref="PostData"/> already holds at least one byte, as curl 8.21.0 does.
    /// </summary>
    /// <param name="data">A <c>-d</c> / <c>--data</c> value, possibly empty.</param>
    internal void AppendPostData(string data) => AppendPostData(Encoding.UTF8.GetBytes(data));

    /// <summary>
    /// Appends <paramref name="data"/> to <see cref="PostData"/>, after a single <c>&amp;</c> when
    /// <see cref="PostData"/> already holds at least one byte, as curl 8.21.0 does.
    /// </summary>
    /// <param name="data">The bytes of one <c>-d</c> / <c>--data</c> piece, possibly empty.</param>
    internal void AppendPostData(byte[] data)
    {
        byte[] separator = PostData is { Length: > 0 } ? [(byte)'&'] : [];
        PostData = PostData is { } body ? [.. body.Span, .. separator, .. data] : data;
    }

    /// <summary>
    /// Appends <paramref name="data"/> to <see cref="PostData"/> with no separator and sets
    /// <see cref="SendsJson"/>, as curl 8.21.0 does for <c>--json</c>.
    /// </summary>
    /// <param name="data">The bytes of one <c>--json</c> value, possibly empty.</param>
    internal void AppendJsonData(byte[] data)
    {
        PostData = PostData is { } body ? [.. body.Span, .. data] : data;
        SendsJson = true;
    }

    /// <summary>
    /// Appends <paramref name="query"/> to <see cref="UrlQuery"/>, after a <c>&amp;</c> when
    /// <see cref="UrlQuery"/> is already set, even to empty text.
    /// </summary>
    /// <param name="query">One encoded <c>--url-query</c> value, possibly empty.</param>
    internal void AppendUrlQuery(string query) =>
        UrlQuery = UrlQuery is null ? query : $"{UrlQuery}&{query}";

    /// <summary>Sets <see cref="Credentials"/> from <paramref name="userAndPassword"/>, split at its first colon.</summary>
    /// <param name="userAndPassword">A <c>-u</c> / <c>--user</c> value, possibly empty.</param>
    internal void SetCredentials(string userAndPassword)
    {
        Credentials = SplitAtFirstColon(userAndPassword);
        userAwaitingPassword = UserWhosePasswordIsMissing(userAndPassword);
    }

    /// <summary>Sets <see cref="ProxyCredentials"/> from <paramref name="userAndPassword"/>, split at its first colon.</summary>
    /// <param name="userAndPassword">A <c>-U</c> / <c>--proxy-user</c> value, possibly empty.</param>
    internal void SetProxyCredentials(string userAndPassword)
    {
        ProxyCredentials = SplitAtFirstColon(userAndPassword);
        proxyUserAwaitingPassword = UserWhosePasswordIsMissing(userAndPassword);
    }

    private static NetworkCredential SplitAtFirstColon(string userAndPassword)
    {
        int colon = userAndPassword.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new NetworkCredential(userAndPassword, string.Empty)
            : new NetworkCredential(userAndPassword[..colon], userAndPassword[(colon + 1)..]);
    }

    /// <summary>
    /// The value itself when it names a user with no password, which curl 8.21.0 prompts for: no
    /// colon, and not starting with <c>;</c>. <see langword="null"/> otherwise.
    /// </summary>
    private static string? UserWhosePasswordIsMissing(string userAndPassword) =>
        !userAndPassword.Contains(':', StringComparison.Ordinal) && !userAndPassword.StartsWith(';') ? userAndPassword : null;

    /// <summary>
    /// Asks <paramref name="passwordPrompt"/> for each password the command line left out, with
    /// curl 8.21.0's prompts, host first: when the last <c>-u</c> / <c>--user</c> value named a user
    /// with no password and no <c>--oauth2-bearer</c> was given,
    /// <c>Enter host password for user '&lt;user&gt;':</c>, recorded as the <see cref="Credentials"/>
    /// password; then, when the last <c>-U</c> / <c>--proxy-user</c> value named a user with no
    /// password, <c>Enter proxy password for user '&lt;user&gt;':</c>, recorded as the
    /// <see cref="ProxyCredentials"/> password. The user shown is the value up to its first
    /// <c>;</c> (curl's login options are not shown). Does nothing when no password is missing.
    /// </summary>
    /// <remarks>
    /// Measured with the local curl 8.21.0 on 2026-09-26: <c>-u u --oauth2-bearer tok</c> never
    /// prompts; <c>-U p -u h</c> prompts for <c>'h'</c>'s host password first.
    /// </remarks>
    /// <param name="passwordPrompt">Asks for the passwords.</param>
    internal void ReadMissingPasswords(IPasswordPrompt passwordPrompt)
    {
        if (userAwaitingPassword is { } user && BearerToken is null)
        {
            Credentials = new NetworkCredential(user, ReadPassword(passwordPrompt, "host", user));
            userAwaitingPassword = null;
        }

        if (proxyUserAwaitingPassword is { } proxyUser)
        {
            ProxyCredentials = new NetworkCredential(proxyUser, ReadPassword(passwordPrompt, "proxy", proxyUser));
            proxyUserAwaitingPassword = null;
        }
    }

    private static string ReadPassword(IPasswordPrompt passwordPrompt, string kind, string user)
    {
        int loginOptions = user.IndexOf(';', StringComparison.Ordinal);
        string shownUser = loginOptions < 0 ? user : user[..loginOptions];
        return passwordPrompt.ReadPassword($"Enter {kind} password for user '{shownUser}':");
    }

    /// <summary>
    /// Adds <paramref name="scheme"/> to, or for its <c>--no-</c> spelling removes it from, the
    /// schemes <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c>, <c>--anyauth</c> and
    /// <c>--oauth2-bearer</c> asked for.
    /// </summary>
    /// <param name="scheme">The scheme the option names.</param>
    /// <param name="on"><see langword="false"/> for the <c>--no-</c> spelling.</param>
    internal void WantAuthScheme(HttpAuthSchemes scheme, bool on) =>
        wantedAuthSchemes = on ? wantedAuthSchemes | scheme : wantedAuthSchemes & ~scheme;

    /// <summary>Replaces every scheme asked for so far with every scheme there is, for <c>--anyauth</c>.</summary>
    internal void WantEveryAuthScheme() =>
        wantedAuthSchemes = HttpAuthSchemes.Any | HttpAuthSchemes.Bearer;

    /// <summary>
    /// Records an <c>--oauth2-bearer</c> token as <see cref="BearerToken"/> and adds
    /// <see cref="HttpAuthSchemes.Bearer"/> to the schemes asked for, as curl 8.21.0's tool does.
    /// </summary>
    /// <param name="token">The non-empty token.</param>
    internal void SetBearerToken(string token)
    {
        BearerToken = token;
        WantAuthScheme(HttpAuthSchemes.Bearer, on: true);
    }

    /// <summary>
    /// Records a <c>-x</c> / <c>--proxy</c> value, or a <c>--socks4</c>, <c>--socks4a</c>, <c>--socks5</c>
    /// or <c>--socks5-hostname</c> one, as <see cref="Proxy"/>, replacing any earlier one.
    /// </summary>
    /// <param name="address">The value as given, possibly empty.</param>
    /// <param name="kindWithoutScheme">The kind the option names, used when the value has no scheme.</param>
    internal void SetProxy(string address, ProxyKind kindWithoutScheme) =>
        Proxy = new CommandLineProxy(address, kindWithoutScheme);

    /// <summary>Appends <paramref name="telnetOption"/> to <see cref="TelnetOptions"/>, unchanged and unvalidated.</summary>
    /// <param name="telnetOption">A <c>-t</c> / <c>--telnet-option</c> value, possibly empty.</param>
    internal void AddTelnetOption(string telnetOption) => telnetOptions.Add(telnetOption);

    /// <summary>Appends <paramref name="entry"/> to <see cref="ResolveEntries"/>, unchanged and unvalidated.</summary>
    /// <param name="entry">A <c>--resolve</c> value, possibly empty.</param>
    internal void AddResolveEntry(string entry) => resolveEntries.Add(entry);

    /// <summary>Appends <paramref name="entry"/> to <see cref="ConnectToEntries"/>, unchanged and unvalidated.</summary>
    /// <param name="entry">A <c>--connect-to</c> value, possibly empty.</param>
    internal void AddConnectToEntry(string entry) => connectToEntries.Add(entry);

    /// <summary>Appends <paramref name="cookie"/> to <see cref="Cookies"/>, unchanged and unvalidated.</summary>
    /// <param name="cookie">A <c>-b</c> / <c>--cookie</c> value, possibly empty.</param>
    internal void AddCookie(string cookie) => cookies.Add(new CommandLineCookie(cookie));

    /// <summary>Appends <paramref name="header"/> to <see cref="Headers"/>, unchanged and unvalidated.</summary>
    /// <param name="header">A <c>-H</c> / <c>--header</c> value, or one line of its <c>@file</c>.</param>
    internal void AddHeader(string header) => headers.Add(header);

    /// <summary>
    /// Appends <paramref name="part"/> to the innermost multipart part still open, or to
    /// <see cref="FormParts"/> when none is.
    /// </summary>
    /// <param name="part">The part to append.</param>
    internal void AddFormPart(FormPartSpecification part)
    {
        if (openMultiparts.TryPeek(out FormPartSpecification? multipart))
        {
            multipart.AddPart(part);
        }
        else
        {
            formParts.Add(part);
        }
    }

    /// <summary>
    /// Appends <paramref name="multipart"/> as <see cref="AddFormPart"/> does and opens it, so the
    /// parts that follow go inside it until <see cref="TryCloseMultipart"/>.
    /// </summary>
    /// <param name="multipart">A <see cref="FormPartKind.Multipart"/> part.</param>
    internal void OpenMultipart(FormPartSpecification multipart)
    {
        AddFormPart(multipart);
        openMultiparts.Push(multipart);
    }

    /// <summary>Closes the innermost multipart part still open.</summary>
    /// <returns><see langword="true"/> when one was closed; <see langword="false"/> when none is open.</returns>
    internal bool TryCloseMultipart() => openMultiparts.TryPop(out _);
}
