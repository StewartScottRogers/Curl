---
id: BL-2170
title: Close GF-0078: The progress meter on stderr ends its lines differently from the reference curl (an extra restarted line for a second URL, a final line feed after -T .)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2170 — Close GF-0078: The progress meter on stderr ends its lines differently from the reference curl (an extra restarted line for a second URL, a final line feed after -T .)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0078 (The progress meter on stderr ends its lines differently from the reference curl (an extra restarted line for a second URL, a final line feed after -T .)), so a later gap analysis measures each of `behaviour:test675`, `behaviour:test1498` as `match`.

## Context

- Finding: GF-0078, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Items: `behaviour:test675`, `behaviour:test1498`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both pass in process and differ only out of process (evidence 'was match; in-process and out-of-process differ', actual 'stderr differs'). test675 (two URLs with different user:password, reply '200 OK swsclose'): expected 'reference curl exits 0; stdout 136 bytes: HTTP/1.1 200 OK swsclose ...'; the reference stderr for the second transfer holds an extra zero meter line ended by CR LF before its '100 26 100 26' lines, Curl's has none. test1498 (http://%HOSTIP:%HTTPPORT/bzz/1498 -T .): expected 'reference curl exits 0; stdout 0 bytes'; the reference stderr ends after its last meter update with no line feed, Curl's ends with CR LF. Recorded under C:/curl-crosscheck/recorded/675 and /1498 (reference/stderr.txt against curl/stderr.txt). Reproduce: powershell -NoProfile -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/behaviour-unchecked.json -TestsData $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data -Candidate <run>/tree/Curl.Console/bin/Release/net10.0/curl.exe -OutFile $env:TEMP/behaviour.json, then compare each case's reference/stderr.txt with curl/stderr.txt.

Suggestion, copied from the finding:

In Curl.Console (ProgressMeterLines and the meter's line endings), match the reference curl: for test675, end the meter line and start a new one when the transfer restarts (the second URL's request on the first connection, which the server closed, retried on a new one); for test1498, leave the final line feed off when the '-T .' upload ends as the reference does. Measure both several times on the reference with Record-CurlExchange.ps1 first, to separate a fixed rule from timing.

## Acceptance criteria

- [ ] `behaviour:test675`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 136 bytes: HTTP/1.1 200 OK swsclose\x0D\x0ADate: Thu, 09 Nov 2010 14:49:00 GMT\x0D\x0AContent-Type: text/html\x0D\x0AContent-Length: 26\x0D\x0A\x0D\x0Athe content would go here\x0A`, so the item measures `match`.
- [ ] `behaviour:test1498`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
