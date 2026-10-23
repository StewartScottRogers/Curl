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
completed:
---
# BL-3260 — Host the differential runner: loopback responder, control port and two-binary process runner

## Goal

`Curl.Conformance.Differential.IntegrationTests` exists and hosts the differential runner: it serves `DifferentialHttpResponder`'s routes on a loopback port with its control port beside it (`/reset`, `/log`), runs each case through Curl and the reference curl in its own working directory, and writes the `### DIFF` report with `DifferentialReportWriter`.

## Context

ADR-0782 ("Host"): the tests are `Integration` and return Inconclusive unless `CURL_DIFFERENTIAL_REFERENCE` names the reference curl 8.22.0 binary (on Stewart's machine `C:\Program Files\Git\ucrt64\bin\curl.exe`). The tester's `runner.py` (in `Z:\repos\Curl.difftest\2026-10-22\`, reference only, never run Python) is the model: port 18080 with control on 18081; environment without any `*proxy*` variable and with `HOME`, `USERPROFILE`, `CURL_HOME`, `APPDATA` pointed at an empty temporary home; `{U}`, `{CWD}` substitution in arguments; stdin; the case's files written first; a timeout reported as exit `TIMEOUT`; files the run wrote or changed collected afterwards; the wire log fetched from the control port after each run; cases run serially (the server's state and log are shared). Use the new-project skill for the project and its entry in `Curl.slnx`. The Curl binary under test: the built `Curl.Console` output, or a path in an environment variable (decide and record). Keep the host in `Host/` so case-family folders (`CliHttp/`, `CliProtocols/`, `CliTls/`) can add their own test classes beside it. Split out of BL-3233.

## Acceptance criteria

- [ ] `Curl.Conformance.Differential.IntegrationTests` exists, is in `Curl.slnx`, every test carries `[TestCategory("Integration")]`, and without `CURL_DIFFERENTIAL_REFERENCE` its tests are Inconclusive.
- [ ] With `CURL_DIFFERENTIAL_REFERENCE` set, a test runs a small inline case list (e.g. `-s {U}/echo`, `-v {U}/echo`) through both binaries and writes a `### DIFF` report; its first lines are recorded under Notes.
- [ ] Build clean with -warnaserror and fast tests green.

## Notes

- Filed by BL-3233's lane-3 run on 2026-10-23 when sizing it.

## Log

- 2026-10-23: Created.
