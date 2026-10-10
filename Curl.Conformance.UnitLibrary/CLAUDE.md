# Curl.Conformance.UnitLibrary

The harness that runs curl's own upstream test cases (`tests/data/test*`) against
Curl, in process, as data-driven MSTest cases. It follows ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).

Today it holds the test-file parser, the test-file expander, the `sws` emulation and the case runner. `UpstreamTestCaseParser.Parse` reads one test file's
bytes line by line, the way upstream's `getpart.pm` does (`UpstreamTestFileTag` recognises
tag lines), into an `UpstreamTestCase` whose `UpstreamTestSection` parts keep their bodies
and attributes as written, or into an `UpstreamTestCaseParseFailure` naming the section and
line. Bodies stay as written because `runtests.pl` applies `nonewline`, `crlf` and
`mode="text"` where it uses a part, after variable substitution and in a different order
per part; `UpstreamTestSectionLineEndings` holds those transforms (with
`UpstreamTestHeaderLine` guessing header lines for `crlf="headers"`) for the comparison
stage to call. The parser leaves variables and `%if` blocks as written. Tag lines are
recognised by hand, not with `Regex`: source-generated regex code is compiled into this
assembly and would count against its coverage gate.

`UpstreamTestFileExpander.Expand` preprocesses a test file's bytes for one run, before
parsing, the way `runtests.pl`'s `prepro` does: `UpstreamTestConditionalLines` resolves
`%if` / `%else` / `%endif` against the run's feature set, and on each kept line
`UpstreamTestVariableSubstitution` replaces `%NAME` variables with the run's values, then
`UpstreamTestFileInclusions` replaces `%includetext` (then the variables again),
`UpstreamTestInstructions` replaces `%SP`-style character macros and `%b64[]b64%`,
`%hex[]hex%` and `%repeat[]%`, `UpstreamTestFileInclusions` replaces `%include`, and
`UpstreamTestFileContentInstructions` replaces `%sha256b64file[]sha256b64file%` (the base64
SHA-256 of a file) and `%strippemfile[]strippemfile%` (a file's PEM blocks); all four read files
through the delegate the caller passes (`UpstreamCaseRunner` reads the disk). The resulting
`UpstreamTestFileExpansion` lists upstream variables with no value and instructions it does not
carry out (`%days`, a `%repeat` that would produce more than 16 MiB (ADR-0423), and the four
file instructions when given no reader), left as written so the
case can be skipped with a reason;
`Parse()` hands it to the parser.
It works on bytes, not on a parsed case, because a `%if` block can wrap whole parts.

`SwsHttpServerConnector` is the first in-memory test server: an `IConnector` whose
connections emulate upstream's `sws` (`tests/server/sws.c` at `curl-8_21_0`) for one parsed,
expanded case. `SwsHttpRequestFraming` finds where each request ends (headers, then a body
by chunked encoding or `Content-Length`, as sws reads them); `SwsHttpReplySelector` answers
it with `<data>`, or `<dataN>` when the path's last segment is a number over 10000 whose last
four digits are N (`SwsHttpRequestLine`), decoding `base64` and applying `nonewline` as sws's
`getpart` does, or with sws's 404 document for a malformed first line. The connection stays
open until a reply containing `swsclose`, an empty or missing part, or `swsclose` in
`<servercmd>`. `ReceivedBytes` records every byte the client wrote while the server had the
connection open, across connections, for comparison with `<verify><protocol>`.
`SwsServerCommands` reads `<servercmd>`. Framing carries out the commands that change where a
request ends: `auth_required` ends one with no `Authorization:` in it at its headers,
`no-expect` does the same for one with `Expect: 100-continue`, and `skip: N` takes N off its
`Content-Length` (past zero, the request never ends, as sws's `size_t` wraps). Bytes past
such an early end start the next request; `upgrade` ends a request with `Upgrade:` in it at its
headers too. `SwsHttpServerConnection` carries out the rest (ADR-0042): each reply goes out in
writes of up to 20 bytes (`SwsServerSend`), each readable when sws would write it, with
`writedelay: N` ms after each and `<postcmd>` `wait N` seconds (`SwsPostReplyCommands`) after
the last, timed on the `TimeProvider` given to the connector (a wait that wakes more than a
second late, as after a stall on a busy runner, waits 250 ms more so curl's own overdue timers,
such as `-m`, fire first; BL-1321); `idle` answers nothing, and a read
then waits until cancelled; `stream` answers with `a string to stream 01234567890\n` without end
and reads nothing more; `connection-monitor` records `[DISCONNECT]\n` in `ReceivedBytes`
(`SwsServerRecording`, one flag for the server as in sws) when a connection that carried a
request closes; after an `upgrade` reply the connection records raw traffic until the client
has been quiet for one second, then closes. `delay: N` alone is listed in
`UnsupportedServerCommands`, so a case using it can be skipped with a reason: sws applies it
only when it accepts a connection while another's request is part-read, which needs its
single-threaded interleaving of connections, not modelled here, and no case at `curl-8_21_0`
uses it. `SwsHttpReplySelector` then moves the part number as sws does (ADR-0047): the first
of `Authorization: Negotiate` (a counter from the first such request's part, plus one each
time), `Digest` (+1000), NTLM type 3 (+1002), NTLM type 1 (+1001) and, from part 1000 up,
`Basic` (+1) found anywhere in the request applies, except to a chunked request or one with an
unreadable `Content-Length`, which sws stops reading before the rules; after a reply
containing `swsbounce` the next request gets that part plus one; both states are kept across
connections. A `CONNECT host:port HTTP/x.y` request with no number in its path is answered
from `<connect>` / `<connectN>`, and the connection stays open for the tunnelled request.
Connections to `SwsHttpServerConnector.ProxyPort` (8992, the runner's `%PROXYPORT`) stand in
for upstream's `http-proxy` server (BL-1924): they are served the same way but recorded in
`ProxyReceivedBytes`, compared with `<verify><proxy>` after `<strip>` / `<strippart>` as
`<verify><protocol>` is; after a `CONNECT` such a connection records into `ReceivedBytes`, as
upstream's HTTP server logs the tunnelled request. Screening lets `http-proxy` cases run.
No case is skipped for `%PROXYPORT` any more (BL-1897): 17 proxy cases are measured (80, 83, 95,
150, 184, 194, 275, 744, 1078, 1184, 1288, 1297, 1428, 1904, 2050, 2107, 3028), and the other
`%PROXYPORT` cases skip for another reason (a `<tool>`, `<setenv>`, a feature or another server's port).
Every connection a case makes goes through `TcpConnector`'s own code (ADR-0460, BL-1915): the
conformance tests compose curl with `CurlComposition.CreateRunner`, as the command does, and replace
only the dial, `ITcpDialer`, with `InMemoryServerTcpDialer` (in `Curl.Conformance.UnitTests`), which
asks the runner's connector chain for the dialled address and port. So the `CONNECT` request and its
reply's parsing (`HttpProxyTunnel`, `-p` / `--proxytunnel` and HTTPS through an HTTP proxy), the SOCKS
handshakes, the HAProxy PROXY line (`--haproxy-protocol`, `--haproxy-clientip`, written inside an
opened tunnel) and TLS are Curl's real code; the `sws` stand-in only answers them.
`InMemoryServerTcpDialerTests` pins this, and 22 CONNECT-tunnel cases (206, 209, 213, 217, 265, 287,
718, 749, 750, 1008, 1021, 1060, 1061, 1297, 1715, 3028 among them) and 6 PROXY-line cases (1455,
1456, 3028, 3201, 3202, 3220) pass through it.
A read before the client's first write waits for that write, as sws blocks reading the
request (a telnet `-T` session reads while its upload is on its way; BL-1853), and a read while
an `Expect: 100-continue` request still owes its body waits for the client's next write, since
sws never answers 100 (test1070). Under `skip: N`, bytes past the request's end are dropped
unrecorded, as sws's stored request ends there. Otherwise a
read with no reply waiting returns 0, because in memory nothing else can arrive.

`NoListenPortConnector` (BL-1904) stands for the port nothing listens on, the runner's `%NOLISTENPORT` (47, as `runtests.pl` gives it): a connection to it ends as a refused TCP connect does (`ConnectResult.Refused`, exit 7, `Failed to connect to <host>:47 after 0 ms: Could not connect to server`), and every other connection reaches the `sws` emulation it wraps. The runner puts it between `SocksServerConnector` and the `sws` emulation, so a SOCKS CONNECT to `%NOLISTENPORT` fails too: socksd's SOCKS4 reply 91, or SOCKS5 reply 5, then a close.

`SocksServerConnector` (BL-1898) emulates upstream's `socksd` (`tests/server/socksd.c`) on port 8994, the runner's `%SOCKSPORT`, and passes every other connection to the `sws` emulation it wraps: a `SocksServerConnection` answers a SOCKS4 or SOCKS4a request, or a SOCKS5 greeting, the username/password exchange under `method 2` and a CONNECT to an IPv4 address, IPv6 address or host name, then relays both ways to the wrapped server at the requested port, or at `backendport`. `SocksServerConfiguration` reads socksd's `method`, `user`, `password` and `backendport` from `<servercmd>`; wrong credentials get status 1 and a close. Screening lets `socks4` and `socks5` cases run; `<verify><socks>` (the target socksd logs) is not compared yet, so cases verifying it still skip, naming it.

`MqttServerConnector` (BL-1900) emulates upstream's `mqttd` (`tests/server/mqttd.c`) on port 8998, the runner's `%MQTTPORT`, outermost of the runner's connectors, and passes every other connection on. An `MqttServerConnection` reads binary MQTT packets however the writes split them (not on the line-protocol core): a CONNECT passing mqttd's preamble, length and client-id checks gets CONNACK; a SUBSCRIBE gets SUBACK, a PUBLISH of `<reply><data>` (mqttd's default payload, and no SUBACK, when there is none) and DISCONNECT; a client PUBLISH is followed by two bytes read as its DISCONNECT and a close; anything else closes. `MqttServerConfiguration` reads `PUBLISH-before-SUBACK`, `short-PUBLISH` (two bytes short, then a close), `excessive-remaining`, `PINGRESP-as-CONNACK`, `DISCONNECT-malformed`, `error-CONNACK N` and `remlen-CONNACK N` from `<servercmd>`. `ProtocolLog` is mqttd's dump, one `client|server TYPE <remaining length in hex> <hex>` line per packet, which the runner appends to the sws emulation's bytes for `<verify><protocol>`. Screening lets `mqtt` cases run; test1916 and test1917 still skip for their `<tool>`.

`%RTSPPORT` is 8996 (`UpstreamCaseRunner.RtspPort`, BL-1902) and no `rtspd` stand-in stands behind it (ADR-0457): the 9 cases naming it at `curl-8_21_0` are all `<tool>` libtests, so they skip for their `<tool>`, not for the variable, and a connection to 8996 reaches the `sws` emulation like any other port.

`%HTTP6PORT` is 8991 (`UpstreamCaseRunner.Http6Port`, BL-1903) and `%HOST6IP` is `[::1]`: upstream's `http-ipv6` server is the same `sws` emulation, all in memory, so no case needs IPv6 from the machine, and screening lets `http-ipv6` cases run. A connection whose target is an IPv6 address has `::1` at both ends (`SwsHttpServerConnector`), so a `--haproxy-protocol` line reads `PROXY TCP6 ::1 ::1` (test1456), and the conformance tests' resolver answers `ip6-localhost` with `::1`, as the `%RESOLVE` precheck says it does (test241). The 8 cases this opened (240, 241, 242, 263, 1324, 1408, 1456, 3202) pass; the other `http-ipv6` cases skip for another reason (`%CLIENT6IP`, `%CLIENT6IP-NB`, a `<tool>`, a feature).

`LineProtocolServerConnector` is the shared core of the line-protocol stand-ins for upstream's
`tests/ftpserver.pl` (BL-1895), the base that the FTP, SMTP, IMAP and POP3 stand-ins of
BL-1905, BL-1909, BL-1910 and BL-1911 build on. Each connection (`LineProtocolServerConnection`)
gets its own `ILineProtocolResponder` from the factory the connector is given: its `Greeting`
waits to be read from the start, every CRLF-terminated command line the client writes is
answered with a `LineProtocolReply` however the writes split it (a lone line feed stays part of
the line), and a read with nothing waiting waits for the client's next write, as ftpserver.pl
blocks reading a command. A reply that closes the connection makes later reads return 0 and
drops the rest of the client's bytes unrecorded; `ReceivedBytes` records every byte read, across
connections, for `<verify><protocol>` (the shared `SwsServerRecording`).
`LineProtocolServerCommands` reads `<servercmd>`'s `REPLY <command> <text>` lines for the
responders to look up by command name, reading the text as ftpserver.pl's `eval "qq{...}"` does (`\r`, `\n` and `\t` are control characters, `\@` is `@`), so one `REPLY` can send several lines. `FtpControlChannelResponder` (BL-1920) is the FTP
control channel's responder: the `220` curl banner (or `REPLY welcome`), a `REPLY` line's text,
else ftpserver.pl's display text, `PWD` as `CWD` moved it, `500 <command> is not dealt with!`
otherwise, and `500 Unrecognized command` with a close for a line that is not a three- or
four-letter command; `ReceivedCommandLines` keeps each line with its CRLF. The data-connection
commands are BL-1906 to BL-1908's. `FtpServerConnector` (BL-1905) wires it into the runner: connections to port 8993, the runner's `%FTPPORT`, reach a `LineProtocolServerConnector` giving each connection its own `FtpControlChannelResponder` over the case's `<servercmd>`, every other port reaches the connector it wraps (it sits between `SocksServerConnector` and `NoListenPortConnector`), and its `ReceivedBytes` follow the `sws` emulation's for `<verify><protocol>`. Screening lets `ftp` cases run, but skips one whose `<verify><protocol>` has a line starting `EPSV`, `PASV`, `PORT`, `EPRT` or `LPRT`, naming the command, since no data connection is served yet; 20 FTP cases that end before one (104, 113, 114, 119, 125, 148, 195, 196, 225, 226, 229, 289, 295, 340, 402, 1000, 1108, 1120, 1152, 1282) pass. `Pop3Responder` (BL-1927) answers POP3 as ftpserver.pl does: the banner and `+OK` greeting, CAPA (each line with two CRLFs, then a `SASL` line), APOP, AUTH, USER/PASS, STAT, LIST, RETR and TOP (the `<reply>` data as written, then a lone `.`), UIDL, DELE, NOOP, RSET and QUIT, and `-ERR Unrecognized command` with a close for a line that is no command. `Pop3ServerConnector` (BL-1911) wires it in: connections to port 8999, the runner's `%POP3PORT`, each get their own `Pop3Responder` over the case's `<servercmd>` and its `<reply>` parts (with their `crlf` line endings); it sits between the SOCKS stand-in and `ImapServerConnector`, and its `ProtocolLog` (every connection's command lines) follows the IMAP log for `<verify><protocol>`. Screening lets `pop3` cases run and no case is skipped for `%POP3PORT`; 41 POP3 cases pass (480, 850-868, 870-877, 882, 883, 885, 887-890, 892-894, 985, 993, 997), 7 fail on a difference the next gap run files (879, 880, 884, 886, 891, 982, 1319), and the rest skip for `%SRCDIR`, `<setenv>` or the Debug feature. `SmtpResponder` (BL-1925) answers SMTP as ftpserver.pl does: EHLO with `<servercmd>`'s `CAPA` and `AUTH` lines (which `LineProtocolServerCommands` also reads), HELO, MAIL, RCPT, DATA through the lone `.` line (kept raw, terminator included, in `UploadedMessage` for `<verify><upload>`), RSET, VRFY and EXPN (from `<reply>` parts), NOOP, HELP and QUIT, and AUTH only through `REPLY` lines. `SmtpServerConnector` (BL-1909) wires it in: connections to port 8995, the runner's `%SMTPPORT`, each get their own `SmtpResponder` over the case's `<servercmd>` and `<reply>` parts (it sits between the MQTT and SOCKS stand-ins and `FtpServerConnector`); its `ProtocolLog` (every connection's command lines, without the message) follows the FTP bytes for `<verify><protocol>`, and its last `UploadedMessage` is compared with `<verify><upload>` as it is, after no `<strip>`. Screening lets `smtp` cases run; 50 SMTP cases pass (900-906, 908-923, 926, 928-933, 939, 940, 942, 944, 946-949, 951-954, 992, 1711, 3002-3007). `ImapResponder` (BL-1926) answers tagged IMAP commands as ftpserver.pl does: the `* OK` banner, CAPABILITY, LOGIN, SELECT/EXAMINE, FETCH and UID (the `<reply>` data as a `{n}` literal, then `POSTFETCH` text, which `LineProtocolServerCommands` also reads), LIST/LSUB, STATUS, SEARCH, STORE, COPY, CREATE/DELETE/RENAME, NOOP, CHECK, CLOSE, EXPUNGE, IDLE, LOGOUT, and APPEND through its `{n}` literal (kept in `UploadedMessage`); AUTHENTICATE only through `REPLY` lines. Both pick `<reply>` parts with `LineProtocolReplyData`. `ImapServerConnector` (BL-1910) wires it in: connections to port 8997, the runner's `%IMAPPORT`, each get their own `ImapResponder` over the case's `<servercmd>` and its `<reply>` parts, each with the line endings its `crlf` attribute forces (`UpstreamTestPartBodies.Served`); it sits between `Pop3ServerConnector` and `SmtpServerConnector`, its `ProtocolLog` (every connection's tagged command lines, without an `APPEND` literal) follows the SMTP log for `<verify><protocol>`, and its last `APPEND` literal joins the SMTP message as the run's `<verify><upload>` (a case reaches one mail server). Screening lets `imap` cases run and no case is skipped for `%IMAPPORT`; 48 IMAP cases pass (799-803, 805-814, 817-822, 824-831, 837, 839, 841-845, 847-849, 895-897, 984, 1847, 1848, 3206, 3209, 3210), 15 fail on a difference the next gap run files, and the rest skip for a `<tool>`, `<setenv>`, `%SRCDIR` or the Debug feature. `EmulatedServers` lists the `<server>` names whose cases
`UpstreamCaseScreening` lets run: `ftp`, `smtp`, `imap` and `pop3` so far, each other protocol task adding its stand-in's name. Screening skips a case verifying `<upload>` unless its server is `smtp` or `imap`.

`TlsServerStream` (BL-1921) is the TLS layer for the stand-ins of upstream's stunnel-fronted
servers (BL-1912 to BL-1914): `AuthenticateAsync` runs `SslStream`'s server handshake on a
server's in-memory stream with a `TlsServerOptions` certificate, ALPN protocol list (empty for
none, as stunnel by default) and client-certificate request, accepting any client certificate
as stunnel at `verify = 0` does, and disposes the stream if the handshake fails.
`InMemoryDuplexStream.CreatePair` makes the two connected ends it runs over: each end reads,
asynchronously only, what the other writes, and reads 0 once the other end is disposed and
drained. Tests reload a generated certificate through PKCS#12, since Windows Schannel and macOS
reject an ephemeral server key.

`HttpsServerConnector` (BL-1912) stands in for upstream's HTTPS server, `sws` behind stunnel, on
port 8989, the runner's `%HTTPSPORT`, which has a value only when the caller names a certificate
directory: a `TlsServerConnection` runs `TlsServerStream` over an `InMemoryDuplexStream` pair with
the certificate the case's `<server>` line `https [file]` names from `%CERTDIR/certs/`
(`test-localhost.pem` when none, loaded by `LoadCertificate`), offers no ALPN and asks for no client
certificate, as stunnel does, and relays the decrypted requests to an `sws` emulation connection in
turns (request bytes in, every reply byte out), so they land in its `ReceivedBytes` for
`<verify><protocol>`; once sws closes the connection it sends close_notify, as stunnel does. It
sits between `FtpServerConnector` and `NoListenPortConnector`. Screening lets `https` cases run,
but skips one that also names `http-proxy` until the proxy stand-in tunnels to it (BL-1915). On
Windows 25 HTTPS cases pass (300, 303, 304, 306, 309, 311, 312, 325, 364, 410, 414, 417, 474,
1561, 1562, 2009-2011, 2033, 2048, 2070, 2079, 2087, 3023, 3024, ...) and 2035, 2038, 2042,
323 and 1244 fail on a difference the next gap run files; the `!Schannel` cases run on Linux and macOS.

`MailTlsServerConnector` (BL-1914, ADR-0459) stands in for stunnel in front of the mail
stand-ins: ports 9000, 9001 and 9002, the runner's `%SMTPSPORT`, `%IMAPSPORT` and `%POP3SPORT`
(with a value only when the caller names a certificate directory, as `%HTTPSPORT`), reach the
SMTP, IMAP and POP3 stand-ins on their plain ports through a `TlsRelayConnection`, which runs
`TlsServerStream` with `%CERTDIR/certs/test-localhost.pem`, no ALPN and no client certificate
request, and relays both ways at once (the server's greeting comes first), so the decrypted
commands land in the plain stand-in's `ProtocolLog` and `UploadedMessage`; once the plain server
closes it sends close_notify, and once the client closes the relay from the server stops. The
runner puts it between the SOCKS stand-in and `Pop3ServerConnector` only for a case naming
`smtps`, `imaps` or `pop3s`; screening lets those servers run, and records `<verify><upload>`
for `smtps` and `imaps` too. No STARTTLS upgrade is emulated, since ftpserver.pl offers none:
the `--ssl` / `--ssl-reqd` cases on a plain mail server (980, 981, 982, 984, 985) measure curl
against a server that does not offer it. 981, 984, 985, 987, 988 and 989 pass; 980 and 982 fail
on a Curl difference the next gap run files.

`UpstreamTestCertificateGenerator` (BL-1923) writes the files cases read through
`%CERTDIR/certs/`, as upstream's `tests/certs/genserv.pl` does with OpenSSL at build time,
from the vendored `.prm` files (read by `UpstreamCertificateParameters`) and the BCL only: a
P-256 test CA from `test-ca.prm` (`.key`, `.cacert`, `.crt`, `.der`, 6000 days), then for every
other `test-*.prm` a P-256 key and a 300-day certificate the CA signs (`.key`, `.pub.pem`,
`.pub.der`, `.crt`, `.der`, a `.crl` revoking it, and the `.pem` of prm text, key and
certificate), with the subject and `x509v3` extensions the `.prm` names, including
`test-localhost0h.prm`'s raw `DER:` subject alternative name. `.cacert` and `.crt` hold the PEM
block without OpenSSL's text dump. An extension, key usage or name form it does not write
throws `FormatException`.

`UpstreamCaseRunner.RunAsync` runs one case end to end (ADR-0013, decision 4): it expands
the file for an `UpstreamCurlPlatform` (the features Curl reports and its null device),
asks `UpstreamCaseScreening` whether the harness can run it (a `<tool>` case, a server other
than `http`, `file` or `none`, a missing feature, a variable with no value, an unsupported
`<servercmd>` or strip line each skip it with a reason, and so does a file part naming a file
outside the case's log directory; before expansion `UpstreamTestDirectoryComposition` rewrites
`%PWD/%LOGDIR` to `%LOGDIR` (the absolute log directory, so the composition names the file there on
every platform) and `%SRCDIR/libtest/test610.pl` / `test613.pl` to `./libtest/...`, as
`runtests.pl`'s default `$srcdir` names them; any other `%SRCDIR` has no value and skips the case
(ADR-0458, BL-1944); curl's `--output` is `%LOGDIR/curl%TESTNUMBER.out`, as `runtests.pl` names it;
`%PWD` elsewhere has a value only when the caller names a tests
directory, and `%CERTDIR` only when it names a certificate directory: the folder holding
upstream's `certs` folder, since cases name `%CERTDIR/certs/test-ca.crt` (BL-1922; the
conformance tests pass the parent of the `certs` folder `UpstreamTestCertificateGenerator` writes, BL-1923); a log, tests or certificate directory
holding a blank is refused, since commands name them unquoted, GF-0044), writes `<client><file>` parts into
the case's log directory, splits `<client><command>` with `UpstreamCommandLineSplitter` as
the shell `runtests.pl` uses would, and runs curl through an `UpstreamCurlInvocation` against
the `sws` emulation and `UnreachableDatagramConnector`. The invocation's
`EnvironmentVariables` is the whole environment the run reads (BL-1892): each `NAME=value` line
of `<client><setenv>`, after expansion (so `%HOSTIP` and the rest are already replaced), with an
empty value kept as an empty variable and a line with no `=` (or a `#` comment) left out, as
`runtests.pl` unsets such a name; nothing else is in it, so a run without `<setenv>` sees no
variable and nothing is left to restore. The conformance tests hand it to
`CurlComposition.CreateRunner` as its environment reader, never to the process environment, so
parallel cases cannot race. 24 `<setenv>` cases pass (63, 288, 329, 392, 429, 449, 708, 709,
736-738, 1101, 1136, 1143, 1249-1257, 1265); 214, 428, 448, 458 and 755 fail on a Curl
difference (428, 448 and 458 because `--variable %NAME` reads the process environment), and the
rest skip for another reason. The emulation's clock is the real one
only when `CurlTimerOptions` finds a curl timer that races the server (`-m`, `-y`, `-Y`,
`--connect-timeout`, `--expect100-timeout`); otherwise it is a `WaitSkippingTimeProvider`, which
moves on by each wait at once, so `writedelay` and `<postcmd>` `wait` keep their order and take
no real time (ADR-0404, BL-1355). The run is under a time limit from an injected
`TimeProvider` (a run past it cannot be stopped, since curl's runner takes no cancellation
token, so the case fails and the run is abandoned). `UpstreamCaseVerification` compares the `UpstreamCaseRun` against
`<verify>` (protocol after `<strip>` / `<strippart>`, run as `UpstreamPerlSubstitution`s
compiled by `UpstreamRegex`; stdout; stderr; exit code; `<verify><file>`), and
`UpstreamFirstDifference` names the first differing byte and line.
`UpstreamPerlOneLiner` (BL-1930) interprets, by whole-line pattern and with no Perl, the
`%PERL -e` one-liners the vendored cases put in a precheck or postcheck, returning an
`UpstreamPerlOneLinerResult` (exit code and stdout), or null for any other line: the
`stat` modification-time check, the `print ... if('A' ne 'B')` and `$^O` prechecks, test8's
`%HOSTIP !~ /.../` precheck, the `grep` line count over a file, the `printf` loop redirected
to a file, and test1683's numbered-file write and verify loops; `$^O` is passed in. test1083's
`exec '%RESOLVE ...'` form is not interpreted. `UpstreamTest610Script` (BL-1931) emulates
upstream's `tests/libtest/test610.pl` for a `%PERL` line whose program is `test610.pl`: its
`mkdir`, `rmdir`, `rm`, `move` and `gone` verbs, chained on one line and stopping at the first
failure with Perl's `die "$!"` exit code (2 missing, 17 exists, 39 not empty, 255 for `gone` on
an existing path), and the usage or `Unsupported command` text with exit 1.
`UpstreamTest613Script` (BL-1932) emulates `tests/libtest/test613.pl` the same way: `prepare`
makes the listed folder (`asubdir`, `plainfile.txt` and `emptyfile.txt` last written
946728000, the read-only `rofile.txt` 978264000), and `postprocess` removes it (Perl's `die`
exit code when it will not go), then exits 1 unless a named file was last written at a given
time, or rewrites a listing into test613.pl's canonical form sorted from its 57th character.
Screening skips a case whose postcheck runs test1013.pl or test1022.pl, naming the
`../curl-config` they compare with, which Curl does not ship. The runner runs a precheck or
postcheck whose every line is a `%PERL -e` one-liner `UpstreamPerlOneLiner` interprets (`%PERL` is
`perl`, `$^O` is the platform's `OperatingSystemName`; BL-1933): a precheck that prints skips the
case with its first line, one that exits non-zero with `precheck command error`, and a postcheck
that exits non-zero fails it. A check line `%RESOLVE [--ipv4|--ipv6] NAME` (`%RESOLVE` is
`resolve`; BL-1929) runs `UpstreamResolveCheck`, which stands for upstream's `server/resolve`
without any lookup, so the answer is platform-neutral: an IP literal resolves in its own family
only, the names `localhost` (both) and `ip6-localhost` (IPv6) resolve, and anything else prints
`Resolving IPv6 'NAME' didn't work` (or `IPv4`) and exits 1, so the precheck skips the case
with that line; `%HOST6IP` is `[::1]`, which lets test1085 run. Any other check line skips the case, naming it. Nothing calls the two script
emulations yet: their check lines reach the runner as `perl ./libtest/test613.pl ...` and skip
as uninterpreted until BL-1894 routes them. The result is an
`UpstreamCaseOutcome` (passed, failed or skipped, with its detail), which
`UpstreamCaseRatchet.Judge` turns into the `UpstreamCaseVerdict` a test row reports, given
whether the case is on the passing list.

What it is to hold in full, per ADR-0013 decision 2:

- the test-file parser, variable substitution (`%HOSTIP`, `%TESTNUMBER`, `%LOGDIR`, ...)
  and `%if` evaluation;
- the in-memory test servers, reached through `IConnector` and `IDatagramConnector`
  from `Curl.Protocol.Abstractions.UnitLibrary`, which record the bytes they receive;
- the comparison of a case's results against its `<verify>` section.

This library references `Curl.Protocol.Abstractions.UnitLibrary` and no protocol
library. It never opens a socket: every connection goes to an in-memory server. It is
held to the same quality gates as every other `.UnitLibrary` (100% line and branch
coverage, complexity at most 10, CRAP at most 30) and is AOT-compatible like every
production project.
