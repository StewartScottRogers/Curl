---
id: BL-3260
title: Host the differential runner: loopback responder, control port and two-binary process runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.IntegrationTests/Host, Curl.Conformance.Differential.IntegrationTests/Curl.Conformance.Differential.IntegrationTests.csproj, Curl.slnx]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3260 — Host the differential runner: loopback responder, control port and two-binary process runner

## Goal

`Curl.Conformance.Differential.IntegrationTests` exists and hosts the differential runner: it serves `DifferentialHttpResponder`'s routes on a loopback port with its control port beside it (`/reset`, `/log`), runs each case through Curl and the reference curl in its own working directory, and writes the `### DIFF` report with `DifferentialReportWriter`.

## Context

ADR-0782 ("Host"): the tests are `Integration` and return Inconclusive unless `CURL_DIFFERENTIAL_REFERENCE` names the reference curl 8.22.0 binary (on Stewart's machine `C:\Program Files\Git\ucrt64\bin\curl.exe`). The tester's `runner.py` (in `Z:\repos\Curl.difftest\2026-10-22\`, reference only, never run Python) is the model: port 18080 with control on 18081; environment without any `*proxy*` variable and with `HOME`, `USERPROFILE`, `CURL_HOME`, `APPDATA` pointed at an empty temporary home; `{U}`, `{CWD}` substitution in arguments; stdin; the case's files written first; a timeout reported as exit `TIMEOUT`; files the run wrote or changed collected afterwards; the wire log fetched from the control port after each run; cases run serially (the server's state and log are shared). Use the new-project skill for the project and its entry in `Curl.slnx`. The Curl binary under test: the built `Curl.Console` output, or a path in an environment variable (decide and record). Keep the host in `Host/` so case-family folders (`CliHttp/`, `CliProtocols/`, `CliTls/`) can add their own test classes beside it. Split out of BL-3233.

## Acceptance criteria

- [x] `Curl.Conformance.Differential.IntegrationTests` exists, is in `Curl.slnx`, every test carries `[TestCategory("Integration")]`, and without `CURL_DIFFERENTIAL_REFERENCE` its tests are Inconclusive.
- [x] With `CURL_DIFFERENTIAL_REFERENCE` set, a test runs a small inline case list (e.g. `-s {U}/echo`, `-v {U}/echo`) through both binaries and writes a `### DIFF` report; its first lines are recorded under Notes.
- [x] Build clean with -warnaserror and fast tests green.

## Notes

- Filed by BL-3233's lane-3 run on 2026-10-23 when sizing it.
- Host in `Host/`: `DifferentialLoopbackServer` (routes on 18080, `DifferentialControlResponder` on 18081), `DifferentialProcessRunner` (one case, one binary, runner.py's rules), `DifferentialCaseRunner` (serial A/B runs, `/reset` before and `/log` after each, normalise and compare), `DifferentialBinaries`, `DifferentialHostTests`. Decisions in ADR-0785: Curl under test is the `Curl.Console` app host built into the test output (project reference), or `CURL_DIFFERENTIAL_OURS`; fixed ports as runner.py; report written to `report.txt` in the kept temporary root.
- Without the variable: `dotnet test --filter TestCategory=Integration` shows the test Skipped (Inconclusive).
- With `CURL_DIFFERENTIAL_REFERENCE=C:\Program Files\Git\ucrt64\bin\curl.exe` (passed, 4.9 s), the report's first lines:
  `### SLOW echo-silent: ours 3.8s real 0.1s` (first-run .NET start-up)
  `### DIFF echo-verbose   args=-v {U}/echo`
  `stderr:` with ours and real equal except the progress lines' speed columns (ours `11`, real `173`), filed as BL-3263.
- No production library changed (the IntegrationTests project carries no coverage gate), so Measure-CodeQuality.ps1 had nothing to measure. The tests run only with the reference set, which CI never sets, so nothing is platform-dependent in CI.
- Fast tests: `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 0f1c33be 01c61342 566e31a4.
- 2026-10-23: Doing -> Done. Differential IntegrationTests host runs inline cases through Curl and reference curl against the loopback responder and writes the DIFF report
