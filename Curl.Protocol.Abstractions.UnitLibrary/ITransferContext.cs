namespace Curl.Protocol.Abstractions;

/// <summary>
/// Everything a protocol handler needs for one transfer, assembled by the command
/// line layer and handed to the handler.
/// </summary>
public interface ITransferContext
{
    /// <summary>
    /// Gets the URL being transferred, as given on the command line and parsed as curl
    /// parses it (<see cref="CurlUrl" />).
    /// </summary>
    CurlUrl Url { get; }

    /// <summary>
    /// Gets the stream that received data is written to.
    /// </summary>
    Stream Output { get; }

    /// <summary>
    /// Gets the stream to upload from, or <see langword="null" /> for a download.
    /// </summary>
    Stream? Upload { get; }

    /// <summary>
    /// Gets the byte offset a transfer resumes from, per <c>-C</c>/<c>--continue-at</c>,
    /// or <see langword="null" /> when the caller is not resuming.
    /// </summary>
    /// <remarks>
    /// A download seeks the source to this offset and appends to the destination; an
    /// upload skips this many bytes of the local file. It is an offset, not a size, so
    /// zero means "resume from the start" and is not the same as
    /// <see langword="null" />.
    /// </remarks>
    long? ResumeFrom { get; }

    /// <summary>
    /// Gets whether <c>-C -</c> asked a <c>-T</c> upload to resume from an offset the caller
    /// cannot know: how much of the file the server already holds.
    /// </summary>
    /// <remarks>
    /// curl 8.21.0 turns an upload's <c>-C -</c> into offset -1, whatever <c>-o</c> names, and
    /// over HTTP sends the whole source with <c>Content-Range: bytes 0-(L-1)/L</c> for its
    /// length L (measured, BL-351 Notes). When this is <see langword="true" />, a handler that
    /// honours it ignores <see cref="ResumeFrom" />.
    /// </remarks>
    bool ResumeUploadFromUnknownOffset { get; }

    /// <summary>
    /// Gets the byte range requested with <c>-r</c>/<c>--range</c>, or
    /// <see langword="null" /> when the whole resource was asked for.
    /// </summary>
    /// <remarks>
    /// curl accepts a comma-separated list but honours only the first range for
    /// <c>file://</c>. <c>Curl.Console</c> parses the <c>-r</c>/<c>--range</c> text once per
    /// transfer, before any handler runs, with <c>ByteRangeParser</c> in
    /// <c>Curl.Core.UnitLibrary</c>, which answers text that names no range with exit 33
    /// (<see cref="CurlExitCode.RangeError" />) and no handler call; so a handler sees at most
    /// one <see cref="Abstractions.ByteRange" />, already validated. An <c>http</c> or
    /// <c>https</c> transfer is the exception: curl 8.21.0 does not parse the text for HTTP, so
    /// it is never refused there, and this is <see langword="null" /> for text that names no
    /// range; an HTTP handler reads <see cref="RangeText" /> instead.
    /// </remarks>
    ByteRange? Range { get; }

    /// <summary>
    /// Gets the <c>-r</c>/<c>--range</c> text exactly as given, or <see langword="null" />
    /// when none was given.
    /// </summary>
    /// <remarks>
    /// curl 8.21.0 sends it verbatim over HTTP, unparsed: <c>-r 0-9,20-29</c> sends
    /// <c>Range: bytes=0-9,20-29</c>, and with <c>-d x</c> <c>Content-Range: bytes 0-9,20-29/1</c>;
    /// <c>abc</c>, <c>1-2abc</c>, <c>-0</c> and <c>3-1</c> go out as typed too (measured,
    /// BL-386 Notes). The HTTP handler reads it; a handler that serves one range reads
    /// <see cref="Range" />.
    /// </remarks>
    string? RangeText { get; }

