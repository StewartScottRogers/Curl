---
id: BL-1984
title: Close GF-0054: When the EPSV data port cannot be connected, FTP does not fall back to PASV
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1984 — Close GF-0054: When the EPSV data port cannot be connected, FTP does not fall back to PASV

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0054 (When the EPSV data port cannot be connected, FTP does not fall back to PASV), so a later gap analysis measures each of `behaviour:test1233` as `match`.

## Context

- Finding: GF-0054, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1233`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1233 (EPSV answered '229 Entering Passive Mode (|||1|)') expected 'upstream test1233 passes' with EPSV, PASV, TYPE I, SIZE, RETR, QUIT, actual 'curl did not finish within 20 seconds'. Curl.Protocol.Ftp.UnitLibrary/FtpSession.OpenPassiveDataConnectionAsync falls back to PASV only when the EPSV reply is not 229, not when the connection to its port fails. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1233

Suggestion, copied from the finding:

In Curl.Protocol.Ftp.UnitLibrary's FtpSession.OpenPassiveDataConnectionAsync, when the data connection to the EPSV port fails, send PASV and use its port, as curl 8.21.0 does ('Failed EPSV attempt. Switching to PASV'). Pin it with a connector that refuses the EPSV port. If the 20-second hang comes from the harness's FTP connector never refusing port 1, fix that in Curl.Conformance.UnitLibrary as well.

## Acceptance criteria

- [x] `behaviour:test1233`: Curl answers what curl 8.21.0 answers, `upstream test1233 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- The FTP fallback itself already existed (BL-1250: `FtpSession.AnswerFailedEpsvDialAsync` sends PASV after a failed dial to the 229 port). The 20-second hang was the harness: `NoListenPortConnector` passed port 1 on to the in-memory sws stand-in, which accepts any port, so the EPSV dial succeeded and RETR waited for data that never came. Fix: `NoListenPortConnector` now refuses port 1 as well as %NOLISTENPORT (test1233 says "assuming there is nothing listening on port 1"). Chose port 1 alone over all ports below 1024 so no case that reaches the stand-in on another port changes. No FTP library change was needed. test1233 added to `PassingUpstreamCases.txt`; it passes in the ratchet.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Harness refuses port 1, so test1233's EPSV dial fails and Curl falls back to PASV; case listed in the ratchet
