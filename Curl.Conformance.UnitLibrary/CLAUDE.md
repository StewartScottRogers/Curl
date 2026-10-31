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
`%hex[]hex%`, `%repeat[]%` and `%days[N]` (BL-2220: `%alternatives[D,D+60]`, D the epoch second
N days and 30 seconds after the `TimeProvider`'s now rounded down to the minute, as `testutil.pm`
writes it; `UpstreamCaseRunner` passes its own `TimeProvider`, and `UpstreamFirstDifference`
resolves the alternatives with `UpstreamTestAlternatives` as `getpart.pm`'s `compareparts` does,
so cookie-jar cases 31, 46, 61, 483 and 1415 pass), `UpstreamTestFileInclusions` replaces `%include`, and
`UpstreamTestFileContentInstructions` replaces `%sha256b64file[]sha256b64file%` (the base64
SHA-256 of a file) and `%strippemfile[]strippemfile%` (a file's PEM blocks); all four read files
through the delegate the caller passes (`UpstreamCaseRunner` reads the disk). The resulting
`UpstreamTestFileExpansion` lists upstream variables with no value and instructions it does not
carry out (a `%repeat` that would produce more than 16 MiB (ADR-0423), and the four
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
(`SwsServerRecording`) when a connection that served a request while the command was on closes,
once per such connection (BL-2178); after an `upgrade` reply the connection records raw traffic until the client
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
upstream's HTTP server logs the tunnelled request, and under `connection-monitor` its close is
recorded as `[DISCONNECT]` in both logs, as upstream's http-proxy, an sws of its own, logs it too
(test503; BL-2602). A `CONNECT` to `%POP3PORT`, `%IMAPPORT` or `%SMTPPORT` is relayed instead, once its `<connect>` reply keeps the connection open, to that mail stand-in (`SwsHttpServerConnector.TunnelServerForPort`, which the runner sets; BL-2011), as upstream's http-proxy connects to the named port, so the tunnelled commands land in its `ProtocolLog` (test1319, test1320, test1321 pass). A `CONNECT` to `%FTPPORT` or the FTP stand-in's passive port (`FtpServerConnector.FtpPort`, `PassivePort`) is relayed the same way, so FTP's control and data connections through the proxy reach the FTP stand-in (test714, test715 pass; BL-2035). A `CONNECT` to `%HTTPSPORT` is relayed the same way to the HTTPS stand-in (BL-2225). A `<client><connectport>` part is accepted and never read, as `runtests.pl`, `runner.pm` and `servers.pm` at `curl-8_22_0` never read it: the http-proxy `sws` tunnels to the port the `CONNECT` request names, as this stand-in does, so test1316 (FTP `LIST` through the proxy) passes (BL-2239). Screening lets `http-proxy` cases run.
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
the bytes written hold an unfinished request (an `Expect: 100-continue` body still owed, test1070;
a request sent in pieces, test1548) waits for the client's next write, as sws blocks reading it. Under `skip: N`, bytes past the request's end are dropped
unrecorded, as sws's stored request ends there. Otherwise a
read with no reply waiting returns 0, because in memory nothing else can arrive.

`NoListenPortConnector` (BL-1904) stands for the port nothing listens on, the runner's `%NOLISTENPORT` (47, as `runtests.pl` gives it): a connection to it ends as a refused TCP connect does (`ConnectResult.Refused`, exit 7, `Failed to connect to <host>:47 after 0 ms: Could not connect to server`), and every other connection reaches the `sws` emulation it wraps. The runner puts it between `SocksServerConnector` and the `sws` emulation, so a SOCKS CONNECT to `%NOLISTENPORT` fails too: socksd's SOCKS4 reply 91, or SOCKS5 reply 5, then a close.

`SocksServerConnector` (BL-1898) emulates upstream's `socksd` (`tests/server/socksd.c`) on port 8994, the runner's `%SOCKSPORT`, and passes every other connection to the `sws` emulation it wraps: a `SocksServerConnection` answers a SOCKS4 or SOCKS4a request, or a SOCKS5 greeting, the username/password exchange under `method 2` and a CONNECT to an IPv4 address, IPv6 address or host name, then relays both ways to the wrapped server at the requested port, or at `backendport`. `SocksServerConfiguration` reads socksd's `method`, `user`, `password` and `backendport` from `<servercmd>`; wrong credentials get status 1 and a close. Screening lets `socks4` and `socks5` cases run. `SocksServerConnector.ConnectLog` holds the line socksd appends to its dump file for each SOCKS5 CONNECT (`atyp 1 => 12.34.56.78`, `atyp 3 => host.name`, or `atyp 4 =>` and each IPv6 byte as ` %02x`), which the runner passes as `UpstreamCaseRun.SocksLog` and `UpstreamCaseVerification` compares with `<verify><socks>` as written (BL-2222): 719, 720 and 721 pass.

`<verify><errorcode>` may list several codes separated by commas (`77,60`, `35,28`), as `runtests.pl` splits it, and a run passes with any of them; screening skips a case only when an entry is not a number. `<verify><data>` is accepted and never compared, since `runtests.pl` at `curl-8_22_0` never reads it (test301's placeholder `[insert full protocol verifiction dump here]`). 301, 302, 305, 404 and 405 pass (BL-2222).

`UpstreamCommandLineSplitter` reads one `> file` or `>> file` that starts a word as a redirect of standard output (`UpstreamCommandLine.StandardOutputFile`, `AppendsStandardOutput`); a second or mid-word `>` (`2>`) is still unsupported. The runner carries it out after the run: under `option="no-stdout"` the file gets curl's stdout and the run's stdout is empty; otherwise `runtests.pl`'s own later `>%LOGDIR/stdoutN` wins, so the file is opened but gets nothing. Screening skips a redirect outside the log directory. A strip line `s/.../.../ or $_ = ''` empties a line the pattern does not match, as Perl does (`UpstreamPerlSubstitution`). 321 and 1287 pass (BL-2223). A strip line of test1286's form `if(s/TEST//) { $_ = $1 . join('SEP', map { EDIT; ...; $_ } sort split(/SPLIT/, $2)) . $3; }` is carried out by `UpstreamPerlFieldSortProgram` (BL-2238): on a line TEST matches, group 2's fields are split as Perl's `split` does (trailing empty fields dropped), sorted in ordinal order, run through each EDIT substitution and joined by SEP between groups 1 and 3; any other line is left as it is. A strip line `$_ = '' if(($_ !~ /A/) and ($_ =~ /B/) ...)` is carried out by `UpstreamPerlConditionalEmptying` (BL-2641): a line every term holds for is emptied, any other left as it is; 1506, 1510 and 1512 pass (1506 and 1510 once the `Libcurl` engine wrote its verbose lines to the case stderr, BL-2726). All three kinds are an `IUpstreamPerlLineEdit`, whose `Parse` screening and verification share. test1286 is measured and fails on a Curl difference BL-2252 owns (no Digest credentials on the same-host redirect).

`MqttServerConnector` (BL-1900) emulates upstream's `mqttd` (`tests/server/mqttd.c`) on port 8998, the runner's `%MQTTPORT`, just inside the Gopher stand-in, and passes every other connection on. An `MqttServerConnection` reads binary MQTT packets however the writes split them (not on the line-protocol core): a CONNECT passing mqttd's preamble, length and client-id checks gets CONNACK; a SUBSCRIBE gets SUBACK, a PUBLISH of `<reply><data>` (mqttd's default payload, and no SUBACK, when there is none) and DISCONNECT; a client PUBLISH is followed by two bytes read as its DISCONNECT and a close; anything else closes. `MqttServerConfiguration` reads `PUBLISH-before-SUBACK`, `short-PUBLISH` (two bytes short, then a close), `excessive-remaining`, `PINGRESP-as-CONNACK`, `DISCONNECT-malformed`, `error-CONNACK N` and `remlen-CONNACK N` from `<servercmd>`. `ProtocolLog` is mqttd's dump, one `client|server TYPE <remaining length in hex> <hex>` line per packet, which the runner appends to the sws emulation's bytes for `<verify><protocol>`. Screening lets `mqtt` cases run; test1916 and test1917 still skip for their `<tool>`.

`TftpServerConnector` (BL-1901) emulates upstream's `tftpd` (`tests/server/tftpd.c`) over datagrams on port 9003, the runner's `%TFTPPORT` (8996 is `%RTSPPORT`), answering from transfer port 9004; it is the runner's `IDatagramConnector`, and an open to any other UDP port fails as `UnreachableDatagramConnector`'s does. A `TftpServerChannel` answers a read request with 512-byte DATA blocks, each sent once the previous one is acknowledged, after `<servercmd>`'s `writedelay: N` seconds on the runner's server clock; it sends no OACK, as tftpd never does, converts netascii (LF to CRLF, CR to CR NUL), and answers a file name with no number after its last slash with ERROR 2 `Access violation` and anything else with ERROR 4 `Illegal TFTP operation`. The number picks `<data>`, or `<dataN>` as sws does. `ProtocolLog` is tftpd's `server.input` dump (`opcode`, `mode`, each option and `filename`, one `name = value` line each), which the runner appends after the mqttd dump for `<verify><protocol>`. Screening lets `tftp` cases run and none skips for `%TFTPPORT`: 271, 283, 284, 332, 1007, 1009, 1049, 1093, 1094, 1099, 1238 and 1242 pass (BL-2219). A write request is answered with ACK 0 and each DATA block with its ACK; each new block's bytes, as received, go to `UploadedBytes`, the file tftpd writes, which the runner adds to the run's `<verify><upload>` bytes (BL-1952), so screening records `<verify><upload>` for `tftp` too; the upload cases 285, 286 and 1243 pass (BL-2219), and the multi-protocol cases 2002 and 2003 pass now that the servers' dumps interleave in write order (`UpstreamProtocolDump`).

`RtspServerConnector` (BL-2617, ADR-0674) emulates upstream's `rtspd` (`tests/server/rtspd.c` at `curl-8_22_0`) on port 8996, the runner's `%RTSPPORT` (`UpstreamCaseRunner.RtspPort`, BL-1902), just inside the Gopher stand-in, and passes every other connection on. An `RtspServerConnection` ends each request at its empty line plus its first `Content-Length` header's bytes (however the writes split it), records it, answers it with `<data>`, or `<dataN>` when the number after the path's last slash (past any non-digits) is over 10000, as written with `crlf` and `nonewline` applied (an `RTSP/` status line keeps its bare line feed under `crlf="headers"`, as upstream's `subnewlines` keeps it), then sends the part's interleaved RTP packets (`RtspInterleavedPackets`: one per `<servercmd>` line `rtp: part P channel C size S [size_err E]` naming the part, `$`, the channel byte, S + E as a 16-bit length and S bytes repeating `$_1234\n\0Rsdf`). The connection stays open, a read with nothing to answer waiting for the client's next write, until a reply holds `swsclose` or is empty; then reads return 0 once it is read. rtspd's auth part-number rules, `idle`, `stream`, `pipe`, `skip`, 404 document and `<postcmd>` are not emulated, as no `rtsp` case uses them. `ReceivedBytes` follows the Gopher selector lines for `<verify><protocol>`. Screening lets `rtsp` cases run: 567 to 572, 577, 689 and 3100 are measured, and all differ in the `Libcurl` engine, which sends every RTSP transfer as the tool's `OPTIONS *` (BL-2704).

`%HTTP6PORT` is 8991 (`UpstreamCaseRunner.Http6Port`, BL-1903) and `%HOST6IP` is `[::1]`: upstream's `http-ipv6` server is the same `sws` emulation, all in memory, so no case needs IPv6 from the machine, and screening lets `http-ipv6` cases run. A connection whose target is an IPv6 address has `::1` at both ends (`SwsHttpServerConnector`), so a `--haproxy-protocol` line reads `PROXY TCP6 ::1 ::1` (test1456), and the conformance tests' resolver answers `ip6-localhost` with `::1`, as the `%RESOLVE` precheck says it does (test241). The 8 cases this opened (240, 241, 242, 263, 1324, 1408, 1456, 3202) pass; the other `http-ipv6` cases skip for another reason (a `<tool>`, a feature, a precheck). `%CLIENT6IP` is `[::1]` and `%CLIENT6IP-NB` `::1`, and upstream's `ftp-ipv6` and `gopher-ipv6` servers are the FTP and Gopher stand-ins on `%FTP6PORT` 9011 (`FtpServerConnector.Ftp6Port`) and `%GOPHER6PORT` 9012 (`GopherServerConnector.Gopher6Port`), in memory too (BL-2227; servers.pm picks free ports, and these two no other stand-in uses). `LoopbackAddress` gives every in-memory connection to an IPv6 address `::1` at both ends, the line-protocol stand-ins included, so curl's `-P -` sends `EPRT |2|::1|...`; ftpserver.pl answers `EPSV` the same on IPv6. Screening lets `ftp-ipv6` (with ftpserver.pl's `<servercmd>` set and `<verify><upload>`) and `gopher-ipv6` cases run: 252, 253, 254, 255, 1046, 1048, 1050 and 1203 pass (1046 and 1048 with `--interface ::1`, which the in-memory dial accepts), test1083's `if(...) {print ...} else {exec '%RESOLVE ...'}` precheck runs (`UpstreamPerlOneLiner`, BL-2242), and test2086 skips for its `<tool>`.

`GopherServerConnector` (BL-2175) emulates upstream's Gopher server, `sws --gopher`, on port 9008, the runner's `%GOPHERPORT`, outside every other stand-in but the Gophers, dict, telnet, SSH and https-proxy ones, and passes every other connection on. A `GopherServerConnection` reads one selector line up to its CRLF (however the writes split it), records those bytes for `<verify><protocol>` (after the tftpd dump; bytes past the CRLF are dropped), answers with `<data>`, or `<dataN>` when the number after the selector's last slash (past any non-digits, as sws's `strtol` reads it) is over 10000, sent as written with no headers, and closes. Screening lets `gopher` cases run: 1200, 1201 and 1202 pass, 1609 fails on a Curl difference (BL-2185: Curl sends `/sel\r\n` where curl rejects the CR LF with exit 3), and 5028 passes with the small-sends stand-in (BL-2832).

`%HTTP2PORT` is 9009 (`UpstreamCaseRunner.Http2Port`, BL-2175), so no case skips for it. The stand-in for upstream's `nghttpx` h2c front lives in `Curl.Conformance.Http2Server.UnitLibrary` (ADR-0491), which references this library, so the runner reaches it through `UpstreamHttp2Server` (BL-2230), a record the caller builds (`new UpstreamHttp2Server(UpstreamHttp2ServerFactory.Create)`) and passes as `UpstreamCaseRunner`'s optional `http2Server`, as it does `UpstreamSshServer`. Given one, `InFrontOf` puts a fresh h2c server, relaying each stream to the case's `sws` emulation as HTTP/1.1 (so the relayed requests land in its `ReceivedBytes`), on 9009 in front of every stand-in but the https-proxy one, and screening lets a case naming the `http/2` server run; without one screening skips it and a connection to 9009 reaches the `sws` emulation like any other port. `%H2CVER` is `h2c` (`UpstreamCaseRunner.H2cVersion`), as `runtests.pl` sets it. `UpstreamCurlPlatform` reports `http/2` and `h2c` on Linux and macOS only, since Curl's Windows build matches curl's Schannel build, which has no `HTTP2` and refuses `--http2` (ADR-0495): on Windows 358, 471, 1700, 1702, 1800 and 1801 are `SkippedOnThisPlatform`, and they are measured on Linux and macOS. `%HTTP2TLSPORT` is 9018 (`UpstreamCaseRunner.Http2TlsPort`, BL-2245, ADR-0496) and, like the other stunnel-fronted ports, has a value only with a certificate directory: given an `UpstreamHttp2Server` with its optional `CreateTlsServer` (`new UpstreamHttp2Server(UpstreamHttp2ServerFactory.Create, UpstreamHttp2ServerFactory.CreateTls)`), `InFrontOf` puts nghttpx's TLS front there, presenting `%CERTDIR/certs/test-localhost.pem` with ALPN `h2`, loaded only for a case naming the `http/2` server. On Windows 2400-2404 and 2406 are `SkippedOnThisPlatform` for `http/2`; on Linux and macOS 2403 passes (measured on Windows emulating that build, BL-2264) and 2404 skips for its `<tool>` (`lib2404`). The conformance tests report `Debug` to a case whose command names `%HTTP2TLSPORT` (2400, 2401, 2406 use nothing debug-only) and to an `alt-svc` case setting `CURL_ALTSVC_HTTP` (358, 359), which Curl honours behind `CreateRunner(honoursAltSvcHttpVariable: true)`; `FindFeatureOfAnotherPlatform` counts such a stand-in feature, one no platform reports, as met on every platform, so these listed cases are `SkippedOnThisPlatform` on Windows (ADR-0497, BL-2263); test2407 skips first for its `<tool>`.

`%HTTP3PORT` is 9019 (`UpstreamCaseRunner.Http3Port`, BL-2261, ADR-0498) and has a value only when the caller names a certificate directory and gives an `UpstreamHttp3Server`, the record through which the runner reaches the nghttpx-h3 stand-in in `Curl.Conformance.Http3Server.UnitLibrary` (which references this library), passed as `UpstreamCaseRunner`'s optional `http3Server`. Its `CreateServer` takes the case's `sws` emulation and `%CERTDIR/certs/test-localhost.pem`; the conformance tests build it with `UpstreamHttp3ServerFactory.Create` and TLS settings signing with the certificate's P-256 key. Given one, the platform reports `nghttpx-h3` (`UpstreamCaseRunner.Http3ServerFeature`), screening lets a case naming the `http/3` server run, for such a case `InFrontOf` puts a fresh stand-in on datagram port 9019 in front of the dnsd and tftpd stand-ins (so the relayed HTTP/1.1 requests land in the `sws` emulation's `ReceivedBytes`), and `RefusingTcpOnHttp3Port` refuses a TCP connection to 9019, so `--http3`'s race of TCP against QUIC settles on QUIC. `UpstreamCurlPlatform` reports `http/3` on no platform, since Curl refuses `--http3` and `--http3-only` on every reference build, none of which has a QUIC library (ADR-0540): the conformance tests add `http/3` (and `Debug`) with `WithFeatures` only for a case naming the `http/3` server (ADR-0720, BL-2838), so 2500 to 2503 run over the stand-in and are on `PassingUpstreamCases.txt`, held on Linux and macOS and `Inconclusive` on Windows, where the Schannel build's revocation check fails on the test CA.

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
Every server's protocol dump numbers its writes (`UpstreamProtocolDump`): when a case reaches more
than one server, the runner puts their dumps back into the order the writes happened, as upstream's
servers all append to one `server.input` (test2002 and test2003 interleave HTTP, FTP and TFTP, with
FTP's `QUIT` at exit last); with one server the dumps follow each other as described below.
`LineProtocolServerCommands` reads `<servercmd>`'s `REPLY <command> <text>` lines (and
`REPLYLF`, whose text ends in a bare LF, test891) for the responders to look up by command name, reading the text as ftpserver.pl's `eval "qq{...}"` does (`\r`, `\n` and `\t` are control characters, `\x` and up to two hex digits is the character with that code, so test2108's `\x00` sends a NUL byte, `\@` is `@`), so one `REPLY` can send several lines. `FtpControlChannelResponder` (BL-1920) is the FTP
control channel's responder: the `220` curl banner (or `REPLY welcome`), a `REPLY` line's text,
else ftpserver.pl's display text, `PWD` as `CWD` moved it, `500 <command> is not dealt with!`
otherwise, and `500 Unrecognized command` with a close for a line that is not a three- or
four-letter command; `ReceivedCommandLines` keeps each line with its CRLF. `FtpServerConnector` (BL-1905) wires it into the runner: connections to port 8993, the runner's `%FTPPORT`, reach a `LineProtocolServerConnector` giving each connection its own `FtpControlChannelResponder` over the case's `<servercmd>`, every other port reaches the connector it wraps (it sits between `SocksServerConnector` and `NoListenPortConnector`), and its `ReceivedBytes` follow the `sws` emulation's for `<verify><protocol>`. `FtpTransferCommands` (BL-1906, BL-1956) carries out passive mode as ftpserver.pl does, after the command's display text: `PASV` (`227 Entering Passive Mode (127,0,0,1,35,45)`, or `1,2,3,4` under `PASVBADIP`) and `EPSV` (`229 Entering Passive Mode (|||9005|)`) open an `FtpDataConnection`, which `FtpServerConnector` hands, once, to the next connection to `FtpServerConnector.PassivePort` (9005, since ftpserver.pl picks a free port and 8995 is `%SMTPPORT`); `RETR` sends `<reply><dataN>` (the part number from a file number over 10000, `sendzero` honoured) between `150 Binary data connection for ...` and `226 File transfer complete` (`RETRWEIRDO`, `RETRNOSIZE`, `RETRSIZE N` and a preceding `REST` change the 150 line), `LIST` sends `<reply><data>` with CRLF line ends and `NLST` ftpserver.pl's fixed name list, both then `226 ASCII transfer complete`, and each closes the data connection; without one they send nothing after the display text. `SIZE` answers from `<reply><size>` or the data part's length, `MDTM` from `<reply><mdtm>`, and `PRET` stays not dealt with, as ftpserver.pl has no handler for it. A file name loads the case's data only when it names a test number, standing for ftpserver.pl loading that test's file. Active mode and uploads (BL-1907): `FtpServerConnector.ActiveModeListener` is an in-memory `FtpActiveModeListener`, which the runner passes in `UpstreamCurlInvocation.ConnectionListener` and the conformance tests to `CurlComposition.CreateRunner`'s `ftpListener`, so curl's `-P` listens there instead of on a `TcpConnectionListener` (no socket; a listen on port 0 takes 9006). `PORT` (after its display text; `500 silly you, go away` for a line with no six numbers) and `EPRT` (`200 Thanks for dropping by. We contact you later`) connect a new `FtpDataConnection` to the curl listening on the named port, ignoring the address as ftpserver.pl does; port 0 or past 65535, or a port nothing listens on, leaves no data connection. `STOR` and `APPE` answer `125 Gimme gimme gimme!` then `226 File transfer complete` (or `<servercmd>`'s `STOR <text>` line), and hand the data connection over as the upload: every byte curl writes into it is kept, and the last upload is `FtpServerConnector.UploadedBytes`, which the runner adds to the run's `<verify><upload>` bytes. `<servercmd>` (BL-1908): `NODATACONN` (and `NODATACONN425`, `421` and `150`, which imply it) makes no data connection - `PASV` and `EPSV` still answer, but the passive port refuses curl's connect (as it does whenever no data connection is offered), and `PORT` and `EPRT` connect to nothing - and `LIST`, `NLST`, `RETR`, `STOR` and `APPE` then answer `150 Opening data connection` followed by `425 Can't open data connection` or `421 Connection timed out`, or the 150 line alone, or nothing; `COUNT <COMMAND> <n>` takes the command's `REPLY` away after n uses (`LineProtocolServerCommands.TryFindReply`); `SLOWDOWN` is accepted with its bytes unpaced, since no case depends on its 5 ms per byte. `DELAY <COMMAND> <seconds>` (`LineProtocolServerCommands.ReplyDelay`, the command matched as written) sets the `LineProtocolReply.Delay` of that command's reply, which `LineProtocolServerConnection` sends only after the delay on the clock `FtpServerConnector` is given (the runner's server clock, real when `-m` races it; a reply after a delayed one waits for it), and `SLOWDOWNDATA` makes `RETR`, `LIST` and `NLST` send their data one byte every 5 ms on that clock (`FtpDataConnection.SendSlowlyAsync`), so test190 and test1086 pass with exit 28 (BL-1958); `NOSAVE` and the early stop after `STOR <text>` are not emulated either (no case at `curl-8_21_0` uses them). Screening reads `<servercmd>` with ftpserver.pl's command set for an ftp case and sws's otherwise. `%FTPTIME2` is 8, servers.pm's check time (never under 1 second) times 8. A strip replacement is read as a Perl double-quoted string (`\|` is `|`). 147, 280, 1208 and 1209 pass; 1206 and 1207 are measured and differ (Curl sends no `QUIT` after the 425 or 421), 1211 runs past the time limit (no `-m`, curl waits for its active connection), 416 differs (`SIZE` where curl sends `RETR`), and 591 to 594 skip for their `<tool>`. Screening lets `ftp` cases run, but skips one whose `<verify><protocol>` has a line starting `LPRT`, naming it. ftpserver.pl's simulated wildcard directories are emulated (BL-2618): once `CWD` leaves the connection under `/fully_simulated` (`FtpTransferCommands.ChangeDirectory`), `LIST` sends `FtpSimulatedDirectories`' fixed UNIX or DOS listing of `tests/directories.pm` and `RETR` and `SIZE` answer from its file contents (a file the list lacks gets no `RETR` answer, as ftpserver.pl sends none), so 574, 1113, 1162 and 1163 pass; 575 is measured and differs in the `Libcurl` engine (a third round of transfers where libcurl ends with `QUIT`). Of the 46 FTP cases verifying `PORT`, `EPRT`, `STOR` or `APPE`, 22 more pass (101, 103, 107, 108, 109, 112, 116, 128, 144, 145, 235, 236, 248, 251, 348, 362, 475, 476, 1038, 1039, 1055, 1414), 149, 216, 1211 and 1217 are measured and differ, and the rest skip for a `<tool>` or `%FTPTIME2`. Besides the 20 FTP cases that end before a data connection (104, 113, 114, 119, 125, 148, 195, 196, 225, 226, 229, 289, 295, 340, 402, 1000, 1108, 1120, 1152, 1282), passive mode makes 171 more pass (102, 105, 106, 110, 111, 115, 117, 118, 120 to 124, 126, 127, 130 to 143, 1348 to 1363, 1378 to 1393, and the rest listed in `PassingUpstreamCases.txt`). `Pop3Responder` (BL-1927) answers POP3 as ftpserver.pl does: the banner and `+OK` greeting, CAPA (each line with two CRLFs, then a `SASL` line), APOP, AUTH, USER/PASS, STAT, LIST, RETR and TOP (the `<reply>` data as written, then a lone `.`), UIDL, DELE, NOOP, RSET and QUIT, and `-ERR Unrecognized command` with a close for a line that is no command. `Pop3ServerConnector` (BL-1911) wires it in: connections to port 8999, the runner's `%POP3PORT`, each get their own `Pop3Responder` over the case's `<servercmd>` and its `<reply>` parts (with their `crlf` line endings); it sits between the SOCKS stand-in and `ImapServerConnector`, and its `ProtocolLog` (every connection's command lines) follows the IMAP log for `<verify><protocol>`. Screening lets `pop3` cases run and no case is skipped for `%POP3PORT`; 43 POP3 cases pass (480, 850-868, 870-877, 882, 883, 885, 887-894, 985, 993, 997, 1319), 5 fail on a difference the next gap run files (879, 880, 884, 886, 982), and the rest skip for `%SRCDIR`, `<setenv>` or the Debug feature. `SmtpResponder` (BL-1925) answers SMTP as ftpserver.pl does: EHLO with `<servercmd>`'s `CAPA` and `AUTH` lines (which `LineProtocolServerCommands` also reads), HELO, MAIL, RCPT, DATA through the lone `.` line (kept raw, terminator included, in `UploadedMessage` for `<verify><upload>`), RSET, VRFY and EXPN (from `<reply>` parts), NOOP, HELP and QUIT, and AUTH only through `REPLY` lines. `SmtpServerConnector` (BL-1909) wires it in: connections to port 8995, the runner's `%SMTPPORT`, each get their own `SmtpResponder` over the case's `<servercmd>` and `<reply>` parts (it sits between the MQTT and SOCKS stand-ins and `FtpServerConnector`); its `ProtocolLog` (every connection's command lines, without the message) follows the FTP bytes for `<verify><protocol>`, and its last `UploadedMessage` is compared with `<verify><upload>` after `<strippart>` but no `<strip>`, as `runtests.pl` compares it (so test646's random MIME boundary is cut to fixed dashes). Screening lets `smtp` cases run; 50 SMTP cases pass (900-906, 908-923, 926, 928-933, 939, 940, 942, 944, 946-949, 951-954, 992, 1711, 3002-3007). `ImapResponder` (BL-1926) answers tagged IMAP commands as ftpserver.pl does: the `* OK` banner, CAPABILITY, LOGIN, SELECT/EXAMINE, FETCH and UID (the `<reply>` data as a `{n}` literal, then `POSTFETCH` text, which `LineProtocolServerCommands` also reads), LIST/LSUB, STATUS, SEARCH, STORE, COPY, CREATE/DELETE/RENAME, NOOP, CHECK, CLOSE, EXPUNGE, IDLE, LOGOUT, and APPEND through its `{n}` literal (kept in `UploadedMessage`); AUTHENTICATE only through `REPLY` lines. Both pick `<reply>` parts with `LineProtocolReplyData`. `ImapServerConnector` (BL-1910) wires it in: connections to port 8997, the runner's `%IMAPPORT`, each get their own `ImapResponder` over the case's `<servercmd>` and its `<reply>` parts, each with the line endings its `crlf` attribute forces (`UpstreamTestPartBodies.Served`); it sits between `Pop3ServerConnector` and `SmtpServerConnector`, its `ProtocolLog` (every connection's tagged command lines, without an `APPEND` literal) follows the SMTP log for `<verify><protocol>`, and its last `APPEND` literal joins the SMTP message as the run's `<verify><upload>` (a case reaches one mail server). Screening lets `imap` cases run and no case is skipped for `%IMAPPORT`; 48 IMAP cases pass (799-803, 805-814, 817-822, 824-831, 837, 839, 841-845, 847-849, 895-897, 984, 1847, 1848, 3206, 3209, 3210), 15 fail on a difference the next gap run files, and the rest skip for a `<tool>`, `<setenv>`, `%SRCDIR` or the Debug feature. `EmulatedServers` lists the `<server>` names whose cases
`UpstreamCaseScreening` lets run: `ftp`, `smtp`, `imap` and `pop3` so far, each other protocol task adding its stand-in's name. Screening skips a case verifying `<upload>` unless its server is `smtp`, `imap`, `smtps`, `imaps`, `tftp`, `ftp`, `ftps`, `scp` or `sftp`.

`TlsServerStream` (BL-1921) is the TLS layer for the stand-ins of upstream's stunnel-fronted
servers (BL-1912 to BL-1914): `AuthenticateAsync` runs `SslStream`'s server handshake on a
server's in-memory stream with a `TlsServerOptions` certificate, ALPN protocol list (empty for
none, as stunnel by default) and client-certificate request, accepting any client certificate
as stunnel at `verify = 0` does, and disposes the stream if the handshake fails.
`InMemoryDuplexStream.CreatePair` makes the two connected ends it runs over: each end reads,
asynchronously only, what the other writes, and reads 0 once the other end is disposed and
drained, or gets a connection reset (`IOException` carrying `SocketError.ConnectionReset`) when
the disposed end had `ResetsWhenDisposed` set. Tests reload a generated certificate through PKCS#12, since Windows Schannel and macOS
reject an ephemeral server key.
The stand-ins call `AcceptAsync` with `PlatformServesTls13`, false on macOS only, where .NET's
`SslStream` server negotiates at most TLS 1.2 (BL-2335, ADR-0522). Elsewhere it is
`AuthenticateAsync`. On macOS it reads the client's first record: a ClientHello offering only TLS
1.3, with no client certificate asked for, is answered by `Tls13ServerStream`, Curl's own TLS 1.3
server from `Curl.Tls.UnitLibrary` (`Tls13ServerHandshake`: TLS_AES_128_GCM_SHA256, the client's
X25519 share, the certificate's ECDSA key, ECH ignored as stunnel without ECH keys does), so
test4001's `--ech` client exits 101 there as on Windows and Linux; any other first record goes
back to `SslStream` through `ReplayedPrefixStream`. `ShutdownAsync` sends close_notify on either.

`HttpsServerConnector` (BL-1912) stands in for upstream's HTTPS server, `sws` behind stunnel, on
port 18989 (five digits, as upstream's are), the runner's `%HTTPSPORT`, which has a value only when the caller names a certificate
directory: a `TlsServerConnection` runs `TlsServerStream` over an `InMemoryDuplexStream` pair with
the certificate the case's `<server>` line `https [file]` names from `%CERTDIR/certs/`
(`test-localhost.pem` when none, loaded by `LoadCertificate`), offers no ALPN and asks for no client
certificate, as stunnel does, and relays the decrypted requests to an `sws` emulation connection in
turns (request bytes in, every reply byte out), so they land in its `ReceivedBytes` for
`<verify><protocol>`; once sws closes the connection it sends close_notify, as stunnel does, and a
failed handshake (plain HTTP sent to the port, test1244) resets the connection, as stunnel does, so
curl reports exit 56 (BL-2272). It
sits between `FtpServerConnector` and `NoListenPortConnector`. Screening lets `https` cases run,
those that also name `http-proxy` included: a `CONNECT` to `%HTTPSPORT` through the proxy stand-in
is relayed to `HttpsServerConnector`, so the TLS handshake travels inside the tunnel, the `CONNECT`
lands in `ProxyReceivedBytes` and the decrypted requests in `ReceivedBytes` (BL-2225; 1849 and 3106
pass; 780 to 783 pass with the Debug stand-in below, and 446 fails until Curl honours `CURL_HSTS_HTTP`, BL-2250). On
Windows 26 HTTPS cases pass (300, 303, 304, 306, 309, 311, 312, 325, 364, 410, 414, 417, 474,
1244, 1561, 1562, 2009-2011, 2033, 2048, 2070, 2079, 2087, 3023, 3024, ...) and 2035, 2038, 2042
and 323 fail on a difference the next gap run files; the `!Schannel` cases run on Linux and macOS.

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

`HttpsProxyServerConnector` (BL-2226) stands in for upstream's `https-proxy` server, stunnel in
front of the proxy-mode `sws`: port 9010, the runner's `%HTTPSPROXYPORT` (with a value only when
the caller names a certificate directory, as `%HTTPSPORT`), reaches the sws emulation's proxy port
(`SwsHttpServerConnector.ProxyPort`) through a `TlsRelayConnection` presenting
`%CERTDIR/certs/test-localhost.pem`, no ALPN and no client certificate request, so the decrypted
proxied requests and `CONNECT`s land in `ProxyReceivedBytes` for `<verify><proxy>` and a
`CONNECT` is tunnelled as through the http-proxy. The runner puts it in front of every other
stand-in only for a case naming `https-proxy`; screening lets `https-proxy` cases run. The relay
reads the sws side eagerly, and sws returns 0 from a read with no reply waiting, so the proxy
connection is wrapped in `SwsKeptAliveConnection`: while sws still reads requests on it, such a
read waits for the client's next write, as sws blocks reading the next request, and the kept-alive
TLS connection carries curl's next URL (BL-2241). 1586, 1630, 1631, 1632 and 3401 pass; 1647 is a
libtest `<tool>` case and stays screened for that.

`FtpsServerConnector` (BL-1913, ADR-0462) stands in for stunnel in front of the FTP stand-in,
upstream's FTPS server: port 9007, the runner's `%FTPSPORT` (with a value only when the caller
names a certificate directory), speaks TLS from the first byte (implicit FTPS) and reaches
`FtpServerConnector.FtpPort` through a `TlsRelayConnection` with `test-localhost.pem`, so the
decrypted control channel lands in the FTP stand-in's `ReceivedBytes`. Every other connection,
the passive and active data connections included, passes on as it is: stunnel wraps the control
port only, ftpserver.pl answers `PBSZ` and `PROT` with 500, and no `AUTH TLS` upgrade (explicit
FTPS) is emulated, since ftpserver.pl offers none. The runner puts it between the SOCKS stand-in
and `MailTlsServerConnector`'s backend only for a case naming `ftps`; screening lets `ftps` cases
run, reads their `<servercmd>` with ftpserver.pl's command set and records their
`<verify><upload>`. No case skips for `%FTPSPORT`: 400, 401, 403, 406, 407, 408 and 1112
(`SLOWDOWNDATA`) pass, Curl sending `PROT C` under `--ftp-ssl-control` as curl does (BL-2237),
and 404 (exit code `77,60`) passes (BL-2222).
`MqttsServerConnector` and `GophersServerConnector` (BL-2228) stand in for stunnel in front of the
MQTT and Gopher stand-ins: ports 9013 and 9014, the runner's `%MQTTSPORT` and `%GOPHERSPORT` (with a
value only when the caller names a certificate directory; servers.pm picks free ports, and these two
no other stand-in uses), reach `MqttServerConnector.MqttPort` and `GopherServerConnector.GopherPort`
through a `TlsRelayConnection` with `test-localhost.pem`, so the decrypted packets land in the mqttd
dump and the selector in the Gopher stand-in's `ReceivedBytes`. The runner puts each just outside
its plain stand-in only for a case naming `mqtts` or `gophers`; screening lets those servers run,
and `UpstreamCurlPlatform` reports the `mqtts` protocol feature, as Curl's `curl -V` does. 1640 and
1272 pass.
`DictServerConnector` and `TelnetServerConnector` (BL-2243) stand in for upstream's `dict` and
`telnet` servers (`tests/dictserver.py` and `tests/negtelnetserver.py` at `curl-8_22_0`) on ports
9015 and 9016, the runner's `%DICTPORT` and `%TELNETPORT`, just inside the Gopher stand-ins. The
dict stand-in is a `LineProtocolServerConnector` whose responder sends
`220 dictserver <xnooptions> <msgid@msgid>\n` on connect and answers the first command line with
`552 No matches\n` and a close, as dictserver.py answers its one read. A `TelnetServerConnection`
sends negtelnetserver.py's `IAC WILL NEW_ENVIRON`, `IAC WONT NAWS`, `IAC DO NEW_ENVIRON`,
`IAC DONT NAWS`, skips the client's TELNET commands (`IAC` with `WILL`/`WONT`/`DO`/`DONT` and an
option byte, or `IAC` and one other byte) until a write leaves data, echoes that data with
surrounding whitespace stripped, and closes. Neither server writes `server.input`, so neither joins
`UpstreamProtocolDump`. Screening lets `dict` and `telnet` cases run: 1450 and 1452 pass.
`DnsServerConnector` (BL-2232) emulates upstream's `dnsd` (`tests/server/dnsd.c` at `curl-8_22_0`)
over datagrams on port 9017, the runner's `%DNSPORT` (servers.pm picks a free port, and no other
stand-in uses this one); it wraps `TftpServerConnector` as the runner's `IDatagramConnector`, so any
other UDP port reaches the tftpd stand-in. A `DnsServerChannel` answers every query, for any name, as
dnsd does: the query's ID, flags `0x8180`, the question copied, and for an A or AAAA query one record
(`0xc00c`, class IN, TTL 2580) with the last `A:` or `AAAA:` address (brackets allowed) of
`<reply><dns>`, or no record when that line's address does not parse (`A: none`) or is missing; a
datagram under 16 bytes or whose name runs past its end gets no answer. `HTTPS:`, `Delay-*` and
`Rcode-*` lines are not emulated, as no case at `curl-8_22_0` uses them. `QueryLog` holds dnsd's
`dnsd.input` lines (`QNAME <name> QTYPE <A|AAAA|HTTPS|<unknown>>`), which the runner passes as
`UpstreamCaseRun.DnsLog` and `UpstreamCaseVerification` compares with `<verify><dns>` as
`runtests.pl` does: both sorted, the logged lines kept only where the part's `host` attribute matches
as a pattern. The conformance tests compose curl with `CreateRunner(honoursDnsServerVariable: true)`
(ADR-0492), so `CURL_DNS_SERVER` reaches the stand-in through the datagram connector, and report the
`override-dns` feature only to a case naming the `dns` server. Screening lets `dns` cases run: 2102
and 2103 pass, and 2104 fails on a Curl difference BL-2262 owns (Curl asks again for a name that
failed to resolve, where curl caches the failure).
`%HTTPUNIXPATH` is `log/server/http.sock` (`UpstreamCaseRunner.HttpUnixPath`, BL-2229), the relative
path `runtests.pl`'s `http.sock` has under its relative log directory, so it fits a `sockaddr_un`
wherever the harness's log directory is. Nothing listens on it: upstream's `http-unix` server is the
`sws` emulation, and the conformance tests' `InMemoryServerTcpDialer` sends a `--unix-socket` dial to
the connector chain at `%HTTPPORT` through `UpstreamCaseRunner.HttpUnixSocketTarget` (BL-2271, the one
public way to dial it, for the gap office's tool too), so it reaches `SwsHttpServerConnector` through `TcpConnector`'s own
Unix-socket code. Screening lets `http-unix` cases run: 1435 and 1436 pass.
`%SOCKSUNIXPATH` is `/log/server/socks5.sock` (`SocksServerConnector.SocksUnixPath`, BL-2859), absolute as
`runtests.pl` makes it, since curl reads `localhost%SOCKSUNIXPATH` as a host and a path. Upstream's `socks5unix`
server is the socksd emulation: `SocksServerConnector` answers a dial whose host is that path, which the dialer's
`HttpUnixSocketTarget` makes, as it answers `%SOCKSPORT`. Screening lets `socks5unix` cases run: 1467, 1468 and
1470 are listed, `SkippedOnThisPlatform` on Windows (`!win32`) and passing as the Unix build.
`%HTTPS-MTLSPORT` is 9021 (`HttpsServerConnector.HttpsMtlsPort`), with a value only when the caller names a
certificate directory: `UpstreamHttpsMtlsFront` puts an `HttpsServerConnector` there for a case naming
`https-mtls`, presenting `test-localhost.pem` and, through a `TlsClientCertificateVerifier` over
`%CERTDIR/certs/test-ca.crt` (`TlsServerOptions.ClientCertificateVerifier`), demanding a client certificate the
test CA signed, as stunnel at `verify = 2` does: the handshake fails without one, and every certificate presented
is recorded in `ReceivedCertificates`. 2088 and 2089 are listed and run only on Linux and macOS (`!Schannel`).
A `<client><stdout>` part is accepted and never compared, since `runtests.pl` at `curl-8_22_0` reads
stdout only from `<verify>` (BL-2221): 210 passes, and 211 and 212 differ (Curl retries a refused
`EPSV` or `EPRT` on the reused connection; BL-2236).

`UpstreamTestCertificateGenerator` (BL-1923) writes the files cases read through
`%CERTDIR/certs/`, as upstream's `tests/certs/genserv.pl` does with OpenSSL at build time,
from the vendored `.prm` files (read by `UpstreamCertificateParameters`) and the BCL only: a
P-256 test CA from `test-ca.prm` (`.key`, `.cacert`, `.crt`, `.der`, 6000 days), then for every
other `test-*.prm` a P-256 key and a 300-day certificate the CA signs (`.key`, `.pub.pem`,
`.pub.der`, `.crt`, `.der`, a `.crl` revoking it, and the `.pem` of prm text, key and
certificate), with the subject and `x509v3` extensions the `.prm` names, including
`test-localhost0h.prm`'s raw `DER:` subject alternative name. `.cacert` and `.crt` hold the PEM
block without OpenSSL's text dump. An extension, key usage or name form it does not write
throws `FormatException`. `UpstreamCertificateFolder.Prepare(certsSourceDirectory,
outputDirectory, timeProvider)` (BL-2217) is the entry point the conformance tests and the gap
office's `Gap/Tools/Measure-UpstreamCases.cs` share: it runs the generator from a release's
`tests/certs` folder into `<outputDirectory>/certs`, regenerating on every call, and returns
`outputDirectory` to pass to `UpstreamCaseRunner.RunAsync` as `certificateDirectory`. Call it
once per process and reuse the folder.

`UpstreamCaseRunner.RunAsync` runs one case end to end (ADR-0013, decision 4): it expands
the file for an `UpstreamCurlPlatform` (the features Curl reports and its null device),
asks `UpstreamCaseScreening` whether the harness can run it (a `<tool>` case, a server other
than `http`, `file` or `none`, a missing feature, a variable with no value, an unsupported
`<servercmd>` or strip line each skip it with a reason, and so does a file part naming a file
outside the case's log directory; before expansion `UpstreamTestDirectoryComposition` rewrites
`%PWD/%LOGDIR` to `%LOGDIR` (the absolute log directory, so the composition names the file there on
every platform; a file part named `log/<name>` and a `log/<name>` word in `<command>` become `%LOGDIR/<name>` too, as runtests.pl runs in the tests folder, BL-2202) and `%SRCDIR/libtest/test610.pl` / `test613.pl` to `./libtest/...`, as
`runtests.pl`'s default `$srcdir` names them; any other `%SRCDIR` is the `sourceDirectory`
`RunAsync` is given, with forward slashes (`UpstreamConformanceTests` passes `CurlTestCases/Original`,
whose `data/` holds the httpsig key files, BL-2183), and with none it has no value and skips the case
(ADR-0458, BL-1944); `UpstreamCurlPlatform.WithFeatures` adds a feature the harness stands in for
(`Debug` for the httpsig cases, whose fixed `created=0` the tests supply as the signing clock, and for a case naming the `HSTS` feature and setting `CURL_TIME` - 446 and 780 to 783 - whose fixed HSTS clock Curl reads from `CURL_TIME` because the tests pass `CreateRunner(honoursHstsTimeVariable: true)`, ADR-0493, BL-2234); curl's `--output` is `%LOGDIR/curl%TESTNUMBER.out`, as `runtests.pl` names it, and `--trace-ascii %LOGDIR/trace%TESTNUMBER --trace-config all --trace-time` follow `--include` as `runtests.pl` passes them, so the default config file's note prints as test433 expects (BL-2017);
`%PWD` elsewhere is the tests directory the caller names, else its source directory (the same release `tests` folder, as `runtests.pl`'s working directory; so 726, 1145 and 3009 pass, BL-2224), and has no value when it names neither; `%LIBTESTS` is `./libtest/libtests` (`UpstreamCaseRunner.LibTestsProgram`), a path with no runner behind it: a precheck line `./libtest/libtests libNNN args` (test678's `lib678 check`) runs the ported program in process with the words after its name, against a fresh `sws` emulation, and skips the case with the first line it prints, or with `precheck command error` when it prints nothing and exits non-zero, throws or outruns the time limit (`UpstreamLibTestCheckLine`, BL-2634); screening skips a case whose precheck names a program not ported; and `%CERTDIR` only when it names a certificate directory: the folder holding
upstream's `certs` folder, since cases name `%CERTDIR/certs/test-ca.crt` (BL-1922; the
conformance tests pass the parent of the `certs` folder `UpstreamTestCertificateGenerator` writes, BL-1923); a log, tests or certificate directory
holding a blank is refused, since commands name them unquoted, GF-0044), writes `<client><file>` parts into
the case's log directory, splits `<client><command>` with `UpstreamCommandLineSplitter` as
the shell `runtests.pl` uses would, and runs curl through an `UpstreamCurlInvocation` against
the `sws` emulation and `DnsServerConnector` in front of `TftpServerConnector`. The invocation's
`EnvironmentVariables` is the whole environment the run reads (BL-1892): each `NAME=value` line
of every `<client><setenv>` part (`getpart` joins them; BL-2017), after expansion (so `%HOSTIP` and the rest are already replaced), with an
empty value kept as an empty variable and a line with no `=` (or a `#` comment) left out, as
`runtests.pl` unsets such a name; nothing else is in it, so a run without `<setenv>` sees no
variable and nothing is left to restore. The conformance tests hand it to
`CurlComposition.CreateRunner` as its environment reader, never to the process environment, so
parallel cases cannot race. 24 `<setenv>` cases pass (63, 288, 329, 392, 429, 449, 708, 709,
736-738, 1101, 1136, 1143, 1249-1257, 1265); 214, 428, 448, 458 and 755 pass too (BL-2219),
and the
rest skip for another reason. The emulation's clock is the real one
only when `CurlTimerOptions` finds a curl timer that races the server (`-m`, `-y`, `-Y`,
`--connect-timeout`, `--expect100-timeout`); otherwise it is a `WaitSkippingTimeProvider`, which
moves on by each wait at once, so `writedelay` and `<postcmd>` `wait` keep their order and take
no real time (ADR-0404, BL-1355). The run is under a time limit from an injected
`TimeProvider` (a run past it cannot be stopped, since curl's runner takes no cancellation
token, so the case fails and the run is abandoned). `UpstreamCaseVerification` compares the `UpstreamCaseRun` against
`<verify>` (protocol after `<strip>` / `<strippart>`, run as `UpstreamPerlSubstitution`s
compiled by `UpstreamRegex`; stdout; stderr; exit code; `<verify><file>`).
When the expected `<stderr>` names the case's `%LOGDIR` (`UpstreamCaseRun.LogDirectory`), both sides first have curl's 79-column-wrapped `Note:` and `Warning:` lines joined (`UpstreamWrappedMessageLines`, ADR-0475), since the absolute `%LOGDIR` wraps a note `runtests.pl`'s short `log` keeps on one line (test994, test996, test1491). `UpstreamFirstDifference` names the first differing byte and line, then `UpstreamLineDifference` shows the lines that differ (up to two lines of context each side, expected lines marked `-` and produced lines `+`, at most five of each, control characters escaped). `UpstreamCaseDisplayName` names each test row `test<N> [<servers>]: <name>` from `<client><server>` and `<client><name>`, at most 80 characters (BL-2154). `UpstreamCaseDebugSettings` reads `CURL_UPSTREAM_CASE` (the one case the manual test runs) and `CURL_UPSTREAM_KEEP_LOGS=1` (keep every case's log folder) for debugging a case (BL-2155).
`UpstreamPerlOneLiner` (BL-1930) interprets, by whole-line pattern and with no Perl, the
`%PERL -e` one-liners the vendored cases put in a precheck or postcheck, returning an
`UpstreamPerlOneLinerResult` (exit code and stdout), or null for any other line: the
`stat` modification-time check, the `print ... if('A' ne 'B')` and `$^O` prechecks, test8's
`%HOSTIP !~ /.../` precheck, the `grep` line count over a file, the `printf` loop redirected
to a file, and test1683's numbered-file write and verify loops; `$^O` is passed in. test1083's
`if ... else {exec '%RESOLVE ...'}` precheck prints when the addresses differ and otherwise runs `UpstreamResolveCheck` (BL-2242). `UpstreamTest610Script` (BL-1931) emulates
upstream's `tests/libtest/test610.pl` for a `%PERL` line whose program is `test610.pl`: its
`mkdir`, `rmdir`, `rm`, `move` and `gone` verbs, chained on one line and stopping at the first
failure with Perl's `die "$!"` exit code (2 missing, 17 exists, 39 not empty, 255 for `gone` on
an existing path), and the usage or `Unsupported command` text with exit 1.
`UpstreamTest613Script` (BL-1932) emulates `tests/libtest/test613.pl` the same way: `prepare`
makes the listed folder (`asubdir`, `plainfile.txt` and `emptyfile.txt` last written
946728000, the read-only `rofile.txt` 978264000), and `postprocess` removes it (Perl's `die`
exit code when it will not go), then exits 1 unless a named file was last written at a given
time, or rewrites a listing into test613.pl's canonical form sorted from its 57th character.
`UpstreamTest1013Script` and `UpstreamTest1022Script` (BL-2854, ADR-0712) emulate
`tests/libtest/test1013.pl` and `test1022.pl`, which compare `curl --version`'s protocols,
features, version and numeric version with `../curl-config`: `UpstreamCurlConfig` stands in for
that script with the answers of Curl's own `curl-config` (`Curl.Config.UnitLibrary`, over
libcurl's `curl_version_info`), and `UpstreamCurlConfigScriptLine` carries out case 1014's
`> FILE` redirect; 1013, 1014, 1022 and 1023 pass. The runner runs a precheck or
postcheck whose every line is a `%PERL -e` one-liner `UpstreamPerlOneLiner` interprets (`%PERL` is
`perl`, `$^O` is the platform's `OperatingSystemName`; BL-1933): a precheck that prints skips the
case with its first line, one that exits non-zero with `precheck command error`, and a postcheck
that exits non-zero fails it. A check line `%RESOLVE [--ipv4|--ipv6] NAME` (`%RESOLVE` is
`resolve`; BL-1929) runs `UpstreamResolveCheck`, which stands for upstream's `server/resolve`
without any lookup, so the answer is platform-neutral: an IP literal resolves in its own family
only, the names `localhost` (both) and `ip6-localhost` (IPv6) resolve, and anything else prints
`Resolving IPv6 'NAME' didn't work` (or `IPv4`) and exits 1, so the precheck skips the case
with that line; `%HOST6IP` is `[::1]`, which lets test1085 run. A `%PERL` check line goes through `UpstreamPerlCheckLine` (BL-1894), which
runs a `-e` one-liner with `UpstreamPerlOneLiner` and a line whose program is `test610.pl` or
`test613.pl`, in whatever folder (`perl ./libtest/test613.pl ...`), with `UpstreamTest610Script`
or `UpstreamTest613Script`, and a line whose program is `test1013.pl` or `test1022.pl` with
`UpstreamTest1013Script` or `UpstreamTest1022Script`. Any other check line skips the case, naming it. The result is an
`UpstreamCaseOutcome` (passed, failed or skipped, with its detail), which
`UpstreamCaseRatchet.Judge` turns into the `UpstreamCaseVerdict` a test row reports, given
whether the case is on the passing list. Before any other screening the runner asks
`UpstreamCaseScreening.FindFeatureOfAnotherPlatform` whether the case's `<features>` are met by
another platform's Curl (`UpstreamCurlPlatform.All`) and not this one's; such a case is
`UpstreamCaseOutcome.SkippedOnThisPlatform`, which the ratchet reports `Inconclusive` even when
it is listed (ADR-0488, BL-2176).

Each stand-in that wraps, or adds variables or features beside, the runner's plain servers (BL-2884) is an `UpstreamStandInFront` in a file of its own (`UpstreamHttpsFront`, `UpstreamFtpsFront`, `UpstreamMailTlsFront`, `UpstreamMqttsFront`, `UpstreamGophersFront`, `UpstreamHttp2Front`, `UpstreamHttpsProxyFront`, `UpstreamSshFront`, `UpstreamHttp3Front`, and outermost `UpstreamSmallSendsFront`, which splits every connection's writes into `CURL_SMALLREQSEND` and `CURL_SMALLSENDS` pieces for a case setting them, as the debug build sends them; ADR-0721): it names its `UpstreamStandInStage` in the chain, adds its `%NAME` variables and features, wraps the connector built so far, and writes or reads back its own files. `UpstreamStandInFronts.Create` is the one registration table, so a new stand-in is a new file plus a line there; `UpstreamCaseRunner` keeps only the shared code.

