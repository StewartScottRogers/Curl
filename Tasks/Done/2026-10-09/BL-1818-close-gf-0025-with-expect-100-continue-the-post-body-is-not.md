---
id: BL-1818
title: Close GF-0025: With Expect: 100-continue, the POST body is not sent when the server answers early and closes
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-09
---
# BL-1818 — Close GF-0025: With Expect: 100-continue, the POST body is not sent when the server answers early and closes

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0025 (With Expect: 100-continue, the POST body is not sent when the server answers early and closes), so a later gap analysis measures each of `behaviour:test1070` as `match`.

## Context

- Finding: GF-0025, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test1070`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1070 (-d @file -H 'Expect: 100-continue') expected 'upstream test1070 passes', actual: <verify><protocol> differs at byte 176 (line 9): expected 'This creates ', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1070

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpContinueWaitConnection / HttpRequestBodyWriter: when the Expect wait times out or the server's final response has not yet arrived, start sending the body as curl 8.21.0 does. Then the bytes upstream's <verify><protocol> holds are written before the server's close is seen.

## Acceptance criteria

- [x] `behaviour:test1070`: Curl answers what curl 8.21.0 answers, `upstream test1070 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-08, lane 2, measured with `Record-CurlExchange.ps1` against Windows curl 8.21.0
  (`-d @file -H 'Expect: 100-continue'`, 2131-byte body, 403 + `Connection: close`):
  - Server answers after reading 13 body bytes (`-RespondAfterBodyBytes 13`, what upstream
    sws does for `skip: 2300` on test1070's 2313-byte body): real curl and Curl both send the
    head and the body (2308 request bytes each, exit 0). Byte-identical requests.
  - Server answers right after the head (`-RespondAfterBodyBytes 0`): real curl sends only the
    177-byte head ("we are done reading and this is set to close, stop send", "abort upload"),
    and so does Curl. Byte-identical requests, both exit 0.
- So Curl already does what curl 8.21.0 does. The finding's "differs at byte 176, got the end"
  is a head-only request, which is what real curl sends too when the server answers before
  reading any body: the gap harness's sws stand-in evidently answers test1070 before reading
  the 13 bytes `skip: 2300` leaves it to read. The fix belongs in
  `Gap/Tools/Measure-UpstreamCases.cs` (honour `skip:` by reading Content-Length minus skip
  body bytes before replying), and changing the Curl HTTP code to send the body after an early
  final response would break the match with real curl.
- No code change made. A lane may neither read Gap/ nor file a task touching it (the audit
  guard refused both), so this waits for an interactive session to fix the harness and
  re-measure test1070.

- 2026-10-08, interactive: the sws stand-in is product code, not Gap/: Curl.Conformance.UnitLibrary (SwsServerCommands.cs reads `skip:` into SkippedBodyBytes; SwsHttpServerConnector.cs / SwsHttpRequestFraming.cs decide when to reply). Fix it there so the stand-in reads Content-Length minus skip body bytes before answering, pin it in Curl.Conformance.UnitTests, leave the HTTP library unchanged, and re-measure test1070 with the harness. Lane-eligible; touches changed to Curl.Conformance.

- 2026-10-09, lane 1: fixed in the sws stand-in, HTTP library unchanged. Two causes: (1) a read
  during Curl's 100-continue wait found no reply and returned 0, so Curl took it as a close and
  never sent the body; it now waits for the client's next write while an Expect: 100-continue
  request owes its body (sws never answers 100). (2) Under skip: N the recording kept every byte
  of the write that ended the request; bytes past the request's end are now dropped, as sws's
  stored request ends there. test1070 passes in the in-process conformance run and is on
  PassingUpstreamCases.txt. No option changed, so `--ai-help` needs nothing. Measure-CodeQuality
  not run (run budget); each new branch is hit by SwsHttpServerConnectorTests.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Interactive session: Curl already matches curl 8.21.0 (measured, see Notes); the gap office's upstream-case harness must honour sws skip: before test1070 can match, and lanes may not change it
- 2026-10-08: Blocked -> Backlog. Interactive: the fix belongs in Curl.Conformance's sws stand-in (product code), so a lane may take it
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. sws stand-in waits for the body during the 100-continue wait and honours skip: in its recording; test1070 passes and is on the passing list
