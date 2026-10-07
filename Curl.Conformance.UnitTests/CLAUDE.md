# Curl.Conformance.UnitTests

The tests of `Curl.Conformance.UnitLibrary`. It follows ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).

Today it holds `HarnessReferencesTests`, which pins that the harness library and
`curl` (`Curl.Console`, the runner each case will go through) are both copied beside
the tests, and `UpstreamTestCaseParserTests`, which drive the test-file parser from
inline test-file text, and `UpstreamTestFileExpanderTests`, which pin variable
substitution, `%if` evaluation and the inline instructions against upstream's `prepro`, and
`UpstreamTestDataTests`, which pin that the vendored `UpstreamTestData/` (curl 8.21.0's
`tests/data/test*` files and `COPYING`; see its `README.md`) is copied beside the tests,
and `SwsHttpServerConnectorTests`, which pin the in-memory `sws` emulation's request framing,
reply-part selection, connection closing, the `<servercmd>` commands that change framing
(`auth_required`, `no-expect`, `skip`, `upgrade`), the timing and stream commands (`idle`,
`stream`, `writedelay`, `connection-monitor`, `<postcmd>` `wait`) driven on
`ManualTimeProvider`, a hand-written clock that moves only when a test advances it,
`<servercmd>` reporting and byte recording against upstream's `sws.c`. The harness's other
tests (`UpstreamCaseRunnerTests`, `UpstreamCaseScreeningTests`, `UpstreamCaseVerificationTests`,
`UpstreamCaseRatchetTests`, `UpstreamCommandLineSplitterTests`, ...) drive it from inline
test-file text.

`UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet` is the one
data-driven method, in `TestCategory("Conformance")` and part of the fast suite, that runs
every vendored case through curl in process (`CurlComposition.CreateRunner`, reached through
`InternalsVisibleTo` on `Curl.Console`), one row per case named `test<N>`. It is the ratchet
of ADR-0013 decision 5: a case on `PassingUpstreamCases.txt` must pass, and fails with its
first difference (or skip reason) if it stops; any other case is `Inconclusive` with its skip
reason, its first difference, or a note that it passes and can be listed. When a change makes
a case pass, add its number to the list in the same commit.

## Pass rate

Recompute it from a run's results: listed cases over runnable cases (passing plus failing;
skipped cases are not runnable).

| Date | Listed (passing) | Failing | Skipped | Runnable | Pass rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| 2026-09-26 | 137 | 324 | 1552 | 461 | 29.7% |
| 2026-09-27 | 206 | 271 | 1536 | 477 | 43.2% |
| 2026-10-04 | 392 | 82 | 1539 | 474 | 82.7% |
| 2026-10-07 | 559 | 162 | 1292 | 721 | 77.5% |

The whole conformance run of 2013 cases takes about 8 seconds on the development machine and opens no socket: every
connection goes to the in-memory `sws` emulation, and UDP to `UnreachableDatagramConnector`.

No sockets and no network, like every other `.UnitTests` project. Unlike the rule in
`.claude/rules/testing.md`, the runner's tests and the conformance method touch the file
system without `TestCategory("Integration")`: a case's `%LOGDIR` files and curl's `--output`
are real files, as upstream's are, and ADR-0013 puts the ratchet in the fast suite. Every file
stays under the test output's `log/` folder, and a case naming a file outside its own log
directory is skipped.
