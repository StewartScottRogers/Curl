---
id: BL-1349
title: Cancel a POP3 SASL exchange whose mechanism gives a CancelReason, writing it as a -v line first
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1336, BL-1342]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1349 — Cancel a POP3 SASL exchange whose mechanism gives a CancelReason, writing it as a -v line first

## Goal

When `ISaslExchange.RespondAsync` returns `null` with `ISaslExchange.CancelReason` set (BL-1335, BL-1336), the POP3 handler reports the reason as a `-v` info line and then cancels the exchange with `*` exactly as it does for a challenge that is not base64, instead of failing with exit 67.

## Context

- Today `Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs` `AnswerToAsync` (near line 350) returns `null` for a `null` answer, which the loop near line 268 turns into `LoginDenied()` (exit 67); an undecodable challenge returns `CancelLine`, which the loop answers with `CancelAsync()` (line 309) and goes on to the next mechanism (BL-1222).
- curl 8.21.0 (tag `curl-8_21_0`), `lib/curl_sasl.c` lines 789-793: a mechanism step returning `CURLE_BAD_CONTENT_ENCODING` calls `cancelauth` (POP3 sends `*`) and moves to `SASL_CANCEL`, which drops the mechanism and starts the next (lines 773-779); the GSSAPI step writes its `GSSAPI handshake failure (...)` `infof` line first (`lib/vauth/krb5_sspi.c` lines 268-319).
- A `null` answer with a `null` `CancelReason` keeps today's exit 67. BL-1342 also changes this library; this task waits for it.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Pop3.UnitTests` uses a fake exchange (`ScriptedSaslAuthenticator` or a new fake beside it) whose `RespondAsync` returns `null` with `CancelReason` `GSSAPI handshake failure (invalid security data)` and asserts the reason is reported as an info line, then `*` is sent, and the login goes on as after an undecodable challenge.
- [x] A test pins that a `null` answer with no `CancelReason` still ends with exit 67 and no `*`.
- [x] `dotnet build Curl.Protocol.Pop3.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Pop3.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports no failing member.

## Notes

- 2026-10-03: `Pop3Login.AnswerToAsync` now hands a `null` answer to `CancelOrDeny`: a `CancelReason` is reported with `ReportInfo` and answered with `*` (the same `CancelLine` path as an undecodable challenge, so the next mechanism or APOP/USER follows); no reason keeps exit 67 with no `*`. The fake is `RankedSaslAuthenticator.CancelReasons` (per mechanism). Tests: `ExecuteAsync_ExchangeCancelsWithAReason_ReportsItThenCancelsAndFallsBack` and `ExecuteAsync_ExchangeGivesNoAnswerAndNoReason_FailsWithLoginDeniedWithoutCancelling` in `Pop3ProtocolHandlerSaslCancelTests`. Pop3 tests: 312 passed, 5 skipped; Measure-CodeQuality: 0 failing members. No ADR: `ISaslExchange`'s contract (BL-1335) already states this behaviour.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. POP3 writes an exchange's CancelReason as a -v line and cancels with *, going on to the next way to log in
