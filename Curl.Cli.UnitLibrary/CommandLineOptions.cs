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
    private readonly List<string> outputFiles = [];
    private readonly List<string> telnetOptions = [];
    private readonly List<string> headers = [];
    private readonly List<string> warningLines = [];
    private readonly List<FormPartSpecification> formParts = [];
    private readonly Stack<FormPartSpecification> openMultiparts = new();
    private string? userAwaitingPassword;

    /// <summary>
    /// The URLs to transfer, in command-line order: positional arguments and
    /// <c>--url</c> values interleaved as they were given.
    /// </summary>
    public IReadOnlyList<string> Urls => urls;

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

    /// <summary>The <c>-o</c> / <c>--output</c> file names, in command-line order.</summary>
    public IReadOnlyList<string> OutputFiles => outputFiles;

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

    /// <summary>Every <c>-t</c> / <c>--telnet-option</c> value, verbatim and unvalidated, in command-line order.</summary>
    public IReadOnlyList<string> TelnetOptions => telnetOptions;

    /// <summary>
    /// The <c>--tftp-blksize</c> value as given, unclamped; <see langword="null"/> when not given.
    /// The TFTP handler clamps it to 8-65464.
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
    /// The <c>--connect-timeout</c> limit, to the millisecond; <see langword="null"/> when not
    /// given. Zero is recorded as given and means no limit, as it does to curl. The last value wins.
    /// </summary>
    public TimeSpan? ConnectTimeout { get; internal set; }

    /// <summary>
    /// The <c>-m</c> / <c>--max-time</c> limit on the whole transfer, to the millisecond;
    /// <see langword="null"/> when not given. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public TimeSpan? MaxTime { get; internal set; }

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
    /// The <c>-e</c> / <c>--referer</c> value, verbatim, empty included, and <c>;auto</c> kept as given;
    /// <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? Referer { get; internal set; }

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
    /// and <c>-1</c> for no limit. The last value wins.
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
    /// The HTTP version the last <c>-0</c> / <c>--http1.0</c> or <c>--http1.1</c> asked for;
    /// <see langword="null"/> when neither was given, which means curl's default, HTTP/1.1.
    /// </summary>
    public HttpVersionPreference? HttpVersion { get; private set; }

    /// <summary>
    /// The HTTP request method <c>-I</c> / <c>--head</c> (<see cref="SelectedHttpMethod.Head"/>),
    /// <c>--no-head</c> (<see cref="SelectedHttpMethod.Get"/>) or <c>-F</c> / <c>--form</c> and
    /// <c>--form-string</c> (<see cref="SelectedHttpMethod.MultipartFormPost"/>) selected first; once one
    /// is selected, selecting another is refused, as curl 8.21.0 does.
    /// </summary>
    internal SelectedHttpMethod HttpMethodSelected { get; set; }

    /// <summary>
    /// <see langword="true"/> when <c>-s</c> / <c>--silent</c> has been read and <c>-S</c> /
    /// <c>--show-error</c> has not, so far: curl then hides error messages.
    /// </summary>
    internal bool ErrorsHidden => Silent && !ShowError;

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
        int colon = userAndPassword.IndexOf(':', StringComparison.Ordinal);
        Credentials = colon < 0
            ? new NetworkCredential(userAndPassword, string.Empty)
            : new NetworkCredential(userAndPassword[..colon], userAndPassword[(colon + 1)..]);
        userAwaitingPassword = colon < 0 && !userAndPassword.StartsWith(';') ? userAndPassword : null;
    }

    /// <summary>
    /// When the last <c>-u</c> / <c>--user</c> value named a user with no password, asks
    /// <paramref name="passwordPrompt"/> for it with curl 8.21.0's prompt,
    /// <c>Enter host password for user '&lt;user&gt;':</c>, the user being the value up to its first
    /// <c>;</c> (curl's login options are not shown), and records the answer as the
    /// <see cref="Credentials"/> password. Does nothing otherwise.
    /// </summary>
    /// <param name="passwordPrompt">Asks for the password.</param>
    internal void ReadMissingPassword(IPasswordPrompt passwordPrompt)
    {
        if (userAwaitingPassword is not { } user)
        {
            return;
        }

        int loginOptions = user.IndexOf(';', StringComparison.Ordinal);
        string shownUser = loginOptions < 0 ? user : user[..loginOptions];
        string password = passwordPrompt.ReadPassword($"Enter host password for user '{shownUser}':");
        Credentials = new NetworkCredential(user, password);
        userAwaitingPassword = null;
    }

    /// <summary>Appends <paramref name="telnetOption"/> to <see cref="TelnetOptions"/>, unchanged and unvalidated.</summary>
    /// <param name="telnetOption">A <c>-t</c> / <c>--telnet-option</c> value, possibly empty.</param>
    internal void AddTelnetOption(string telnetOption) => telnetOptions.Add(telnetOption);

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
