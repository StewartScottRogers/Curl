# Curl.Conformance.UnitTests

The tests of `Curl.Conformance.UnitLibrary`. It follows ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).

Today it holds `HarnessReferencesTests`, which pins that the harness library and
`curl` (`Curl.Console`, the runner each case will go through) are both copied beside
the tests, and `UpstreamTestCaseParserTests`, which drive the test-file parser from
inline test-file text, and `UpstreamTestFileExpanderTests`, which pin variable
substitution, `%if` evaluation and the inline instructions against upstream's `prepro`, and
`UpstreamTestDataTests`, which pin that the vendored `UpstreamTestData/` (curl 8.22.0's
`tests/data/test*` files and `COPYING`; see its `README.md`) is copied beside the tests,
and `SwsHttpServerConnectorTests`, which pin the in-memory `sws` emulation's request framing,
reply-part selection, connection closing, the `<servercmd>` commands that change framing
(`auth_required`, `no-expect`, `skip`, `upgrade`), the timing and stream commands (`idle`,
`stream`, `writedelay`, `connection-monitor`, `<postcmd>` `wait`) driven on
`ManualTimeProvider`, a hand-written clock that moves only when a test advances it,
`<servercmd>` reporting and byte recording against upstream's `sws.c`. The harness's other
tests (`UpstreamCaseRunnerTests`, `UpstreamCaseScreeningTests`, `UpstreamCaseVerificationTests`,
`UpstreamCaseRatchetTests`, `UpstreamCommandLineSplitterTests`, ...) drive it from inline
test-file text. The adversarial black-box tests of `Documentation/Wiki/Adversarial-Testing.md`
(BL-1494) sit in `<Class>Tests.Adversarial.cs` files, as the second half of a `partial` test
class, so each production class still has one test class.

`UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet` is the one
data-driven method, in `TestCategory("Conformance")` and part of the fast suite, that runs
every vendored case through curl in process (`CurlComposition.CreateRunner`, reached through
`InternalsVisibleTo` on `Curl.Console`), one row per case named by `UpstreamCaseDisplayName` (`test798 [http]: HTTP cookies in a folded header`) and carrying one test category per server it names, so `dotnet test --filter "TestCategory=ftp"` runs only the FTP cases. It is the ratchet
of ADR-0013 decision 5: a case on `PassingUpstreamCases.txt` must pass, and fails with its
first difference (or skip reason) if it stops; any other case is `Inconclusive` with its skip
reason, its first difference, or a note that it passes and can be listed. A listed case that
fails is run once more before it is judged, since a stall of a busy CI runner can hold any case
past a time limit - the harness's (curl past 20 seconds, or the whole case past 30; BL-1859) or
the case's own, as test303's `-m 8` (BL-2093). When a change makes
a case pass, add its number to the list in the same commit.

## Debugging one case

`NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` (`TestCategory("Manual")`, BL-2155)
runs the one case `CURL_UPSTREAM_CASE` names, whether or not it is on `PassingUpstreamCases.txt`,
and fails unless it passes, with the ratchet's message for it (first difference and the
differing lines, or the skip reason). Without the variable `RunsOnlyWhenAnUpstreamCaseIsNamed`
reports it skipped, so the fast command runs nothing extra. In Test Explorer, set the variable
in a `.runsettings` file's `EnvironmentVariables` (or the shell Visual Studio starts from) and run
the test by name.

With `CURL_UPSTREAM_KEEP_LOGS=1`, every upstream case this class runs keeps its log folder
(`bin/<configuration>/net10.0/log/test<N>-<guid>`: the case's files, curl's output, `trace<N>`)
instead of deleting it, and prints `Kept the case's log folder: <path>` to the test's output
(`--logger "console;verbosity=detailed"` shows it on the command line).

PowerShell:

```powershell
$env:CURL_UPSTREAM_CASE = '798'; $env:CURL_UPSTREAM_KEEP_LOGS = '1'
dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual" --logger "console;verbosity=detailed"
Remove-Item Env:CURL_UPSTREAM_CASE, Env:CURL_UPSTREAM_KEEP_LOGS
```

`cmd`:

```cmd
set CURL_UPSTREAM_CASE=798& set CURL_UPSTREAM_KEEP_LOGS=1
dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual" --logger "console;verbosity=detailed"
```

To keep the logs of the ratchet rows instead, set only `CURL_UPSTREAM_KEEP_LOGS=1` and filter by
row name, for example `--filter "Name~test798 "`.

## Pass rate

Recompute it from a run's results: listed cases over runnable cases (passing plus failing;
skipped cases are not runnable).

| Date | Listed (passing) | Failing | Skipped | Runnable | Pass rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| 2026-09-26 | 137 | 324 | 1552 | 461 | 29.7% |
| 2026-09-27 | 206 | 271 | 1536 | 477 | 43.2% |
| 2026-10-04 | 392 | 82 | 1539 | 474 | 82.7% |
| 2026-10-07 | 559 | 162 | 1292 | 721 | 77.5% |

The whole conformance run of 2072 cases takes about 8 seconds on the development machine and opens no socket: every
connection goes to the in-memory `sws` emulation, and UDP to the in-memory `tftpd` emulation, `TftpServerConnector`.

No sockets and no network, like every other `.UnitTests` project. Unlike the rule in
`.claude/rules/testing.md`, the runner's tests and the conformance method touch the file
system without `TestCategory("Integration")`: a case's `%LOGDIR` files and curl's `--output`
are real files, as upstream's are, and ADR-0013 puts the ratchet in the fast suite. Every file
stays under the test output's `log/` folder, and a case naming a file outside its own log
directory is skipped.
