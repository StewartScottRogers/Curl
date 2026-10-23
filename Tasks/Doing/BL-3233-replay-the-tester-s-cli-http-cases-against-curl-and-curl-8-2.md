---
id: BL-3233
title: Replay the tester's CLI HTTP cases against Curl and curl 8.22.0 and report each difference
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3231, BL-3232, BL-3259, BL-3260, BL-3261, BL-3262]
touches: [Curl.Conformance.Differential.IntegrationTests, Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp, Curl.Conformance.Differential.UnitTests/Cases/CliHttp, Curl.slnx]
requirement: none
created: 2026-10-22
completed:
---
# BL-3233 — Replay the tester's CLI HTTP cases against Curl and curl 8.22.0 and report each difference

## Goal

Running the differential runner on demand replays the tester's CLI HTTP cases (`cases.py` and the HTTP part of `cases_more.py`, about 600 cases) against Curl and a reference curl 8.22.0, on a loopback host serving BL-3232's routes, and writes a report in the tester's `### DIFF` shape; each difference it finds is filed as a task.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Port the cases to a JSON case file (data only, no Python): name, command line with `{U}`, stdin, files, timeout, skip_wire, cmp_err. Host the real-process runner and the TCP listener where BL-3231's ADR says (suggested: `Curl.Conformance.Differential.IntegrationTests`, skipped unless the reference binary is named). Compare the first run's report with the tester's `report.txt` and `report_more.txt` to show the port is faithful. Note: BL-3217's criterion says "CLI HTTP cases from scen.json"; scen.json in fact holds the Libcurl URL and printf scenarios (BL-3236) and the CLI cases are in cases*.py.

## Acceptance criteria

- [ ] The CLI HTTP case file exists and holds every case from cases.py and the HTTP cases of cases_more.py.
- [ ] One run against Curl and curl 8.22.0 writes a ### DIFF report; its run and the report's first lines are recorded under Notes, and each difference not in the named exclusions is filed as a task or matches an existing one.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- 2026-10-23 lane-3 sizing pass (before any edit): the work is about 608 cases (380 `case(` lines in cases.py, 228 in cases_more.py, many inside Python loops), a host project that does not exist yet (ADR-0782 says BL-3232 adds `Curl.Conformance.Differential.IntegrationTests`, but it did not), and 23 cases with byte content the case-file reader cannot express yet (ADR-0782 Consequences). Too big for one run, so split: BL-3259 (byte content and embedded case files in the reader), BL-3260 (the IntegrationTests host: loopback responder, control port, two-binary process runner, report), BL-3261 (port cases.py), BL-3262 (port the HTTP cases of cases_more.py). What is left here once they are Done: one run against Curl and the reference, compare with report.txt and report_more.txt, record the report's first lines, file each difference. Checked: the reference binary `C:Program FilesGitCrt64binrl.exe` is present and reports curl 8.22.0 (x86_64-w64-mingw32) Schannel; the tester's report.txt and report_more.txt each hold 36 `### DIFF` entries.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 69dc0dc5 e4778720 ae951636 5e81fa96.
- 2026-10-23: Doing -> Backlog. Split for size: waits on BL-3259, BL-3260, BL-3261 and BL-3262 (case-file bytes, runner host, the two case ports)
- 2026-10-23: Backlog -> Doing. Criteria at claim: 69dc0dc5 e4778720 ae951636 5e81fa96.
