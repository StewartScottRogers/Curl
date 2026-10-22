using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.LibTest;
using Curl.Conformance.UnitProgram;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Runs one vendored upstream test case through curl in process, the way <c>runtests.pl</c> at
/// <c>curl-8_21_0</c> runs it through the <c>curl</c> binary (ADR-0013, decision 4): expand the
/// file, skip it with a reason when the harness cannot run it, write its <c>&lt;client&gt;</c>
/// files, run the command against the in-memory <c>sws</c> emulation, and compare what came out
/// with <c>&lt;verify&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The command gets the arguments <c>runtests.pl</c> puts before it: <c>--output
/// %LOGDIR/curl%TESTNUMBER.out</c> unless the command's <c>option</c> says <c>no-output</c> or the case
/// verifies <c>&lt;stdout&gt;</c> without <c>force-output</c>, then <c>--include</c> unless it says
/// <c>no-include</c>, then <c>--trace-ascii %LOGDIR/trace%TESTNUMBER</c> (<c>--trace</c> when it says
/// <c>binary-trace</c>), <c>--trace-config all</c> and <c>--trace-time</c>, as upstream's do: no case
/// compares the trace file, but a trace option makes curl print the default config file's
/// <c>Note: Read config file from</c> line that test433 verifies (BL-2017).
/// </para>
/// <para>
/// <c>%LOGDIR</c> is the absolute log directory with forward slashes, so cases can run in
/// parallel, <c>%PWD</c> is the tests directory the caller names (else the source directory, the same release <c>tests</c> folder), with forward slashes, <c>%LIBTESTS</c> is <see cref="LibTestsProgram"/>, and <c>%FILE_PWD</c> is <c>/</c> before a drive-letter <c>%LOGDIR</c> and empty before one starting with <c>/</c>
/// (<see cref="UpstreamTestDirectoryComposition.FilePwdBefore"/>), so <c>file:%FILE_PWD/%LOGDIR/…</c> and
/// <c>file://localhost%FILE_PWD/%LOGDIR/…</c> name the file on every platform. <see cref="UpstreamTestDirectoryComposition"/> first rewrites <c>%PWD/%LOGDIR</c>
/// to <c>%LOGDIR</c>, and <c>%SRCDIR</c> before the two emulated <c>libtest</c> scripts to <c>.</c>;
/// any other <c>%SRCDIR</c> is the source directory the caller names, with forward slashes, and has no value when it names none. As upstream does, standard output and standard error are saved to
/// <c>%LOGDIR/stdout%TESTNUMBER</c> and <c>%LOGDIR/stderr%TESTNUMBER</c> before the comparison,
/// for the cases that verify them as files. <c>%include</c> and <c>%includetext</c> read the file
/// they name by its path, relative to the working directory when not absolute, as nothing when it
/// is not there. <c>%HOSTIP</c> and <c>%CLIENTIP</c> are <c>127.0.0.1</c>, <c>%HTTPPORT</c> is
/// <see cref="HttpPort"/>, <c>%HOST6IP</c> and <c>%CLIENT6IP</c> are <c>[::1]</c>, <c>%CLIENT6IP-NB</c> is <c>::1</c>, <c>%HTTP6PORT</c> is <see cref="Http6Port"/>, <c>%FTP6PORT</c> is <see cref="Ftp6Port"/>, <c>%GOPHER6PORT</c> is <see cref="Gopher6Port"/>, <c>%RESOLVE</c> is the name
/// <see cref="UpstreamResolveCheck"/> emulates in a precheck, <c>%PROXYPORT</c> is <see cref="ProxyPort"/>, <c>%FTPPORT</c> is <see cref="FtpPort"/>, <c>%SMTPPORT</c> is <see cref="SmtpPort"/>, <c>%IMAPPORT</c> is <see cref="ImapPort"/>, <c>%POP3PORT</c> is <see cref="Pop3Port"/>, <c>%SOCKSPORT</c> is <see cref="SocksPort"/>, <c>%MQTTPORT</c> is <see cref="MqttPort"/>, <c>%TFTPPORT</c> is <see cref="TftpPort"/>,<c>%RTSPPORT</c> is <see cref="RtspPort"/>, <c>%HTTPSPORT</c>, <c>%SMTPSPORT</c>, <c>%IMAPSPORT</c>, <c>%POP3SPORT</c>, <c>%FTPSPORT</c>, <c>%HTTPSPROXYPORT</c>, <c>%MQTTSPORT</c>, <c>%GOPHERSPORT</c> and <c>%HTTP2TLSPORT</c> are <see cref="HttpsPort"/>, <see cref="SmtpsPort"/>, <see cref="ImapsPort"/>, <see cref="Pop3sPort"/>, <see cref="FtpsPort"/>, <see cref="HttpsProxyPort"/>, <see cref="MqttsPort"/>, <see cref="GophersPort"/> and <see cref="Http2TlsPort"/> when a certificate directory is named, <c>%GOPHERPORT</c> is <see cref="GopherPort"/>, <c>%DICTPORT</c> is <see cref="DictPort"/>, <c>%TELNETPORT</c> is <see cref="TelnetPort"/>, <c>%DNSPORT</c> is <see cref="DnsPort"/>, <c>%HTTP2PORT</c> is <see cref="Http2Port"/>, <c>%H2CVER</c> is <see cref="H2cVersion"/>, <c>%HTTP3PORT</c> is <see cref="Http3Port"/> when the caller names a certificate directory and gives an <see cref="UpstreamHttp3Server"/>, <c>%NOLISTENPORT</c> is <see cref="NoListenPort"/>, a port that refuses every connection, <c>%HTTPUNIXPATH</c> is <see cref="HttpUnixPath"/>, <c>%SOCKSUNIXPATH</c> is <see cref="SocksServerConnector.SocksUnixPath"/>, <c>%HTTPS-MTLSPORT</c> is <see cref="HttpsServerConnector.HttpsMtlsPort"/> when a certificate directory is named, <c>%FTPTIME2</c> is <c>8</c> (the seconds upstream gives FTP time-out cases on a host whose FTP server checks in under a second), and <c>%VERSION</c> is <see cref="CurlVersion"/>. Every other variable
/// is unknown, so a case that uses one is skipped.
/// </para>
/// </remarks>
/// <param name="runCurl">Runs curl for one invocation and returns its exit code.</param>
/// <param name="platform">The features and null device of the platform Curl runs on.</param>
/// <param name="timeProvider">Measures <paramref name="timeLimit"/> and gives <c>%days[N]</c> its now.</param>
/// <param name="timeLimit">How long a run may take before the case fails.</param>
/// <param name="sshServer">
/// The stand-in for upstream's test <c>sshd</c> (BL-1916), or <see langword="null"/> to leave
/// <c>%SSHPORT</c>, <c>%USER</c>, <c>%SFTP_PWD</c>, <c>%SCP_PWD</c>, <c>%SSHSRVMD5</c> and
/// <c>%SSHSRVSHA256</c> and <c>%SSHKEYALGO</c> without a value, so the SSH cases skip. Given one, <c>%SSHPORT</c> is
/// <see cref="UpstreamSshServer.SshPort"/>, <c>%USER</c> its user, <c>%SFTP_PWD</c> and
/// <c>%SCP_PWD</c> are empty (since <c>%LOGDIR</c> is already absolute), the
/// two fingerprints and <c>%SSHKEYALGO</c> are its host key's, and its client key files are written to
/// <c>%LOGDIR/server/</c> before the run.
/// </param>
/// <param name="http2Server">
/// The stand-in for upstream's <c>http/2</c> server, nghttpx serving h2c in front of sws (BL-2230), or
/// <see langword="null"/> to leave it out, so screening skips a case naming the <c>http/2</c> server.
/// Given one, a fresh server relaying to the case's sws emulation stands on <see cref="Http2Port"/> in
/// front of the case's other stand-ins, and with a certificate directory its TLS server on <see cref="Http2TlsPort"/>.
/// </param>
/// <param name="http3Server">
/// The stand-in for upstream's <c>http/3</c> server, nghttpx-h3 serving HTTP/3 over QUIC in front of sws (BL-2261), or
/// <see langword="null"/> to leave it out, so screening skips a case naming the <c>http/3</c> server. Given one, the
/// platform reports the <c>nghttpx-h3</c> feature, and with a certificate directory <c>%HTTP3PORT</c> is
/// <see cref="Http3Port"/> and a fresh server relaying to the case's sws emulation, presenting
/// <c>%CERTDIR/certs/test-localhost.pem</c>, takes the datagrams to it for a case naming the <c>http/3</c> server.
/// </param>
/// <param name="libTests">
/// The ported libtest programs and the engine they run over (BL-2556), or <see langword="null"/> for none.
/// A case whose <c>&lt;client&gt;&lt;tool&gt;</c> names a ported program (<c>lib%TESTNUMBER</c> or <c>libNNN</c>)
/// runs it in place of curl, with the command's words as its arguments and no <c>--output</c>, <c>--include</c>
/// or trace before them, as <c>runtests.pl</c> runs a tool, and is judged as a curl case is; one whose program is
/// not ported is skipped as <see cref="UpstreamCaseScreening.LibTestNotPorted"/>.
/// </param>
/// <param name="perlCommandScripts">
/// The C# interpretations of the Perl scripts a <c>&lt;command type="perl"&gt;</c> runs, keyed by the script's file
/// name (<c>test1707.pl</c>, <see cref="UpstreamCaseScreening.PerlScriptName"/>; BL-2863), or <see langword="null"/>
/// for none, so every such case is skipped as <c>the harness does not run a perl command</c>. A registered one runs
/// in place of curl, given an invocation whose arguments are the command's words after the script (with
/// <c>%CURL</c> as <see cref="CurlProgram"/>) and the case's stdout and stderr, and the delegate that runs curl in
/// process, which it calls with an invocation of its own for each <c>%CURL</c> run; its exit code is the run's.
/// </param>
/// <param name="unitPrograms">
/// The ported unit programs (BL-2841, ADR-0711), or <see langword="null"/> for none. A case with the <c>unittest</c>
/// feature whose program (<see cref="UpstreamCaseScreening.UnitProgramName"/>) is in the table runs it in place of curl,
/// as a libtest program runs, its failure count the exit code, and is judged as a curl case is; one whose program is not
/// there is skipped as <see cref="UpstreamCaseScreening.UnitProgramNotPorted"/>.
/// </param>
public sealed class UpstreamCaseRunner(
    Func<UpstreamCurlInvocation, Task<int>> runCurl,
    UpstreamCurlPlatform platform,
    TimeProvider timeProvider,
    TimeSpan timeLimit,
    UpstreamSshServer? sshServer = null,
    UpstreamHttp2Server? http2Server = null,
    UpstreamHttp3Server? http3Server = null,
    UpstreamLibTests? libTests = null,
    IReadOnlyDictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>>? perlCommandScripts = null,
    UnitPrograms? unitPrograms = null)
{
    // Every stand-in front, each in a file of its own; the platform reports the features the fronts stand in for.
    private readonly UpstreamStandInFronts fronts = UpstreamStandInFronts.Create(sshServer, http2Server, http3Server);

    private readonly UpstreamCurlPlatform platform = UpstreamStandInFronts.Create(sshServer, http2Server, http3Server).WithFeatures(platform);

    /// <summary>The value of <c>%HTTPPORT</c>; every connection reaches the emulation whatever its port.</summary>
    public const string HttpPort = "8990";

    /// <summary>
    /// The value of <c>%HTTP6PORT</c>, upstream's <c>http-ipv6</c> server on <c>%HOST6IP</c>: connections to
    /// <c>[::1]</c> at this port reach the same sws emulation as <see cref="HttpPort"/>, all in memory, so no
    /// case needs IPv6 from the machine.
    /// </summary>
    public const string Http6Port = "8991";

    /// <summary>The value of <c>%PROXYPORT</c>: connections to this port reach the emulation too, recorded apart for <c>&lt;verify&gt;&lt;proxy&gt;</c>.</summary>
    public const string ProxyPort = "8992";

    /// <summary>The value of <c>%FTPPORT</c>: connections to this port reach the FTP control channel, <see cref="FtpServerConnector"/>, whose commands follow the sws emulation's received bytes for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string FtpPort = "8993";

    /// <summary>The value of <c>%SMTPPORT</c>: connections to this port reach the SMTP server, <see cref="SmtpServerConnector"/>, whose command lines follow the FTP control channel's for <c>&lt;verify&gt;&lt;protocol&gt;</c> and whose last message is compared with <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    public const string SmtpPort = "8995";

    /// <summary>The value of <c>%IMAPPORT</c>: connections to this port reach the IMAP server, <see cref="ImapServerConnector"/>, whose command lines follow the SMTP server's for <c>&lt;verify&gt;&lt;protocol&gt;</c> and whose last <c>APPEND</c> literal is compared with <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    public const string ImapPort = "8997";

    /// <summary>The value of <c>%POP3PORT</c>: connections to this port reach the POP3 server, <see cref="Pop3ServerConnector"/>, whose command lines follow the IMAP server's for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string Pop3Port = "8999";

    /// <summary>The value of <c>%SOCKSPORT</c>: connections to this port reach the socksd emulation, <see cref="SocksServerConnector"/>.</summary>
    public const string SocksPort = "8994";

    /// <summary>The value of <c>%MQTTPORT</c>: connections to this port reach the mqttd emulation, <see cref="MqttServerConnector"/>, whose protocol dump follows the sws emulation's received bytes for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string MqttPort = "8998";

    /// <summary>
    /// The value of <c>%RTSPPORT</c>: connections to this port reach the rtspd emulation, <see cref="RtspServerConnector"/>,
    /// whose requests follow the Gopher emulation's selector lines for <c>&lt;verify&gt;&lt;protocol&gt;</c> (BL-2617).
    /// </summary>
    public const string RtspPort = "8996";

    /// <summary>The value of <c>%SMBPORT</c>: connections to this port reach the smbserver.py emulation, <see cref="SmbServerConnector"/>, which writes no protocol dump (BL-2860).</summary>
    public const string SmbPort = "9020";

    /// <summary>The value of <c>%TFTPPORT</c>: datagrams to this port reach the tftpd emulation, <see cref="TftpServerConnector"/>, whose request dump follows the mqttd emulation's for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string TftpPort = "9003";

    /// <summary>The value of <c>%HTTPSPORT</c> when the caller names a certificate directory: connections to this port reach the sws emulation through TLS, <see cref="HttpsServerConnector"/>, presenting the certificate the case's <c>https</c> server line names from <c>%CERTDIR/certs/</c>.</summary>
    public const string HttpsPort = "18989";

    /// <summary>The value of <c>%SMTPSPORT</c> when the caller names a certificate directory: connections to this port reach the SMTP server through TLS, <see cref="MailTlsServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string SmtpsPort = "9000";

    /// <summary>The value of <c>%IMAPSPORT</c> when the caller names a certificate directory: connections to this port reach the IMAP server through TLS, <see cref="MailTlsServerConnector"/>.</summary>
    public const string ImapsPort = "9001";

    /// <summary>The value of <c>%POP3SPORT</c> when the caller names a certificate directory: connections to this port reach the POP3 server through TLS, <see cref="MailTlsServerConnector"/>.</summary>
    public const string Pop3sPort = "9002";

    /// <summary>The value of <c>%FTPSPORT</c> when the caller names a certificate directory: connections to this port reach the FTP control channel through implicit TLS, <see cref="FtpsServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string FtpsPort = "9007";

    /// <summary>The value of <c>%HTTPSPROXYPORT</c> when the caller names a certificate directory: connections to this port reach the http-proxy port through TLS, <see cref="HttpsProxyServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string HttpsProxyPort = "9010";

    /// <summary>The value of <c>%MQTTSPORT</c> when the caller names a certificate directory: connections to this port reach the mqttd emulation through TLS, <see cref="MqttsServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string MqttsPort = "9013";

    /// <summary>The value of <c>%GOPHERSPORT</c> when the caller names a certificate directory: connections to this port reach the Gopher emulation through TLS, <see cref="GophersServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string GophersPort = "9014";

    /// <summary>The value of <c>%GOPHERPORT</c>: connections to this port reach the Gopher emulation, <see cref="GopherServerConnector"/>, whose selector lines follow the tftpd emulation's dump for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string GopherPort = "9008";

    /// <summary>The value of <c>%DICTPORT</c>: connections to this port reach the dictserver.py stand-in, <see cref="DictServerConnector"/>.</summary>
    public const string DictPort = "9015";

    /// <summary>The value of <c>%TELNETPORT</c>: connections to this port reach the negtelnetserver.py stand-in, <see cref="TelnetServerConnector"/>.</summary>
    public const string TelnetPort = "9016";

    /// <summary>The value of <c>%DNSPORT</c>: datagrams to this port reach the dnsd emulation, <see cref="DnsServerConnector"/>, whose query log is compared with <c>&lt;verify&gt;&lt;dns&gt;</c>.</summary>
    public const string DnsPort = "9017";

    /// <summary>The value of <c>%FTP6PORT</c>, upstream's <c>ftp-ipv6</c> server on <c>%HOST6IP</c>: connections to <c>[::1]</c> at this port reach the same FTP control channel as <see cref="FtpPort"/>, in memory.</summary>
    public const string Ftp6Port = "9011";

    /// <summary>The value of <c>%GOPHER6PORT</c>, upstream's <c>gopher-ipv6</c> server on <c>%HOST6IP</c>: connections to <c>[::1]</c> at this port reach the same Gopher emulation as <see cref="GopherPort"/>, in memory.</summary>
    public const string Gopher6Port = "9012";

    /// <summary>
    /// The value of <c>%HTTP2PORT</c>, upstream's nghttpx front of sws, <see cref="UpstreamHttp2Server.Http2Port"/>. Without an
    /// <see cref="UpstreamHttp2Server"/> nothing stands behind it, so screening skips a case naming the <c>http/2</c> server,
    /// and a connection to this port reaches the sws emulation like any other.
    /// </summary>
    public const string Http2Port = "9009";

    /// <summary>
    /// The value of <c>%HTTP2TLSPORT</c> when the caller names a certificate directory, upstream's nghttpx TLS front of sws,
    /// <see cref="UpstreamHttp2Server.Http2TlsPort"/>: given an <see cref="UpstreamHttp2Server"/> with a TLS server, a connection to
    /// this port speaks HTTP/2 over TLS, ALPN <c>h2</c>, presenting <c>%CERTDIR/certs/test-localhost.pem</c> (BL-2245).
    /// </summary>
    public const string Http2TlsPort = "9018";

    /// <summary>
    /// The value of <c>%H2CVER</c>, the protocol token curl's <c>Upgrade:</c> header names, as <c>runtests.pl</c>
    /// at <c>curl-8_22_0</c> sets it.
    /// </summary>
    public const string H2cVersion = "h2c";

    /// <summary>
    /// The value of <c>%HTTP3PORT</c> when the caller names a certificate directory and gives an
    /// <see cref="UpstreamHttp3Server"/>, <see cref="UpstreamHttp3Server.Http3Port"/>: datagrams to this port reach
    /// upstream's nghttpx-h3 front of sws (BL-2261).
    /// </summary>
    public const string Http3Port = "9019";

    /// <summary>The feature <c>runtests.pl</c> reports when its nghttpx serves HTTP/3, reported when an <see cref="UpstreamHttp3Server"/> is given.</summary>
    public const string Http3ServerFeature = "nghttpx-h3";

    /// <summary>
    /// The value of <c>%HTTPUNIXPATH</c>, upstream's <c>http-unix</c> server: the relative path <c>runtests.pl</c>'s
    /// <c>http.sock</c> has under its relative log directory, short enough for a <c>sockaddr_un</c> wherever the
    /// harness's log directory is. Nothing listens on it: a <c>--unix-socket</c> dial to it reaches the sws emulation
    /// in memory, as a dial to <see cref="HttpPort"/> does.
    /// </summary>
    public const string HttpUnixPath = "log/server/http.sock";

    /// <summary>
    /// The in-memory dial a <c>--unix-socket</c> connection to <paramref name="unixSocketPath"/> makes: the path as
    /// the host and <see cref="HttpPort"/> as the port, since a Unix socket has none, so a dial to
    /// <see cref="HttpUnixPath"/> reaches the sws emulation, as upstream's <c>http-unix</c> server is sws, and a dial to
    /// <see cref="SocksServerConnector.SocksUnixPath"/> reaches the socksd emulation, which answers that host. The
    /// conformance tests' dialer and the gap office's tool both dial with it (BL-2271).
    /// </summary>
    /// <param name="unixSocketPath">The Unix socket path curl dials.</param>
    /// <returns>The target to pass to the case's connector chain.</returns>
    public static ConnectTarget HttpUnixSocketTarget(string unixSocketPath) =>
        new(unixSocketPath, int.Parse(HttpPort, CultureInfo.InvariantCulture), false);

    /// <summary>The value of <c>%NOLISTENPORT</c>: connections to this port are refused, <see cref="NoListenPortConnector"/>.</summary>
    public const string NoListenPort = "47";

    /// <summary>The value of <c>%VERSION</c>: the curl release Curl matches.</summary>
    public const string CurlVersion = "8.22.0";

    /// <summary>
    /// The value of <c>%LIBTESTS</c>, <c>runtests.pl</c>'s path to the libtest runner. No program stands behind
    /// the path: a precheck line <c>./libtest/libtests libNNN args</c> (test678's) runs the ported program
    /// <c>libNNN</c> in process (BL-2634), and screening skips a case whose program is not ported.
    /// </summary>
    public const string LibTestsProgram = "./libtest/libtests";

    /// <summary>
    /// The value of <c>%CURL</c>, <c>runtests.pl</c>'s path to the curl under test. No program stands behind it: a
    /// registered Perl script interpretation that meets this word runs curl in process instead (BL-2863).
    /// </summary>
    public const string CurlProgram = "curl";

    private const string HostAddress = "127.0.0.1";

    // runtests.pl's $HOST6IP: the IPv6 loopback, bracketed for a URL.
    private const string Host6Address = "[::1]";

    // runtests.pl's %CLIENT6IP-NB: the client's IPv6 address unbracketed, as --interface takes it.
    private const string Host6AddressNoBrackets = "::1";

    private static readonly string[] ClientFileParts = ["file", "file1", "file2", "file3", "file4"];

    // What runtests.pl's open reads: nothing when the file is not there.
    private static readonly Func<string, byte[]?> ReadIncludedFile = path => File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <summary>Runs one case.</summary>
    /// <param name="testNumber">The case's number, from its file name.</param>
    /// <param name="testFile">The case's file, as vendored.</param>
    /// <param name="logDirectory">
    /// An empty directory of the case's own, <c>%LOGDIR</c>, by an absolute path with no blank in it;
    /// the caller deletes it.
    /// </param>
    /// <param name="testsDirectory">
    /// The release's <c>tests</c> folder, <c>%PWD</c>, by an absolute path with no blank in it, as
    /// <c>runtests.pl</c>'s working directory; <see langword="null"/> gives <c>%PWD</c> the
    /// <paramref name="sourceDirectory"/>, the same folder, and leaves it without a value when that is
    /// <see langword="null"/> too, so a case that uses it is skipped.
    /// </param>
    /// <param name="certificateDirectory">
    /// The folder holding upstream's <c>certs</c> folder, <c>%CERTDIR</c> (cases name
    /// <c>%CERTDIR/certs/test-ca.crt</c>), by an absolute path with no blank in it;
    /// <see langword="null"/> leaves <c>%CERTDIR</c> without a value, so a case that uses it is skipped.
    /// </param>
    /// <param name="sourceDirectory">
    /// The folder holding the files cases name as <c>%SRCDIR/data/...</c> (the release's <c>tests</c>
    /// folder), <c>%SRCDIR</c>, by an absolute path with no blank in it; <see langword="null"/> leaves
    /// <c>%SRCDIR</c> without a value, so a case that uses it is skipped. The two emulated
    /// <c>libtest</c> scripts keep their <c>./libtest/...</c> names either way.
    /// </param>
    /// <returns>Whether the case passed, failed with its first difference, or was skipped with its reason.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="logDirectory"/>, <paramref name="testsDirectory"/>,
    /// <paramref name="certificateDirectory"/> or <paramref name="sourceDirectory"/> holds a blank, which would split every command that
    /// names <c>%LOGDIR</c>, <c>%PWD</c>, <c>%CERTDIR</c> or <c>%SRCDIR</c> unquoted (test3009's
    /// <c>--output-dir %PWD/not-there</c>, GF-0044), as upstream's relative <c>log/</c> never does.
    /// </exception>
    public async Task<UpstreamCaseOutcome> RunAsync(
        int testNumber, ReadOnlyMemory<byte> testFile, string logDirectory, string? testsDirectory = null, string? certificateDirectory = null, string? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(logDirectory);
        RefuseBlanks(logDirectory, testsDirectory, certificateDirectory, sourceDirectory);

        string logDirectoryVariable = logDirectory.Replace('\\', '/');
        Dictionary<string, string> variables = Variables(testNumber, logDirectoryVariable);
        AddDirectoryVariables(variables, testsDirectory, certificateDirectory, sourceDirectory);
        fronts.AddVariables(variables, certificateDirectory);

        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(UpstreamTestDirectoryComposition.Rewrite(testFile.Span), variables, platform.Features, ReadIncludedFile, timeProvider);
        UpstreamTestCaseParseResult parsed = expansion.Parse();
        if (!parsed.IsParsed)
        {
            return UpstreamCaseOutcome.Skipped(parsed.Failure.Message);
        }

        return Screen(expansion, parsed.TestCase, testNumber, logDirectory)
            ?? await RunScreenedAsync(parsed.TestCase, testNumber, logDirectoryVariable, certificateDirectory).ConfigureAwait(false);
    }

    // The skip outcome of a case the harness cannot run here, or null when it can run: first a
    // case only another platform's Curl has the features for (ADR-0488), then every other reason.
    // A ported unit program meets its case's unittest, Debug and TrackMemory features (ADR-0711).
    private UpstreamCaseOutcome? Screen(UpstreamTestFileExpansion expansion, UpstreamTestCase testCase, int testNumber, string logDirectory)
    {
        IReadOnlySet<string> features = UpstreamCaseScreening.CaseFeatures(testCase, testNumber, platform.Features, IsPortedUnitProgram);
        if (UpstreamCaseScreening.FindFeatureOfAnotherPlatform(testCase, features) is { } otherPlatformReason)
        {
            return UpstreamCaseOutcome.SkippedOnThisPlatform(otherPlatformReason);
        }

        return (UpstreamCaseScreening.FindSkipReason(expansion, testCase, features, emulatesHttp2: http2Server is not null, emulatesHttp3: http3Server is not null, isPortedLibTest: program => libTests?.Programs.Find(program) is not null, interpretsPerlScript: script => perlCommandScripts?.ContainsKey(script) == true, isPortedUnitProgram: IsPortedUnitProgram, testNumber: testNumber)
                ?? UpstreamCaseScreening.FindFileOutsideLogDirectory(testCase, logDirectory)) is { } reason
            ? UpstreamCaseOutcome.Skipped(reason)
            : null;
    }

    private bool IsPortedUnitProgram(string program) => unitPrograms?.Find(program) is not null;

    private static void RefuseBlanks(string logDirectory, string? testsDirectory, string? certificateDirectory, string? sourceDirectory)
    {
        RefuseABlank(logDirectory, "log directory", nameof(logDirectory));
        RefuseABlank(testsDirectory ?? string.Empty, "tests directory", nameof(testsDirectory));
        RefuseABlank(certificateDirectory ?? string.Empty, "certificate directory", nameof(certificateDirectory));
        RefuseABlank(sourceDirectory ?? string.Empty, "source directory", nameof(sourceDirectory));
    }

    // runtests.pl's working directory, %PWD, is the release's tests folder, which is also its %SRCDIR.
    private static void AddDirectoryVariables(Dictionary<string, string> variables, string? testsDirectory, string? certificateDirectory, string? sourceDirectory)
    {
        AddDirectoryVariable(variables, "PWD", testsDirectory ?? sourceDirectory);
        AddDirectoryVariable(variables, "SRCDIR", sourceDirectory);
        AddDirectoryVariable(variables, "CERTDIR", certificateDirectory);
    }

    private static void RefuseABlank(string directory, string description, string parameterName)
    {
        if (directory.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"The {description} {directory} holds a blank; upstream's commands name it unquoted.", parameterName);
        }
    }

    private static void AddDirectoryVariable(Dictionary<string, string> variables, string name, string? directory)
    {
        if (directory is not null)
        {
            variables[name] = directory.Replace('\\', '/');
        }
    }

    private Dictionary<string, string> Variables(int testNumber, string logDirectory) =>
        new(StringComparer.Ordinal)
        {
            ["HOSTIP"] = HostAddress,
            ["CLIENTIP"] = HostAddress,
            ["HTTPPORT"] = HttpPort,
            ["PROXYPORT"] = ProxyPort,
            ["SOCKSPORT"] = SocksPort,
            ["FTPPORT"] = FtpPort,
            ["SMTPPORT"] = SmtpPort,
            ["IMAPPORT"] = ImapPort,
            ["POP3PORT"] = Pop3Port,
            ["MQTTPORT"] = MqttPort,
            ["RTSPPORT"] = RtspPort,
            ["TFTPPORT"] = TftpPort,
            ["NOLISTENPORT"] = NoListenPort,
            // servers.pm's $ftpchecktime * 8, where the check time is never below 1 second.
            ["FTPTIME2"] = "8",
            ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture),
            ["LOGDIR"] = logDirectory,
            ["FILE_PWD"] = UpstreamTestDirectoryComposition.FilePwdBefore(logDirectory),
            ["VERSION"] = CurlVersion,
            ["DEV_NULL"] = platform.NullDevice,
            ["PERL"] = UpstreamPerlOneLiner.Program,
            ["RESOLVE"] = UpstreamResolveCheck.Program,
            ["LIBTESTS"] = LibTestsProgram,
            ["CURL"] = CurlProgram,
            ["HOST6IP"] = Host6Address,
            ["HTTP6PORT"] = Http6Port,
            ["FTP6PORT"] = Ftp6Port,
            ["CLIENT6IP"] = Host6Address,
            ["CLIENT6IP-NB"] = Host6AddressNoBrackets,
            ["GOPHERPORT"] = GopherPort,
            ["GOPHER6PORT"] = Gopher6Port,
            ["DICTPORT"] = DictPort,
            ["TELNETPORT"] = TelnetPort,
            ["DNSPORT"] = DnsPort,
            ["HTTP2PORT"] = Http2Port,
            ["H2CVER"] = H2cVersion,
            ["HTTPUNIXPATH"] = HttpUnixPath,
            ["SOCKSUNIXPATH"] = SocksServerConnector.SocksUnixPath,
        };

    private async Task<UpstreamCaseOutcome> RunScreenedAsync(UpstreamTestCase testCase, int testNumber, string logDirectory, string? certificateDirectory)
    {
        if (await PrecheckSkipReasonAsync(testCase, testNumber).ConfigureAwait(false) is { } skipReason)
        {
            return UpstreamCaseOutcome.Skipped(skipReason);
        }

        string outputFile = $"{logDirectory}/curl{testNumber.ToString(CultureInfo.InvariantCulture)}.out";
        fronts.PrepareLogDirectory(logDirectory);
        WriteClientFiles(testCase);
        ILibTest? libTest = FindLibTest(testCase);
        IUnitProgram? unitProgram = FindUnitProgram(testCase, testNumber);
        bool runsTool = libTest is not null || unitProgram is not null;
        Func<UpstreamCurlInvocation, Task<int>>? perlScript = FindPerlScript(testCase);
        List<string> arguments = ProgramArguments(testCase, runsTool, perlScript is not null, outputFile, $"{logDirectory}/trace{testNumber.ToString(CultureInfo.InvariantCulture)}");

        // On the real clock the server's waits make a case take seconds that a loaded machine
        // stretches past the time limit (test1677's writedelay: 5.5 seconds, BL-1355); with no
        // curl timer to race them, they are skipped.
        TimeProvider serverClock = ServerClock(arguments);
        SwsHttpServerConnector server = new(testCase, serverClock);
        TftpServerConnector tftp = new(testCase, serverClock);
        DnsServerConnector dns = new(testCase, tftp);
        // Not disposed: CurlCommandRunner.RunAsync takes no cancellation token, so a run past the
        // time limit is stopped only at its next exchange with the abandoned server, and may write
        // to these until then. A memory stream holds nothing but its buffer, which the collector takes.
        MemoryStream standardOutput = new();
        MemoryStream standardError = new();
        MemoryStream standardInput = new(StandardInput(testCase));
        UpstreamStandInContext standIns = new(testCase, certificateDirectory, server);
        FtpServerConnector ftp = new(testCase, new NoListenPortConnector(fronts.Wrap(UpstreamStandInStage.Sws, server, standIns)), serverClock);
        SmtpServerConnector smtp = new(testCase, ftp);
        ImapServerConnector imap = new(testCase, smtp);
        Pop3ServerConnector pop3 = new(testCase, imap);
        SocksServerConnector socks = new(testCase, fronts.Wrap(UpstreamStandInStage.Pop3, pop3, standIns));
        MqttServerConnector mqtt = new(testCase, socks);
        RtspServerConnector rtsp = new(testCase, fronts.Wrap(UpstreamStandInStage.Mqtt, mqtt, standIns));
        GopherServerConnector gopher = new(testCase, rtsp);
        TelnetServerConnector telnet = new(new DictServerConnector(fronts.Wrap(UpstreamStandInStage.Gopher, gopher, standIns)));
        IConnector servers = fronts.Wrap(UpstreamStandInStage.Outermost, telnet, standIns);
        // A CONNECT through the http-proxy to a mail port or the FTP control or passive port reaches that stand-in
        // (test1319 to 1321, BL-2011; test714 and test715, BL-2035); one to %HTTPSPORT reaches the HTTPS stand-in, whose TLS handshake travels inside the tunnel (BL-2225).
        server.TunnelServerForPort = port => TunnelledPorts.Contains(port) ? pop3 : null;
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, standardError, standardInput, servers, fronts.WrapDatagram(dns, standIns), EnvironmentVariables(testCase)) { ConnectionListener = ftp.ActiveModeListener };
        (int exitCode, string? failure) = await RunCurlAsync(perlScript is null ? ProgramRun(libTest, unitProgram, testNumber, invocation) : () => perlScript(invocation), server).ConfigureAwait(false);
        if (failure is not null)
        {
            return UpstreamCaseOutcome.Failed(failure);
        }

        byte[] standardOutputBytes = RedirectStandardOutput(testCase, standardOutput.ToArray());
        File.WriteAllBytes($"{logDirectory}/stdout{testNumber}", standardOutputBytes);
        File.WriteAllBytes($"{logDirectory}/stderr{testNumber}", standardError.ToArray());
        // Read before the postcheck, as before: only the uploads and the --output file are read after it.
        // Upstream's servers all append to one server.input, so a case that reaches several
        // (test2002, test2003) sees their lines in the order curl sent them.
        byte[] receivedBytes = UpstreamProtocolDump.Merge(
        [
            (server.ReceivedBytes, server.ReceivedDump),
            (ftp.ReceivedBytes, ftp.ReceivedDump),
            (smtp.ProtocolLog, smtp.ProtocolDump),
            (imap.ProtocolLog, imap.ProtocolDump),
            (pop3.ProtocolLog, pop3.ProtocolDump),
            (mqtt.ProtocolLog, mqtt.ProtocolDump),
            (tftp.ProtocolLog, tftp.ProtocolDump),
            (gopher.ReceivedBytes, gopher.ReceivedDump),
            (rtsp.ReceivedBytes, rtsp.ReceivedDump),
        ]);
        // runner.pm sets $CURLOUT to a tool run's stdout, and runtests.pl compares <reply><data> with $CURLOUT (BL-2560).
        // The --output file is read after the postcheck too: test613.pl rewrites the listing 613 and 614 compare (BL-2855).
        return Judge(testCase, () => new UpstreamCaseRun(exitCode, standardOutputBytes, standardError.ToArray(), receivedBytes, runsTool ? standardOutputBytes : ReadOutputFile(outputFile))
        {
            ReplyDataName = runsTool ? "the program's stdout" : "the --output file",
            ProxyReceivedBytes = server.ProxyReceivedBytes.ToArray(),
            LogDirectory = logDirectory,
            SocksLog = System.Text.Encoding.Latin1.GetBytes(socks.ConnectLog),
            DnsLog = dns.QueryLog,
            // A case reaches one uploading server, so at most one of these holds an upload.
            UploadedBytes = [.. smtp.UploadedMessage.Span, .. imap.UploadedMessage.Span, .. tftp.UploadedBytes.Span, .. ftp.UploadedBytes.Span, .. fronts.UploadedBytes(logDirectory, testNumber)],
        });
    }

    // runtests.pl runs the postcheck, then compares <verify><upload>, so the run is built after the postcheck: it can
    // move the upload into place (tests 624 and 625 upload into a folder and test610.pl moves the file).
    private UpstreamCaseOutcome Judge(UpstreamTestCase testCase, Func<UpstreamCaseRun> buildRun)
    {
        if (FirstFailedCheck(testCase, "verify", "postcheck", result => result.ExitCode != 0) is { } postcheck)
        {
            return UpstreamCaseOutcome.Failed($"postcheck FAILED: exit code {postcheck.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        }

        return UpstreamCaseVerification.FindFirstDifference(testCase, buildRun()) is { } difference
            ? UpstreamCaseOutcome.Failed(difference)
            : UpstreamCaseOutcome.Passed;
    }

    // runtests.pl sets each NAME=value line of every <client><setenv> part (getpart joins parts of one
    // name, as test433 has two) for the run, an empty value as an
    // empty variable, and unsets a NAME with no '='. The run's environment holds nothing else, so
    // nothing is left to restore after it. UpstreamSmallSendsFront reads it too.
    internal static Dictionary<string, string> EnvironmentVariables(UpstreamTestCase testCase)
    {
        Dictionary<string, string> variables = new(StringComparer.Ordinal);
        foreach (string line in testCase.FindAll("client", "setenv").SelectMany(UpstreamTestPartBodies.Lines))
        {
            int equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                variables[line[..equals]] = line[(equals + 1)..];
            }
        }

        return variables;
    }

    private static readonly int[] TunnelledPorts = [Pop3ServerConnector.Pop3Port, ImapServerConnector.ImapPort, SmtpServerConnector.SmtpPort, FtpServerConnector.FtpPort, FtpServerConnector.PassivePort, HttpsServerConnector.HttpsPort];

    private static TimeProvider ServerClock(List<string> arguments) =>
        CurlTimerOptions.AnyIn(arguments) ? TimeProvider.System : new WaitSkippingTimeProvider();

    private static byte[] ReadOutputFile(string outputFile) => File.Exists(outputFile) ? File.ReadAllBytes(outputFile) : [];

    // Any exception curl lets escape is a failure of the case, not of the harness. Curl starts on
    // a thread of its own: against the in-memory server every await can complete at once, so a
    // run that loops would otherwise never return its task, and the time limit would never start.
    // Past the limit the server is abandoned, which stops such a loop at its next exchange.
    // Not the thread pool: a queued work item can wait past the limit on a busy machine
    // (BL-1056), and a looping curl holding a pool thread starves the very continuations that
    // start and fire the limit (BL-1062). On its own thread curl starts at once, and the limit
    // starts here, synchronously, without waiting for the pool.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever curl throws is reported as the case's failure.")]
    private async Task<(int ExitCode, string? Failure)> RunCurlAsync(Func<Task<int>> run, SwsHttpServerConnector server)
    {
        Task<int> curlRun = Task.Factory.StartNew(run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        try
        {
            return (await curlRun.WaitAsync(timeLimit, timeProvider).ConfigureAwait(false), null);
        }
        catch (TimeoutException)
        {
            server.Abandon();
            return (0, $"curl did not finish within {timeLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
        }
        catch (Exception exception)
        {
            return (0, $"curl threw {exception.GetType().Name}: {exception.Message}");
        }
    }

    // runtests.pl skips the case with the first line a precheck prints, or with "precheck command
    // error" when it prints nothing and exits non-zero. Screening let only interpreted one-liners and %LIBTESTS lines
    // running a ported program through.
    private async Task<string?> PrecheckSkipReasonAsync(UpstreamTestCase testCase, int testNumber)
    {
        foreach (string line in UpstreamTestPartBodies.Lines(testCase.Find("client", "precheck")))
        {
            UpstreamPerlOneLinerResult result = UpstreamLibTestCheckLine.Parse(line) is { } libTestCheck
                ? await RunLibTestCheckAsync(testCase, testNumber, libTestCheck.Program, libTestCheck.Arguments).ConfigureAwait(false)
                : RunCheckLine(line);
            if (result.ExitCode != 0 || result.Output.Length > 0)
            {
                return PrecheckSkipReason(result);
            }
        }

        return null;
    }

    private static string PrecheckSkipReason(UpstreamPerlOneLinerResult precheck) =>
        precheck.Output.Length > 0 ? precheck.Output.Split('\n')[0].Replace("\r", "", StringComparison.Ordinal) : "precheck command error";

    // runtests.pl runs a %LIBTESTS precheck line as a command of its own (test678's "lib678 check", BL-2634): the
    // ported program with the line's words after its name, its stdout the line's output; a program that throws or
    // outruns the time limit is a precheck that failed. Its engine reaches a fresh sws emulation, as the servers are up.
    private async Task<UpstreamPerlOneLinerResult> RunLibTestCheckAsync(UpstreamTestCase testCase, int testNumber, string program, string[] arguments)
    {
        SwsHttpServerConnector server = new(testCase, timeProvider);
        MemoryStream standardOutput = new();
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, new MemoryStream(), new MemoryStream(), server, new UnreachableDatagramConnector());
        (int exitCode, string? failure) = await RunCurlAsync(() => RunLibTestAsync(libTests!.Programs.Find(program)!, testNumber, invocation), server).ConfigureAwait(false);
        return new UpstreamPerlOneLinerResult(failure is null ? exitCode : 1, System.Text.Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    private UpstreamPerlOneLinerResult RunCheckLine(string line) =>
        UpstreamPerlCheckLine.RunLine(line, platform.OperatingSystemName) ?? UpstreamResolveCheck.RunLine(line)!;

    private UpstreamPerlOneLinerResult? FirstFailedCheck(UpstreamTestCase testCase, string section, string name, Func<UpstreamPerlOneLinerResult, bool> failed) =>
        UpstreamTestPartBodies.Lines(testCase.Find(section, name))
            .Select(RunCheckLine)
            .FirstOrDefault(failed);

    // Screening let a <tool> case through only when its libtest program is ported, so libTests is given then.
    private ILibTest? FindLibTest(UpstreamTestCase testCase) =>
        UpstreamCaseScreening.LibTestProgramName(testCase) is { } program ? libTests!.Programs.Find(program) : null;

    // Screening let a <command type="perl"> case through only when its script has an interpretation, so perlCommandScripts is given then.
    private Func<UpstreamCurlInvocation, Task<int>>? FindPerlScript(UpstreamTestCase testCase) =>
        UpstreamCaseScreening.PerlScriptName(testCase) is { } script
            ? invocation => perlCommandScripts![script](invocation, runCurl)
            : null;

    // Screening let a unittest case through only when its unit program is ported, so unitPrograms is given then.
    private IUnitProgram? FindUnitProgram(UpstreamTestCase testCase, int testNumber) =>
        UpstreamCaseScreening.UnitProgramName(testCase, testNumber) is { } program ? unitPrograms!.Find(program) : null;

    // runtests.pl runs a libtest or unit program with the command's words alone after its name: no --output, --include or trace;
    // perl gets the words after the script's name. A case with no <command> gets runner.pm's "something silly", "-" (test1658).
    private static List<string> ProgramArguments(UpstreamTestCase testCase, bool runsTool, bool runsPerlScript, string outputFile, string traceFile) =>
        !runsTool && !runsPerlScript
            ? Arguments(testCase, outputFile, traceFile)
            : [.. UpstreamCommandLineSplitter.Split(testCase.Find("client", "command") is { } command ? UpstreamTestPartBodies.Text(command) : "-").Arguments.Skip(runsPerlScript ? 1 : 0)];

    private Func<Task<int>> ProgramRun(ILibTest? libTest, IUnitProgram? unitProgram, int testNumber, UpstreamCurlInvocation invocation)
    {
        if (unitProgram is not null)
        {
            return () => RunUnitProgramAsync(unitProgram, testNumber, invocation);
        }

        return libTest is null ? () => runCurl(invocation) : () => RunLibTestAsync(libTest, testNumber, invocation);
    }

    // The program runs in place of curl, on the same invocation: its engine reaches the case's servers through
    // the invocation's connectors, and its stdout and stderr are the run's.
    private Task<int> RunLibTestAsync(ILibTest libTest, int testNumber, UpstreamCurlInvocation invocation) =>
        RunToolAsync(invocation, async standardError =>
        {
            LibTestContext context = new(testNumber, invocation.Arguments)
            {
                Engine = libTests!.CreateEngine(invocation),
                StandardOutput = invocation.StandardOutput,
                StandardError = standardError,
            };
            return (await libTest.RunAsync(context, CancellationToken.None).ConfigureAwait(false)).ExitCode;
        });

    // A unit program runs from the same first.c main() in curl 8.22.0's tests/units bundle, its failure count the exit code.
    private static Task<int> RunUnitProgramAsync(IUnitProgram unitProgram, int testNumber, UpstreamCurlInvocation invocation) =>
        RunToolAsync(invocation, async standardError =>
        {
            UnitProgramContext context = new(testNumber, invocation.Arguments)
            {
                StandardOutput = invocation.StandardOutput,
                StandardError = standardError,
            };
            return (await unitProgram.RunAsync(context, CancellationToken.None).ConfigureAwait(false)).ExitCode;
        });

    // Around the program go the lines curl 8.22.0's tests/libtest/first.c main() writes on stderr: "URL: <url>" when a
    // URL follows the program's name, before the test, and "Test ended with result <N>" after it (BL-2649; test1549).
    private static async Task<int> RunToolAsync(UpstreamCurlInvocation invocation, Func<TextWriter, Task<int>> runProgram)
    {
        // Line feeds alone, as curl's own stderr in the harness, so a case's <stderr> compares the same on every platform.
        await using StreamWriter standardError = new(invocation.StandardError, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        if (invocation.Arguments.Count > 0)
        {
            await standardError.WriteLineAsync($"URL: {invocation.Arguments[0]}").ConfigureAwait(false);
        }

        int exitCode = await runProgram(standardError).ConfigureAwait(false);
        await standardError.WriteLineAsync($"Test ended with result {exitCode.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        return exitCode;
    }

    private static List<string> Arguments(UpstreamTestCase testCase, string outputFile, string traceFile)
    {
        UpstreamTestSection command = testCase.Find("client", "command")!;
        string option = command.GetAttribute("option") ?? string.Empty;
        List<string> arguments = WritesOutputFile(testCase, option) ? ["--output", outputFile] : [];
        arguments.AddRange(option.Contains("no-include", StringComparison.Ordinal) ? [] : ["--include"]);
        arguments.AddRange(TraceArguments(option, traceFile));
        arguments.AddRange(UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command)).Arguments);
        return arguments;
    }

    // Carries out the command's > or >> redirect (test321). runtests.pl puts its own
    // >%LOGDIR/stdoutN after the command unless the option says no-stdout, and the shell's last
    // redirect of a stream wins, so then the case's file is opened but gets nothing.
    private static byte[] RedirectStandardOutput(UpstreamTestCase testCase, byte[] output)
    {
        UpstreamTestSection command = testCase.Find("client", "command")!;
        UpstreamCommandLine commandLine = UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command));
        if (commandLine.StandardOutputFile is not { } file)
        {
            return output;
        }

        bool runtestsCapturesOutput = !(command.GetAttribute("option") ?? string.Empty).Contains("no-stdout", StringComparison.Ordinal);
        using FileStream stream = new(file, commandLine.AppendsStandardOutput ? FileMode.Append : FileMode.Create);
        stream.Write(runtestsCapturesOutput ? [] : output);
        return runtestsCapturesOutput ? output : [];
    }

    private static bool WritesOutputFile(UpstreamTestCase testCase, string option) =>
        !option.Contains("no-output", StringComparison.Ordinal)
        && (testCase.Find("verify", "stdout") is null || option.Contains("force-output", StringComparison.Ordinal));

    private static string[] TraceArguments(string option, string traceFile) =>
        [option.Contains("binary-trace", StringComparison.Ordinal) ? "--trace" : "--trace-ascii", traceFile, "--trace-config", "all", "--trace-time"];

    private static void WriteClientFiles(UpstreamTestCase testCase)
    {
        foreach (UpstreamTestSection part in ClientFileParts.SelectMany(name => testCase.FindAll("client", name)))
        {
            string path = UpstreamUtf8Words.AsSystemText(part.GetAttribute("name")!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, ClientBytes(part));
        }
    }

    private static byte[] StandardInput(UpstreamTestCase testCase) =>
        testCase.Find("client", "stdin") is { } part
            ? ClientBytes(part)
            : [];

    // A <file> or <stdin> part as runtests.pl writes it: the last line chomped under nonewline,
    // then CRLF forced on every line under crlf="yes" or on header lines under crlf="headers".
    private static byte[] ClientBytes(UpstreamTestSection part) =>
        UpstreamTestPartBodies.WithCrlf(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part), part);
}
