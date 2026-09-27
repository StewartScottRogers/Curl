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
`SwsServerCommands` reads `<servercmd>`; every other command sws knows is listed by name in
`UnsupportedServerCommands` so the case can be skipped with a reason (BL-263, BL-264), and
sws's part-number rules for authentication, `swsbounce` and `CONNECT` are not emulated yet
(BL-265). A read with no reply waiting returns 0, because in memory nothing else can arrive.

`UpstreamCaseRunner.RunAsync` runs one case end to end (ADR-0013, decision 4): it expands
the file for an `UpstreamCurlPlatform` (the features Curl reports and its null device),
asks `UpstreamCaseScreening` whether the harness can run it (a `<tool>` case, a server other
than `http`, `file` or `none`, a missing feature, a variable with no value, an unsupported
`<servercmd>` or strip line each skip it with a reason, and so does a file part naming a file
outside the case's log directory), writes `<client><file>` parts into
the case's log directory, splits `<client><command>` with `UpstreamCommandLineSplitter` as
the shell `runtests.pl` uses would, and runs curl through an `UpstreamCurlInvocation` against
the `sws` emulation and `UnreachableDatagramConnector`, under a time limit from an injected
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