    /// <summary>
    /// Gets the largest body, in bytes, that <c>--max-filesize</c> allows a download to
    /// deliver, or <see langword="null" /> when no limit was given.
    /// </summary>
    /// <remarks>
    /// Zero also means no limit, as it does to curl. Measured on curl 8.21.0 over
    /// <c>file://</c>, a download with more body bytes than this writes exactly this many,
    /// then fails with exit 63 (<see cref="CurlExitCode.FilesizeExceeded" />); the limit
    /// counts body bytes only, so headers written to <see cref="HeaderOutput" /> do not use
    /// it up, and an upload ignores it. <c>Curl.Console</c> fills it from <c>--max-filesize</c>.
    /// The <c>dict</c>, <c>file://</c>, <c>ftp</c>/<c>ftps</c>, <c>gopher</c>,
    /// <c>http</c>/<c>https</c>, <c>imap</c>, <c>mqtt</c>, <c>pop3</c>, <c>rtsp</c>,
    /// <c>telnet</c> and <c>ws</c>/<c>wss</c> handlers enforce it; the <c>ldap</c>,
    /// <c>smb</c>, <c>smtp</c>, <c>scp</c>/<c>sftp</c> and <c>tftp</c> handlers do not
    /// read it yet. Over gopher a reply is cut at the limit and fails with exit 63 and
    /// <c>Exceeded the maximum allowed file size (N) with N bytes</c>. Over HTTP a response whose Content-Length is over the limit
    /// fails before any body is written, with exit 63 and <c>Maximum file size exceeded</c>;
    /// a body with no Content-Length, or one that grows past it, is cut at the limit and
    /// fails with exit 63 and <c>Exceeded the maximum allowed file size (N) with N bytes</c>
    /// (ADR-0044).
    /// </remarks>
    long? MaxFileSize { get; }

    /// <summary>
    /// Gets a value indicating whether only metadata was asked for, per
    /// <c>-I</c>/<c>--head</c>: pseudo-headers are written and no body is.
    /// </summary>
    /// <remarks>
    /// The resource is still opened. curl's <c>-I</c> on a directory reports the same
    /// exit 37 (<see cref="CurlExitCode.FileCouldntReadFile" />) that a body transfer
    /// would, because suppressing the body does not suppress the open.
    /// </remarks>
    bool NoBody { get; }

    /// <summary>
    /// Gets the condition from <c>-z</c>/<c>--time-cond</c> that decides whether the body
    /// is transferred at all, or <see langword="null" /> when none was given.
    /// </summary>
    TimeCondition? TimeCondition { get; }

    /// <summary>
    /// Gets a value indicating whether <c>-R</c>/<c>--remote-time</c> was given, so a handler
    /// that must ask the server for the resource's time does, and reports it as
    /// <see cref="TransferResult.SourceLastWriteTimeUtc" />; <see langword="false" /> when
    /// not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> reads it: curl 8.21.0 sends <c>MDTM</c> only under <c>-R</c>,
    /// <c>-z</c> or <c>-I</c> (BL-637). A handler that learns the time anyway, as
    /// <c>file://</c> does, ignores it. The caller, not the handler, applies the time to the
    /// output file.
    /// </remarks>
    bool RemoteTime { get; }

    /// <summary>
    /// Gets the stream that headers are written to for <c>-i</c>/<c>--include</c> and
    /// <c>-D</c>/<c>--dump-header</c>, or <see langword="null" /> when the caller asked
    /// for no header output.
    /// </summary>
    /// <remarks>
    /// This may be the same stream as <see cref="Output" />, which is what <c>-i</c>
    /// means, or a separate one, which is what <c>-D</c> means. A handler writes headers
    /// here before any body and never inspects which case it has. For <c>file://</c> the
    /// headers are curl's synthesised <c>Content-Length</c>, <c>Accept-ranges</c> and
    /// <c>Last-Modified</c> lines rather than anything received from a peer. Lines curl
    /// writes to the <c>-D</c> file but not under <c>-i</c> go to
    /// <see cref="DumpHeaderOutput" /> instead.
    /// </remarks>
    Stream? HeaderOutput { get; }

