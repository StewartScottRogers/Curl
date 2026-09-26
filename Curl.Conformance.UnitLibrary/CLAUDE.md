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
