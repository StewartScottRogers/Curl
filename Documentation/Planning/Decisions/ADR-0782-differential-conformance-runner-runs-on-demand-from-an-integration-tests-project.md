# ADR-0782 — The differential conformance runner runs on demand from an IntegrationTests project

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3231 (parent BL-3217)
- Decided by Claude under Stewart's delegation.

## Context

Stewart's tester found Curl's differences from curl 8.22.0 on 2026-10-22 by running each command
line through both binaries against loopback servers and diffing exit code, standard output,
standard error, the files written and the bytes on the wire, after normalising what changes from
run to run (`runner.py`, `norm()`, `report.txt`). That harness was Python and lived outside the
repository. Curl's own conformance harness (`Curl.Conformance.UnitLibrary`) runs upstream's
`tests/data` cases in process against fakes; the gap office's behaviour measurement (ADR-0433)
runs the same upstream cases. Neither runs a second, real curl binary, so neither catches a
difference no upstream case covers.

The reference binary the tester used, Git for Windows' ucrt64 curl 8.22.0, is a debug build with
faults of its own: it reads arguments as ANSI, has a 32-bit `time_t`, writes `sendrecv_dl()` and
`Curl_sendrecv()` trace lines, has no HTTP/2 and converts IDN host names wrongly
(`lib_summary.txt`).

## Decision

- **Core.** `Curl.Conformance.Differential.UnitLibrary` holds everything that needs no process,
  socket or file system: the case model and JSON case-file reader (`JsonDocument`, AOT-safe), the
  `shlex.split` argument splitter, the output normaliser, the comparer and the report writer in
  the tester's `### DIFF` / `### SLOW` shape, with outputs as Python `repr()` literals so a report
  diffs cleanly against the tester's. It is held to the usual gates by
  `Curl.Conformance.Differential.UnitTests`.
- **Host.** Starting the two binaries, the loopback servers and the per-case working directories
  live in a `Curl.Conformance.Differential.IntegrationTests` project (added by BL-3232). Its
  tests are `Integration` and return Inconclusive unless the environment variable
  `CURL_DIFFERENTIAL_REFERENCE` names the reference curl 8.22.0 binary, so a machine without one
  runs nothing.
- **When it runs.** On demand, not in CI: the reference build is a Windows debug build with the
  faults above, a download in every CI run would make CI depend on an outside host, and the
  differences it finds are filed as tasks rather than gated. CI keeps the fast tests of the core.
  A CI job can be added later as its own decision once the case files hold no known difference.
- **Named exclusions.** The reference build's faults are named, never silent:
  - `sendrecv_dl()` / `Curl_sendrecv()` lines and the debug warnings are removed by the
    normaliser, since they say nothing about Curl.
  - `ReferenceBuildLimitation.AnsiArguments`, `ThirtyTwoBitTimeT`, `NoHttp2` and `BrokenIdn` tag
    a case that would only show the reference's fault; such a case is not run against that build
    (`DifferentialCase.RunsAgainstReferenceBuild`).
- **Relation to ADR-0433.** The gap office measures upstream's own cases; this runner replays
  hand-written command lines against a real binary. They overlap only where a hand-written case
  repeats an upstream one, and neither feeds the other.

## Consequences

The tester's cases can be ported as JSON case files (BL-3233 to BL-3240) without Python. A case
file's `stdin` and `files` are UTF-8 text; a case that needs arbitrary bytes needs a new property.
`PythonLiteral.FormatString` escapes control characters and U+007F to U+00A0 and U+00AD as Python
does, but not every non-printable character above U+00FF; file names in cases are ASCII.
