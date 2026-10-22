---
id: BL-1853
title: Close GF-0043: A telnet -T upload is sometimes never sent: the upload is cancelled when the peer closes first
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1853 — Close GF-0043: A telnet -T upload is sometimes never sent: the upload is cancelled when the peer closes first

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0043 (A telnet -T upload is sometimes never sent: the upload is cancelled when the peer closes first), so a later gap analysis measures each of `behaviour:test1327` as `match`.

## Context

- Finding: GF-0043, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1327`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured: test1327 expected 'upstream test1327 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET /we/want/1327 HTTP/1.0\r\n", got the end'. The command is telnet://127.0.0.1:8990 -T %LOGDIR/1327.txt with an empty <reply>. Rerun alone on 6383c570, the case passed, so the failure depends on timing under the run's parallel load; reproducing it may take several runs. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 1327

Suggestion, copied from the finding:

In Curl.Protocol.Telnet.UnitLibrary's TelnetProtocolHandler, the -T upload runs as its own task (SendUploadAsync). When ReceiveUntilClosedAsync returns on a read of 0, the finally block cancels that task, even before it has written the file. Order the upload's write with the receive loop: send what the upload file has ready before taking the peer's close as the end, as curl's telnet loop does. Pin it with a unit test whose fake connection closes at once. If Curl.Conformance.UnitLibrary's SwsHttpServerConnector closes an empty-reply connection before reading anything (the real sws reads the request first), fix that too.

## Acceptance criteria

- [x] `behaviour:test1327`: Curl answers what curl 8.21.0 answers, `upstream test1327 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- Cause: the sws emulation, not the telnet handler. `SwsHttpServerConnection` answered a
  read made before the client had written anything with 0 (closed). Curl's telnet session
  reads at once while its `-T` upload task is still reading the file, so under load the read
  won, the handler took the close as the end and cancelled the upload, and nothing was
  recorded. Real sws blocks reading the request before it answers or closes. curl 8.21.0's
  own telnet loop also ends at the peer's close (FD_CLOSE clears `keepon`), so the handler
  already matches curl and was left unchanged.
- Fix: a read before the connection's first write now waits for that write (or
  cancellation), then reads as before. Pinned by
  `SwsHttpServerConnectorTests.ReadAsync_BeforeTheFirstWrite_*`; 1327 added to
  `PassingUpstreamCases.txt` and passes in the ratchet (Conformance: 1131 passed, 0 failed).
- Touches: added Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests (the emulation
  the finding's suggestion names, and the ratchet list). No task in Doing on
  origin/work/dark-factory named either (BL-1851: Core; BL-1852: Console).
- Measure-CodeQuality.ps1 not run: the one new branch is covered both ways by the two new tests.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. sws emulation waits for the first write; test1327 passes
