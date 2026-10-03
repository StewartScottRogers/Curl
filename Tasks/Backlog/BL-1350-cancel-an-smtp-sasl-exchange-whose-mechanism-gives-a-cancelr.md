---
id: BL-1350
title: Cancel an SMTP SASL exchange whose mechanism gives a CancelReason, writing it as a -v line first
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1336, BL-1345]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1350 — Cancel an SMTP SASL exchange whose mechanism gives a CancelReason, writing it as a -v line first

## Goal

When `ISaslExchange.RespondAsync` returns `null` with `ISaslExchange.CancelReason` set (BL-1335, BL-1336), the SMTP handler reports the reason as a `-v` info line and then cancels the exchange with `*` exactly as it does for a challenge that is not base64, instead of failing with exit 67.

## Context

- Today `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` `AnswerAsync` (line 369) returns `ExchangeOutcome.Refused` for a `null` response (exit 67); an undecodable challenge goes to `CancelAsync()` (line 408), which sends `*` and lets the next mechanism be chosen.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/curl_sasl.c` lines 789-793: a mechanism step returning `CURLE_BAD_CONTENT_ENCODING` calls `cancelauth` (SMTP sends `*`) and moves to `SASL_CANCEL`, which drops the mechanism and starts the next (lines 773-779); the GSSAPI step writes its `GSSAPI handshake failure (...)` `infof` line first (`lib/vauth/krb5_sspi.c` lines 268-319).
- A `null` response with a `null` `CancelReason` keeps today's exit 67. BL-1345 also changes this library; this task waits for it.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Smtp.UnitTests` uses a fake exchange whose `RespondAsync` returns `null` with `CancelReason` `GSSAPI handshake failure (empty security message)` and asserts the reason is reported as an info line, then `*` is sent, and authentication goes on as after an undecodable challenge.
- [ ] A test pins that a `null` response with no `CancelReason` still ends with exit 67 and no `*`.
- [ ] `dotnet build Curl.Protocol.Smtp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Smtp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
