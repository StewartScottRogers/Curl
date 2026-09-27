# Curl.Conformance.UnitLibrary

The harness that runs curl's own upstream test cases (`tests/data/test*`) against
Curl, in process, as data-driven MSTest cases. It follows ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).

Today it holds the test-file parser and the test-file expander. `UpstreamTestCaseParser.Parse` reads one test file's
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
`UpstreamTestInstructions` replaces `%SP`-style character macros and `%b64[]b64%`,
`%hex[]hex%` and `%repeat[]%`. The resulting `UpstreamTestFileExpansion` lists upstream
variables with no value and instructions it does not carry out (`%days`, `%include`, ...),
left as written so the case can be skipped with a reason; `Parse()` hands it to the parser.
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
the last, timed on the `TimeProvider` given to the connector; `idle` answers nothing, and a read
then waits until cancelled; `stream` answers with `a string to stream 01234567890\n` without end
and reads nothing more; `connection-monitor` records `[DISCONNECT]\n` in `ReceivedBytes`
(`SwsServerRecording`, one flag for the server as in sws) when a connection that carried a
request closes; after an `upgrade` reply the connection records raw traffic until the client
has been quiet for one second, then closes. `delay: N` alone is listed in
`UnsupportedServerCommands`, so a case using it can be skipped with a reason: sws applies it
only when it accepts a connection while another's request is part-read, which needs its
single-threaded interleaving of connections, not modelled here, and no case at `curl-8_21_0`
uses it. sws's part-number rules for authentication, `swsbounce` and `CONNECT` are not
emulated yet (BL-265). Otherwise a read with no reply waiting returns 0, because in memory
nothing else can arrive.

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