    /// <summary>
    /// Gets the <c>-D</c>/<c>--dump-header</c> stream alone, or <see langword="null" />
    /// without <c>-D</c>; never standard output because of <c>-i</c>.
    /// </summary>
    /// <remarks>
    /// A handler writes here what curl 8.21.0 writes to the <c>-D</c> file but does not
    /// print under <c>-i</c>: the gopher selector, which libcurl passes as
    /// <c>CLIENTWRITE_HEADER</c>, and each FTP, IMAP, POP3 and SMTP server response
    /// line, which it passes as <c>CLIENTWRITE_INFO</c> (BL-1129). Headers that
    /// <c>-i</c> also shows still go to <see cref="HeaderOutput" />. Under <c>-D</c> both
    /// write to the same file, so writes to either land in the order they were made;
    /// under <c>-D -</c> this is standard output, as curl writes there.
    /// </remarks>
    Stream? DumpHeaderOutput { get; }

    /// <summary>
    /// Gets the data given with <c>-d</c>/<c>--data</c>, or <see langword="null" /> when
    /// none was given.
    /// </summary>
    /// <remarks>
    /// <c>mqtt://</c> reads it and sends it as a PUBLISH instead of subscribing
    /// (ADR-0006). A scheme that has no use for request data ignores it.
    /// </remarks>
    ReadOnlyMemory<byte>? PostData { get; }

    /// <summary>
    /// Gets the user name and password from <c>-u</c>/<c>--user</c>, else from the URL's
    /// user information, or <see langword="null" /> when neither is present.
    /// </summary>
    /// <remarks>
    /// <c>mqtt://</c> reads it and sends it in its CONNECT packet (ADR-0006). A scheme
    /// that does not authenticate ignores it.
    /// </remarks>
    System.Net.NetworkCredential? Credentials { get; }

    /// <summary>
    /// Gets each <c>-t</c>/<c>--telnet-option</c> value verbatim, in command-line order,
    /// or an empty list when none was given.
    /// </summary>
    /// <remarks>
    /// <c>telnet://</c> reads it. The values arrive unvalidated: curl rejects an unknown
    /// option name (exit 48) or a value without <c>=</c> (exit 49) at transfer time,
    /// after connecting, so the telnet handler validates them (ADR-0006).
    /// </remarks>
    IReadOnlyList<string> TelnetOptions { get; }

