---
id: BL-174
title: Enforce MaxTime and ConnectTimeout and map HTTP send and receive failures
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-123]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-174 — Enforce MaxTime and ConnectTimeout and map HTTP send and receive failures

## Goal

The HTTP handler ends a transfer at `MaxTime` with curl's exit 28 message and maps send and receive failures to 55 and 56.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ITransferContext.ConnectTimeout` and `MaxTime` exist (BL-123, ADR-0008) and are filled from the command line (BL-124).
- curl's timeout message: `Operation timed out after N milliseconds with M bytes received` (measure exact wording and the out-of form when the size is known).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] On `FakeTimeProvider`, a stalled response returns `CurlExitCode.OperationTimedOut` (28) with the measured message and correct N and M.
- [ ] A send failure returns `SendError` (55) and a receive failure `RecvError` (56), each with the measured message.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
