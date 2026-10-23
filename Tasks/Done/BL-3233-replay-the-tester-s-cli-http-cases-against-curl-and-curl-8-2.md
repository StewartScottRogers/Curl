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
completed: 2026-10-23
---
# BL-3233 — Replay the tester's CLI HTTP cases against Curl and curl 8.22.0 and report each difference

## Goal

Running the differential runner on demand replays the tester's CLI HTTP cases (`cases.py` and the HTTP part of `cases_more.py`, about 600 cases) against Curl and a reference curl 8.22.0, on a loopback host serving BL-3232's routes, and writes a report in the tester's `### DIFF` shape; each difference it finds is filed as a task.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Port the cases to a JSON case file (data only, no Python): name, command line with `{U}`, stdin, files, timeout, skip_wire, cmp_err. Host the real-process runner and the TCP listener where BL-3231's ADR says (suggested: `Curl.Conformance.Differential.IntegrationTests`, skipped unless the reference binary is named). Compare the first run's report with the tester's `report.txt` and `report_more.txt` to show the port is faithful. Note: BL-3217's criterion says "CLI HTTP cases from scen.json"; scen.json in fact holds the Libcurl URL and printf scenarios (BL-3236) and the CLI cases are in cases*.py.

## Acceptance criteria

- [x] The CLI HTTP case file exists and holds every case from cases.py and the HTTP cases of cases_more.py.
- [x] One run against Curl and curl 8.22.0 writes a ### DIFF report; its run and the report's first lines are recorded under Notes, and each difference not in the named exclusions is filed as a task or matches an existing one.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- 2026-10-23 lane-3 sizing pass (before any edit): the work is about 608 cases (380 `case(` lines in cases.py, 228 in cases_more.py, many inside Python loops), a host project that does not exist yet (ADR-0782 says BL-3232 adds `Curl.Conformance.Differential.IntegrationTests`, but it did not), and 23 cases with byte content the case-file reader cannot express yet (ADR-0782 Consequences). Too big for one run, so split: BL-3259 (byte content and embedded case files in the reader), BL-3260 (the IntegrationTests host: loopback responder, control port, two-binary process runner, report), BL-3261 (port cases.py), BL-3262 (port the HTTP cases of cases_more.py). What is left here once they are Done: one run against Curl and the reference, compare with report.txt and report_more.txt, record the report's first lines, file each difference. Checked: the reference binary `C:Program FilesGitCrt64binrl.exe` is present and reports curl 8.22.0 (x86_64-w64-mingw32) Schannel; the tester's report.txt and report_more.txt each hold 36 `### DIFF` entries.
- 2026-10-23 lane-1 run. The case files are CliHttpCases.json (cases.py) and CliHttpMoreCases.json (the HTTP cases of cases_more.py), ported by BL-3261 and BL-3262 (Done), 885 entries between them. This run adds `Curl.Conformance.Differential.IntegrationTests/Cases/CliHttp/CliHttpDifferentialTests.cs`, which replays both files through `DifferentialCaseRunner` and writes the report to `CURL_DIFFERENTIAL_REPORT` (else a temp directory). The test is Integration and inconclusive unless `CURL_DIFFERENTIAL_REFERENCE` is set, so CI on Linux and macOS never runs it. The fast tests are green here (Curl.Conformance.Differential.UnitTests: 229 passed) and the change is platform-neutral, so the CI criterion is met as the lane rules allow.
- The run: `CURL_DIFFERENTIAL_REFERENCE='C:\Program Files\Git\ucrt64\bin\curl.exe' dotnet test Curl.Conformance.Differential.IntegrationTests --filter FullyQualifiedName~CliHttpDifferentialTests`, 12 m 34 s, passed. The reference is in Git's `ucrt64\bin`, not `mingw64`. The report's first lines: `### DIFF basic-u-noPass-stdin   args=-s -u u {U}/needauth` / `exit: ours=0 real=TIMEOUT` / `stdout:` / `  ours=b'denied\n'` / `  real=b''`. In all, 49 `### DIFF` and 2 `### SLOW` (auth-all-challenges-basic, auth-netrc-multiline).
- Runner fix found on the way: the control responder answers one request per connection and closes first, so its side sits in TIME_WAIT. Windows handed those ephemeral ports to new control requests, which were reset (twice, at different cases). The control client now sends `Connection: close` and retries a reset up to 5 times (`DifferentialCaseRunner.FetchControlAsync`), naming the case when it gives up. Having the server wait for the client to hang up first made HttpClient time out, so that was reverted.
- Compared with the tester's report.txt and report_more.txt (36 distinct names): 19 of ours carry their names. 17 of theirs no longer differ, mostly the url-* parsing cases plus auth-empty-user, redir-loc-multi-url, srv-gziptrail-v and srv-te-trailers, all fixed since. 30 of ours are not in theirs, nearly all timing or reference-build noise.
- Named exclusions: the four non-ASCII argument cases (post-unicode-arg, post-unicode-url, post-urlencode-unicode, url-http___127_0_0_1_18080_é7) differed only because the reference reads ANSI arguments, so they are now tagged `AnsiArguments` in CliHttpMoreCases.json (ADR-0782). help, short-h, help-all and help-category differ only by Curl's deliberate `--ai-help` line and httpsig options (ADR-0485), so they get no task.
- Filed for the rest:
  - BL-3274: normaliser noise and the reference build's other faults (timings, ports, WebSocket key, cookie expiry, trace warnings, -Z order, --dns-servers/--ech/--test-event, the console password prompt, PSL lines, --version libraries).
  - BL-3275: status-100, where -si shows no bare 100 headers.
  - BL-3278: the missing "Unsolicited Transfer-Encoding" verbose line.
  - BL-3276: SHA-256 Digest on Windows, where real curl exits 94.
  - BL-3277: proxy "reset" vs "aborted" text, --curves nope on Schannel, the --local-port range, and auth-all-challenges-v.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 69dc0dc5 e4778720 ae951636 5e81fa96.
- 2026-10-23: Doing -> Backlog. Split for size: waits on BL-3259, BL-3260, BL-3261 and BL-3262 (case-file bytes, runner host, the two case ports)
- 2026-10-23: Backlog -> Doing. Criteria at claim: 69dc0dc5 e4778720 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The CLI HTTP differential run replays both case files against curl 8.22.0 and writes a ### DIFF report; its 49 differences are excluded or filed (BL-3274 to BL-3278)
