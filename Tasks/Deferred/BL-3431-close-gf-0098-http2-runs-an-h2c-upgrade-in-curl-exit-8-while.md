---
id: BL-3431
title: Close GF-0098: --http2 runs an h2c Upgrade in Curl (exit 8), while the Windows reference curl, built without nghttp2, refuses --http2 with exit 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.HttpVersionRows.cs, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3431 — Close GF-0098: --http2 runs an h2c Upgrade in Curl (exit 8), while the Windows reference curl, built without nghttp2, refuses --http2 with exit 2

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0098 (--http2 runs an h2c Upgrade in Curl (exit 8), while the Windows reference curl, built without nghttp2, refuses --http2 with exit 2), so a later gap analysis measures each of `behaviour:test1704` as `match`.

## Context

- Finding: GF-0098, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Platform: windows, measured against the Schannel curl of Git for Windows.
- Items: `behaviour:test1704`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1704 (features h2c; command 'http://%HOSTIP:%HTTPPORT/1704 --http2'; <verify><errorcode> 8) expected 'reference curl exits 2; stdout 0 bytes: ', actual 'exit code 8, reference 2', evidence 'was match; in-process and out-of-process differ'. The in-process harness passes the case: Curl sends upstream's exact Upgrade: h2c request and exits 8 'curl: (8) Version mismatch (from HTTP/1 to HTTP/2)' (BL-3270, ADR-0788). The reference curl 8.22.0 (x86_64-w64-mingw32, Schannel, no nghttp2) sends no request and exits 2: 'curl: option --http2: the installed libcurl version does not support this'. Recorded fixtures: C:/curl-crosscheck/recorded/1704/reference and .../curl. Reproduce from the repository root: powershell -NoProfile -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 with -Measurement the in-process behaviour measurement, -TestsData 'C:/Users/Stewart Rogers/AppData/Local/Curl/gap/upstream/8.22.0/tests/data' and -OutFile $env:TEMP/behaviour.json, then read item behaviour:test1704; or run the reference alone: curl.exe http://127.0.0.1:18990/1704 --http2 (exit 2, no connect).

Suggestion, copied from the finding:

No change to Curl's HTTP/2 Upgrade handling: Curl.Original.Protocol.Http.UnitLibrary already does what upstream's test1704 <verify> asks (exit 8, the h2c request bytes), and the gap comes only from the Windows reference build having no HTTP2/h2c feature. Recommend rejecting the finding: to match it literally, Curl.Cli.UnitLibrary (CommandLineParser.cs / CommandLineRefusal.cs) would have to refuse --http2 on Windows with exit 2 and 'option --http2: the installed libcurl version does not support this', which removes a feature and breaks the 'complete reimplementation is the default answer' rule in CLAUDE.md. The lasting fix is office work: the measurement should exclude a case the harness ran as reference-lacks:<feature> when its <features> names one the reference's curl -V lacks (h2c here). Today rule 4 of Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 checks this only for cases the harness skipped (Get-SkippedVerdict). Explained by ADR-0788: an HTTP/2 status line over HTTP/1 is refused with exit 8 on every platform, measured from an HTTP/2-built Windows curl.

## Acceptance criteria

- [ ] `behaviour:test1704`: Curl answers what curl 8.22.0 answers, `reference curl exits 2; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-23 (lane 4): To meet criterion 1, Curl would have to refuse `--http2` on Windows. Stewart decided
  the opposite himself in ADR-0871, and ADR-0808 kept it. Reversing his own decision is outside the
  delegation, so no code was changed. ADR-0809 records this and recommends the gap office's fix: rule 4 of
  ConvertTo-BehaviourMeasurement.ps1 should also exclude cases the harness ran whose `<features>` (h2c)
  the reference's `-V` lacks. Curl already answers upstream's `<verify>` (exit 8, ADR-0788).
  Remove the block once Stewart rejects GF-0098 (then close as won't-change) or reverses ADR-0871.

- 2026-10-24 (interactive): Stewart decided 'Keep HTTP/2 on' (2026-10-23, about 23:30 Arizona): ADR-0871 stands, so the refuse-on-Windows fix this task asks for is not wanted and no code changes. Done for it: AF-0381 closed by Stewart (closed-how: stewart, citing ADR-0871) on the audit branch, PR #221; on the gap branch (PR #217, commits 7c68dc274 and ea55d6f6b) Gap/Baselines/platform-exclusions.json has Measure-GapAreas.ps1 measure options:--http2, --http2-prior-knowledge, --proxy-http2, --proxy-http2:no-form and behaviour:test1704 as excluded with reason decided:ADR-0871 on Windows only, and GF-0098 and GF-0099 log the decision; they close on the next gap run that measures those items excluded. Its criteria (test1704 measuring match) cannot be met without undoing ADR-0871, so it is deferred rather than ticked; GF-0098 closes on the exclusion instead.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: bbcffc50 1fd5de8c 28346828.
- 2026-10-23: Doing -> Blocked. Stewart: GF-0098 needs --http2 refused on Windows, reversing your ADR-0871; reject the finding (and fix the gap measurement's rule 4, ADR-0809) or reverse ADR-0871?
- 2026-10-24: Blocked -> Deferred. Stewart decided 'Keep HTTP/2 on' (2026-10-23): ADR-0871 stands, so refusing HTTP/2 on Windows is not wanted; no code change. Revisit only if Stewart reverses ADR-0871.
