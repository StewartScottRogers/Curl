---
id: GF-0098
title: --http2 runs an h2c Upgrade in Curl (exit 8), while the Windows reference curl, built without nghttp2, refuses --http2 with exit 2
area: behaviour
platform: windows
key: behaviour:reference-curl-built-without-http2
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-23_1637
closed: 2026-10-25_1542
regression: false
items: [behaviour:test1704]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-3431
tasks: [BL-3431]
---
# GF-0098 - --http2 runs an h2c Upgrade in Curl (exit 8), while the Windows reference curl, built without nghttp2, refuses --http2 with exit 2

## Summary

Curl differs from upstream curl in behaviour: --http2 runs an h2c Upgrade in Curl (exit 8), while the Windows reference curl, built without nghttp2, refuses --http2 with exit 2.

## Evidence

behaviour:test1704 (features h2c; command 'http://%HOSTIP:%HTTPPORT/1704 --http2'; <verify><errorcode> 8) expected 'reference curl exits 2; stdout 0 bytes: ', actual 'exit code 8, reference 2', evidence 'was match; in-process and out-of-process differ'. The in-process harness passes the case: Curl sends upstream's exact Upgrade: h2c request and exits 8 'curl: (8) Version mismatch (from HTTP/1 to HTTP/2)' (BL-3270, ADR-0788). The reference curl 8.22.0 (x86_64-w64-mingw32, Schannel, no nghttp2) sends no request and exits 2: 'curl: option --http2: the installed libcurl version does not support this'. Recorded fixtures: C:/curl-crosscheck/recorded/1704/reference and .../curl. Reproduce from the repository root: powershell -NoProfile -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 with -Measurement the in-process behaviour measurement, -TestsData 'C:/Users/Stewart Rogers/AppData/Local/Curl/gap/upstream/8.22.0/tests/data' and -OutFile $env:TEMP/behaviour.json, then read item behaviour:test1704; or run the reference alone: curl.exe http://127.0.0.1:18990/1704 --http2 (exit 2, no connect).

## Suggestion

No change to Curl's HTTP/2 Upgrade handling: Curl.Protocol.Http.UnitLibrary already does what upstream's test1704 <verify> asks (exit 8, the h2c request bytes), and the gap comes only from the Windows reference build having no HTTP2/h2c feature. Recommend rejecting the finding: to match it literally, Curl.Cli.UnitLibrary (CommandLineParser.cs / CommandLineRefusal.cs) would have to refuse --http2 on Windows with exit 2 and 'option --http2: the installed libcurl version does not support this', which removes a feature and breaks the 'complete reimplementation is the default answer' rule in CLAUDE.md. The lasting fix is office work: the measurement should exclude a case the harness ran as reference-lacks:<feature> when its <features> names one the reference's curl -V lacks (h2c here). Today rule 4 of Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 checks this only for cases the harness skipped (Get-SkippedVerdict). Explained by ADR-0788: an HTTP/2 status line over HTTP/1 is refused with exit 8 on every platform, measured from an HTTP/2-built Windows curl.

## Measurements

- 2026-10-23_1637: 1 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-23_1637: Opened by gap-behaviour.
- 2026-10-23_1729: Filed BL-3431.
- 2026-10-23_2330: Stewart decided 'Keep HTTP/2 on': ADR-0786 stands, so the Windows build keeps HTTP/2 and Curl will not refuse it to match the Schannel reference. Gap/Baselines/platform-exclusions.json now has Measure-GapAreas.ps1 measure behaviour:test1704 as excluded with reason decided:ADR-0786 on Windows only; this finding closes on the next run that measures them so (findings close only on measurement).
- 2026-10-25_1542: Closed: run 2026-10-25_1542 measured every item as match or excluded on windows.