    /// <summary>
    /// Gets the block size given with <c>--tftp-blksize</c>, as given and unclamped, or
    /// <see langword="null" /> when none was given.
    /// </summary>
    /// <remarks>
    /// <c>tftp://</c> reads it and clamps it to 8-65464, as curl does rather than
    /// refusing an out-of-range value; when it is <see langword="null" /> the TFTP
    /// default of 512 applies (ADR-0006).
    /// </remarks>
    int? TftpBlockSize { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--tftp-no-options</c> was given, which
    /// suppresses the RFC 2347, 2348 and 2349 options; <see langword="false" /> when not
    /// given.
    /// </summary>
    /// <remarks>
    /// <c>tftp://</c> reads it (ADR-0006).
    /// </remarks>
    bool TftpNoOptions { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--disable-epsv</c> was given, which stops an FTP
    /// transfer trying <c>EPSV</c> before <c>PASV</c>; <see langword="false" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0006).
    /// </remarks>
    bool FtpDisableEpsv { get; }

    /// <summary>
    /// Gets a value indicating whether an FTP transfer ignores the address in the server's
    /// <c>PASV</c> reply and connects its data channel to the control channel's address;
    /// <see langword="true" /> unless <c>--no-ftp-skip-pasv-ip</c> was given, as in curl 8.21.0.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0006).
    /// </remarks>
    bool FtpSkipPasvIp { get; }

    /// <summary>
    /// Gets how an FTP transfer reaches the file in the URL's path, per <c>--ftp-method</c>;
    /// <see cref="FtpFileMethod.MultiCwd" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0006).
    /// </remarks>
    FtpFileMethod FtpFileMethod { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--ftp-create-dirs</c> was given, which creates the
    /// missing directories of an FTP upload's path; <see langword="false" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0006).
    /// </remarks>
    bool FtpCreateDirectories { get; }

    /// <summary>
    /// Gets the <c>--ftp-account</c> value verbatim, the account an FTP login sends with
    /// <c>ACCT</c> when the server answers its password with <c>332</c>;
    /// <see langword="null" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it.
    /// </remarks>
    string? FtpAccount { get; }

    /// <summary>
    /// Gets the <c>--ftp-alternative-to-user</c> value verbatim, the command an FTP login
    /// sends in place of <c>USER</c> when the server refuses <c>USER</c>;
    /// <see langword="null" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it.
    /// </remarks>
    string? FtpAlternativeToUser { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--ftp-pret</c> was given, which makes a passive-mode
    /// FTP transfer send <c>PRET</c> before <c>EPSV</c> or <c>PASV</c>;
    /// <see langword="false" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it.
    /// </remarks>
    bool FtpSendPret { get; }

    /// <summary>
    /// Gets a value indicating whether <c>-l</c>/<c>--list-only</c> was given, which lists a
    /// directory by name only (<c>NLST</c> rather than <c>LIST</c>); <see langword="false" />
    /// when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0006).
    /// </remarks>
    bool ListOnly { get; }

    /// <summary>
    /// Gets a value indicating whether <c>-B</c>/<c>--use-ascii</c> was given, which
    /// transfers the file as ASCII text (FTP <c>TYPE A</c> rather than <c>TYPE I</c>);
    /// <see langword="false" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it; an FTP URL's <c>;type=A</c> asks for the same.
    /// </remarks>
    bool UseAscii { get; }

    /// <summary>
    /// Gets a value indicating whether <c>-a</c>/<c>--append</c> was given, which makes an
    /// upload append to the remote file instead of overwriting it; <see langword="false" />
    /// when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> (<c>APPE</c> rather than <c>STOR</c>) and <c>sftp://</c> are to read
    /// it; a download ignores it.
    /// </remarks>
    bool Append { get; }

    /// <summary>
    /// Gets the <c>-P</c>/<c>--ftp-port</c> value verbatim, which makes an FTP transfer use
    /// active mode; <see langword="null" /> for passive mode, when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0102).
    /// </remarks>
    string? FtpPort { get; }

    /// <summary>
    /// Gets a value indicating whether an active-mode FTP transfer tries <c>EPRT</c> before
    /// <c>PORT</c>; <see langword="true" /> unless <c>--disable-eprt</c> was given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0102).
    /// </remarks>
    bool FtpUseEprt { get; }

    /// <summary>
    /// Gets whether a plaintext scheme upgrades to TLS, per <c>--ssl</c>/<c>--ftp-ssl</c>
    /// (<see cref="TransportSecurityLevel.Try" />) and <c>--ssl-reqd</c>/<c>--ftp-ssl-reqd</c>
    /// (<see cref="TransportSecurityLevel.Required" />); <see cref="TransportSecurityLevel.None" />
    /// when neither was given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0102).
    /// </remarks>
    TransportSecurityLevel SslLevel { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--ftp-ssl-control</c> was given, which secures an
    /// FTP transfer's control connection only and leaves its data connections in plaintext;
    /// <see langword="false" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> is to read it (ADR-0102).
    /// </remarks>
    bool FtpSslControlOnly { get; }

    /// <summary>
    /// Gets whether an FTPS session clears TLS from its control connection with <c>CCC</c>
    /// after <c>PROT</c>, and how, per <c>--ftp-ssl-ccc</c> and <c>--ftp-ssl-ccc-mode</c>;
    /// <see cref="FtpCommandChannelClearing.Off" /> when neither was given.
    /// </summary>
    /// <remarks>
    /// <c>ftp://</c> and <c>ftps://</c> are to read it (BL-636, ADR-0280).
    /// </remarks>
    FtpCommandChannelClearing FtpCommandChannelClearing { get; }

    /// <summary>
    /// Gets every <c>-Q</c>/<c>--quote</c> value, verbatim and in command-line order; empty
    /// when none was given.
    /// </summary>
    /// <remarks>
    /// A value keeps its <c>-</c> (after the transfer), <c>+</c> (before the transfer) or
    /// <c>*</c> (failure ignored) prefix: <c>ftp://</c> interprets them (ADR-0006), and so
    /// does <c>sftp://</c>, which drops a <c>+</c> value as curl does (ADR-0247).
    /// </remarks>
    IReadOnlyList<string> QuoteCommands { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--crlf</c> was given, which converts each line
    /// feed in an upload to a carriage return plus line feed; <see langword="false" /> when
    /// not given.
    /// </summary>
    /// <remarks>
    /// It applies to uploads only; a download ignores it. <c>file://</c> reads it, and
    /// measured on curl 8.21.0 the conversion inserts a carriage return before a line feed
    /// only when the byte before that line feed is not already one, so <c>a\r\nb</c> is
    /// sent unchanged, a lone carriage return is left alone, and that state carries across
    /// chunk boundaries. The upload's byte count is the converted count (ADR-0003).
    /// </remarks>
    bool ConvertLineEndings { get; }

    /// <summary>
    /// Gets the permission bits a file created by an upload receives on a POSIX system,
    /// per <c>--create-file-mode</c>; curl's default of <c>0644</c> when not given.
    /// </summary>
    /// <remarks>
    /// Upstream curl applies it to files created remotely by an upload, over
    /// <c>file://</c>, SFTP and SCP; it does not apply to <c>-o</c>/<c>--output</c>.
    /// <c>file://</c> passes it to <see cref="IFileSystem.OpenForWriteAsync" />, where the
    /// process umask still applies, a file that already exists keeps its mode, and
    /// Windows ignores it.
    /// </remarks>
    UnixFileMode CreateFileMode { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--path-as-is</c> was given, which keeps the
    /// <c>.</c> and <c>..</c> segments of the URL's path instead of removing them;
    /// <see langword="false" /> when not given.
    /// </summary>
    /// <remarks>
    /// <c>file://</c> reads it. Measured on curl 8.21.0, <c>file:///Z:/d/dir/../nosuch</c>
    /// fails quoting <c>Z:/d/nosuch</c> without it and <c>Z:/d/dir/../nosuch</c> with it
    /// (ADR-0003, ADR-0010). A handler that does not read it removes dot segments as
    /// curl does by default.
    /// </remarks>
    bool PathAsIs { get; }

    /// <summary>
    /// Gets the longest time the connection phase may take, per <c>--connect-timeout</c>,
    /// or <see langword="null" /> when none was given.
    /// </summary>
    /// <remarks>
    /// It limits only the connection phase; once the connection is made it no longer
    /// applies. The value arrives as given, and a handler that does not use it ignores it
    /// (ADR-0008).
    /// </remarks>
    TimeSpan? ConnectTimeout { get; }

    /// <summary>
    /// Gets the longest time the whole transfer may take, per <c>-m</c>/<c>--max-time</c>,
    /// or <see langword="null" /> when none was given.
    /// </summary>
    /// <remarks>
    /// It limits the whole transfer, connection phase included. The value arrives as
    /// given, and a handler that does not use it ignores it (ADR-0008).
    /// </remarks>
    TimeSpan? MaxTime { get; }

    /// <summary>
    /// Gets the <see cref="TimeProvider.GetTimestamp" /> value on <see cref="TimeProvider" />
    /// at which the whole operation began, or <see langword="null" /> when it begins with
    /// this call.
    /// </summary>
    /// <remarks>
    /// <c>-m</c> limits the whole operation, so a handler honouring <see cref="MaxTime" />
    /// should count it from here, and print the operation's elapsed time from here, as curl
    /// counts from its <c>t_startop</c>. The HTTP and TFTP handlers do. <c>Curl.Core</c>'s redirect
    /// follower sets it on every hop after the first, so a <c>-L</c> chain shares one
    /// <c>-m</c> (ADR-0040).
    /// </remarks>
    long? OperationStarted { get; }

    /// <summary>
    /// Gets the proxy selected for this transfer, or <see langword="null" /> when the
    /// transfer connects directly.
    /// </summary>
    /// <remarks>
    /// It is scheme-neutral: a handler that connects over TCP passes it to
    /// <see cref="ConnectTarget" /> for its control connection, and the connector opens the
    /// tunnel (ADR-0056). <see cref="HttpRequestOptions.ForwardProxy" /> is still the HTTP
    /// handler's input; both are set from the one selection, so they cannot disagree.
    /// </remarks>
    ProxyEndpoint? Proxy { get; }

    /// <summary>
    /// Gets the HTTP-only options, or <see langword="null" /> when no HTTP option was
    /// given.
    /// </summary>
    /// <remarks>
    /// An HTTP handler treats <see langword="null" /> exactly as
    /// <c>new HttpRequestOptions()</c>, every member at its default; every other handler
    /// ignores it (ADR-0014).
    /// </remarks>
    HttpRequestOptions? Http { get; }

    /// <summary>
    /// Gets the mail-only options, or <see langword="null" /> for every scheme but
    /// <c>smtp</c>, <c>smtps</c>, <c>pop3</c>, <c>pop3s</c>, <c>imap</c> and <c>imaps</c>.
    /// </summary>
    /// <remarks>
    /// A mail handler treats <see langword="null" /> exactly as
    /// <c>new MailRequestOptions()</c>, every member at its default; every other handler
    /// ignores it (ADR-0121).
    /// </remarks>
    MailRequestOptions? Mail { get; }

    /// <summary>
    /// Gets the SSH-only options from <c>--key</c>, <c>--pubkey</c>, <c>--pass</c>,
    /// <c>--hostpubmd5</c>, <c>--hostpubsha256</c>, <c>--compressed-ssh</c> and the
    /// known-hosts check, or <see langword="null" /> for every scheme but <c>scp</c> and
    /// <c>sftp</c>.
    /// </summary>
    /// <remarks>
    /// The SSH handler treats <see langword="null" /> exactly as <c>new SshOptions()</c>,
    /// every member at its default; every other handler ignores it (ADR-0122).
    /// </remarks>
    SshOptions? Ssh { get; }

    /// <summary>
    /// Gets the time source. Injected so that timeout and retry behaviour is testable
    /// without a real delay.
    /// </summary>
    TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets where the handler and its connector report transfer events for <c>-v</c>,
    /// <c>--trace</c> and <c>--trace-ascii</c>. Never <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// <see cref="NoTransferEvents.Instance" /> when nobody is listening. A handler that
    /// connects passes it on as <see cref="ConnectTarget.Events" /> (ADR-0046).
    /// </remarks>
    ITransferEvents Events { get; }

    /// <summary>
    /// Gets where the handler and its connector write Curl's own diagnostic log for
    /// <c>--log-level</c>. Never <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// <see cref="NoDiagnosticLog.Instance" /> at <c>--log-level none</c>, and by default,
    /// so an implementation that does not override it writes nothing. A handler that
    /// connects passes it on as <see cref="ConnectTarget.DiagnosticLog" /> (ADR-0222).
    /// </remarks>
    IDiagnosticLog DiagnosticLog => NoDiagnosticLog.Instance;

    /// <summary>
    /// Gets where the handler reports how far the transfer has got, for the progress meter.
    /// Never <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// <see cref="NoTransferProgress.Instance" /> when nobody is listening. A handler calls
    /// <see cref="ITransferProgress.ReportTransferStarted" /> once it is past connect or open,
    /// and may report running byte totals (ADR-0045).
    /// </remarks>
    ITransferProgress Progress { get; }

    /// <summary>
    /// Gets the token that cancels this transfer.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