The stand-in for upstream's test `sshd` is not here but in its own library,
`Curl.Conformance.SshServer.UnitLibrary` (ADR-0456, BL-1934, BL-1935), because it reuses
the SSH client's internal wire code from `Curl.Original.Protocol.Ssh.UnitLibrary`, which this library
may not reference. Its `SshServerConnector` is an `IConnector` whose sessions run the SSH
transport layer - identification exchange, `curve25519-sha256` key exchange with a fixed
`ssh-ed25519` host key, the client's first implemented cipher and MAC, `NEWKEYS` and the
`ssh-userauth` service request - then user authentication for `curltest`, a session
channel, and `scp` (BL-1917) or the `sftp` subsystem (BL-1918) on it. Its `CLAUDE.md` describes the transport and names the
host keys' stable `--hostpubmd5` and `--hostpubsha256` fingerprints.

The runner reaches it through `UpstreamSshServer` (BL-1916, BL-1954), a record the caller builds
from that library and passes as `UpstreamCaseRunner`'s optional last argument; without one the SSH
variables have no value and the SSH cases skip. Given one, `%SSHPORT` is 9003
(`UpstreamSshServer.SshPort`; TCP, so it does not clash with the TFTP stand-in's UDP 9003) and
`InFrontOf` puts a fresh server from `CreateServer` on that port in front of the case's other
stand-ins; `%USER` is its `User`; `%SFTP_PWD` and `%SCP_PWD` are empty, as `%LOGDIR` is already
absolute; `%SSHSRVMD5` and `%SSHSRVSHA256` are its host key's fingerprints, and `%SSHKEYALGO` its `HostKeyAlgorithm` as a `known_hosts` line names it (`ssh-rsa` from the factory on Windows, `ssh-ed25519` elsewhere and by default; BL-2224); and before curl runs
its client key files are written as `%LOGDIR/server/curl_client_key` and `curl_client_key.pub`,
as `sshserver.pl` writes them. That library's `UpstreamSshServerFactory.Create(windowsBuildOfCurl)`
builds the record (BL-2217), for the conformance tests and the gap office's tool alike: the RSA
client key and RSA host key fingerprints for curl's WinCNG build on Windows, the Ed25519 ones
elsewhere.
Screening lets `scp` and `sftp` cases run (BL-1917, BL-1918): the stand-in runs OpenSSH's `scp` source and sink and `sftp-server` on the
real files under the case's log directory, so a download sends the `<client><file>` the case wrote and
an upload writes `%LOGDIR/upload.%TESTNUMBER`, which the runner, given an SSH server, reads as the
run's `<verify><upload>` bytes (screening records `<verify><upload>` for `scp` and `sftp` too). 606,
607, 628, 629, 630, 631 and 656 pass, and so do the SCP transfers 601, 603, 605, 617, 619, 621, 623,
641, 665 and 3022 and the SFTP transfers and quote commands 600, 602, 604, 608, 609, 611, 612, 615,
616, 618, 620, 622, 624, 625, 626, 627, 633 to 640, 642, 664, 1583, 2007 and 3021, and 632 and 1459,
whose `<features>` name `sftp`, which `UpstreamCurlPlatform` lists with `scp` (BL-2240). 610 and 2004 pass (BL-2219), and 613, 614 and 1446 (BL-2855). The runner reads the upload and the
`--output` file after the case's `<postcheck>`, as `runtests.pl` does, because 624 and 625 upload
into a folder and the postcheck moves the file to `%LOGDIR/upload.%TESTNUMBER` (BL-1955), and
test613.pl's postprocess rewrites 613's and 614's listing into its canonical form (BL-2855).

A case whose `<client><tool>` names a libtest program (`lib%TESTNUMBER` or `libNNN`,
`UpstreamCaseScreening.LibTestProgramName`) runs its ported `ILibTest` in place of curl (BL-2556,
ADR-0656): `UpstreamCaseRunner`'s optional `UpstreamLibTests(Programs, CreateEngine)` names the
ported programs (`LibTestPrograms`, from `Curl.Conformance.LibTest.UnitLibrary`, which this library
references) and makes the `Libcurl` engine each run gets from its `UpstreamCurlInvocation`. The
program gets the command's words alone as its arguments (no `--output`, `--include` or trace), the
invocation's stdout and a line-feed-only stderr writer, runs under the same time limit, and is judged
as a curl run is, except that `<reply><data>` is compared with the program's stdout, since `runner.pm`
at `curl-8_22_0` sets a tool run's `$CURLOUT` to its stdout file (BL-2560; test500's shape). A lib or unit case with no `<client><command>` (test501, test509, test1658) runs its program with the one argument `-`, as `runner.pm` at `curl-8_22_0` puts "something silly" in a missing command (BL-2589, BL-2924; test501 and test1658 pass). Around the program the runner writes on its stderr the lines curl 8.22.0's `tests/libtest/first.c` `main()` writes: `URL: <first argument>` before it, only when the program has an argument (so `URL: -` for a case with no command), and `Test ended with result <exit code>` after it (BL-2649; test1549 passes). A lib case whose program is not ported skips with `libtest program not ported`. A case with the `unittest` feature runs
the unit program its `<tool>` names (or `unit<testnumber>` with none) from the runner's optional
`UnitPrograms` (`Curl.Conformance.UnitProgram.UnitLibrary`; BL-2841, ADR-0711); the platform reports
`unittest`, `Debug` and `TrackMemory` to such a case only when its program is ported, and an unported one
skips with `Curl has no port of <program>`. A `unitNNNN` or `toolNNNN` tool in a case without the
`unittest` feature skips with `the harness does not act on <client><tool>`. Until BL-2558
gives the ratchet a real engine, it passes no `UpstreamLibTests`, so every lib case skips as not ported.

What it is to hold in full, per ADR-0013 decision 2:

- the test-file parser, variable substitution (`%HOSTIP`, `%TESTNUMBER`, `%LOGDIR`, ...)
  and `%if` evaluation;
- the in-memory test servers, reached through `IConnector` and `IDatagramConnector`
  from `Curl.Protocol.Abstractions.UnitLibrary`, which record the bytes they receive;
- the comparison of a case's results against its `<verify>` section.

This library references `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Tls.UnitLibrary`
(for the TLS 1.3 stand-in, ADR-0522), `Curl.Config.UnitLibrary` (for `curl-config`'s answers, ADR-0712), `Libcurl` (whose tables script interpretations read where upstream's libcurl table checks read `curl.h` and `version.c`; BL-2990) and no protocol library. It never opens a socket: every connection goes to an in-memory server. It is
held to the same quality gates as every other `.UnitLibrary` (100% line and branch
coverage, complexity at most 10, CRAP at most 30) and is AOT-compatible like every
production project.
