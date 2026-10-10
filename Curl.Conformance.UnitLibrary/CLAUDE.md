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
A read before the client's first write waits for that write, as sws blocks reading the
request (a telnet `-T` session reads while its upload is on its way; BL-1853), and a read while
an `Expect: 100-continue` request still owes its body waits for the client's next write, since
sws never answers 100 (test1070). Under `skip: N`, bytes past the request's end are dropped
unrecorded, as sws's stored request ends there. Otherwise a
read with no reply waiting returns 0, because in memory nothing else can arrive.

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
responders to look up by command name. `FtpControlChannelResponder` (BL-1920) is the FTP
control channel's responder: the `220` curl banner (or `REPLY welcome`), a `REPLY` line's text,
else ftpserver.pl's display text, `PWD` as `CWD` moved it, `500 <command> is not dealt with!`
otherwise, and `500 Unrecognized command` with a close for a line that is not a three- or
four-letter command; `ReceivedCommandLines` keeps each line with its CRLF. The data-connection
commands are BL-1906 to BL-1908's, and wiring it into `UpstreamCaseRunner` is BL-1905's. `EmulatedServers` lists the `<server>` names whose cases
`UpstreamCaseScreening` lets run; it is empty until a protocol task adds its stand-in's name.

`TlsServerStream` (BL-1921) is the TLS layer for the stand-ins of upstream's stunnel-fronted
servers (BL-1912 to BL-1914): `AuthenticateAsync` runs `SslStream`'s server handshake on a
server's in-memory stream with a `TlsServerOptions` certificate, ALPN protocol list (empty for
none, as stunnel by default) and client-certificate request, accepting any client certificate
as stunnel at `verify = 0` does, and disposes the stream if the handshake fails.
`InMemoryDuplexStream.CreatePair` makes the two connected ends it runs over: each end reads,
asynchronously only, what the other writes, and reads 0 once the other end is disposed and
drained. Tests reload a generated certificate through PKCS#12, since Windows Schannel and macOS
reject an ephemeral server key.

`UpstreamCaseRunner.RunAsync` runs one case end to end (ADR-0013, decision 4): it expands
the file for an `UpstreamCurlPlatform` (the features Curl reports and its null device),
asks `UpstreamCaseScreening` whether the harness can run it (a `<tool>` case, a server other
than `http`, `file` or `none`, a missing feature, a variable with no value, an unsupported
`<servercmd>` or strip line each skip it with a reason, and so does a file part naming a file
outside the case's log directory; `%PWD` has a value only when the caller names a tests
directory, and `%CERTDIR` only when it names a certificate directory: the folder holding
upstream's `certs` folder, since cases name `%CERTDIR/certs/test-ca.crt` (BL-1922; the
conformance tests pass the vendored `UpstreamTestData`); a log, tests or certificate directory
holding a blank is refused, since commands name them unquoted, GF-0044), writes `<client><file>` parts into
the case's log directory, splits `<client><command>` with `UpstreamCommandLineSplitter` as
the shell `runtests.pl` uses would, and runs curl through an `UpstreamCurlInvocation` against
the `sws` emulation and `UnreachableDatagramConnector`. The emulation's clock is the real one
only when `CurlTimerOptions` finds a curl timer that races the server (`-m`, `-y`, `-Y`,
`--connect-timeout`, `--expect100-timeout`); otherwise it is a `WaitSkippingTimeProvider`, which
moves on by each wait at once, so `writedelay` and `<postcmd>` `wait` keep their order and take
no real time (ADR-0404, BL-1355). The run is under a time limit from an injected
`TimeProvider` (a run past it cannot be stopped, since curl's runner takes no cancellation
token, so the case fails and the run is abandoned). `UpstreamCaseVerification` compares the `UpstreamCaseRun` against
`<verify>` (protocol after `<strip>` / `<strippart>`, run as `UpstreamPerlSubstitution`s
compiled by `UpstreamRegex`; stdout; stderr; exit code; `<verify><file>`), and
`UpstreamFirstDifference` names the first differing byte and line. The result is an
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
