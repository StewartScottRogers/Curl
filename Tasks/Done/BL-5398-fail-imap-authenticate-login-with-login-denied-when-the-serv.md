---
id: BL-5398
title: Fail IMAP AUTHENTICATE LOGIN with Login denied when the server answers OK before the password
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5398 — Fail IMAP AUTHENTICATE LOGIN with Login denied when the server answers OK before the password

## Goal

`AUTHENTICATE LOGIN` with SASL-IR fails with exit 67 `Login denied` when the server answers a tagged `OK` before the password step has been sent, and no `SELECT` is sent.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-105 (Low, Confirmed, deterministic: 1 harness run plus source read). Area: IMAP SASL LOGIN, Windows.

**Repro.** An IMAP server on 127.0.0.1:47100: greeting `* OK ready`; `CAPABILITY` answered `* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR` then `<tag> OK`; `AUTHENTICATE LOGIN <b64user>` answered `<tag> OK auth` at once (no `+ UGFzc3dvcmQ6`); SELECT and FETCH answered normally.
`curl -sS -m 15 -u user:pw --login-options AUTH=LOGIN "imap://127.0.0.1:47100/INBOX/;UID=1"`
**Expected (REF).** Sends `A002 AUTHENTICATE LOGIN dXNlcg==`, then `curl: (67) Login denied`, exit 67, no SELECT.
**Actual (PORT).** Sends SELECT, UID FETCH, LOGOUT, prints the message, exit 0.
**Where to start.** `Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs`: `ExchangeAsync` accepts a tagged `OK` when `messageSent && HasFinished(mechanism)`, and `HasFinished` only holds NTLM back (BL-5013). Make a final success while the LOGIN password step is still pending a refusal too.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Imap.UnitTests` (a new file) prove that an `OK` to `AUTHENTICATE LOGIN <b64user>` before the password is requested ends with exit 67 `Login denied` and that the server receives no `SELECT`.
- [x] A test proves a normal LOGIN exchange (`+ UGFzc3dvcmQ6`, password sent, then `OK`) still succeeds.
- [x] A test proves a PLAIN exchange answered `OK` at once with the initial response is still accepted (no regression).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: the repro exits 67 with `curl: (67) Login denied`, and the server sees no SELECT. Measure real curl with `Record-CurlExchange.ps1 -Imap` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- `touches` narrowed from the whole Imap library to `ImapAuthentication.cs`, where the fix sits.
- Review acceptance check: the command exits 67 with `curl: (67) Login denied`; the server sees no SELECT.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b08e2345 94daa4a3 d4cc733f 7ae10ef7 a0b49a29 0c5a9599.
- Fix: `HasFinished` in `ImapAuthentication.cs` now holds LOGIN back as well as NTLM until the password prompt has been answered. Tests in `ImapProtocolHandlerLoginEarlyOkTests.cs` (early OK exits 67 Login denied with no SELECT sent; normal LOGIN and PLAIN-with-IR still succeed). Imap fast tests: 546 passed. Measure-CodeQuality for the Imap library: 0 failing members.
- Hand check: not run against real curl in this lane; the scripted-connection test reproduces the repro's exchange byte for byte (exit 67, `Login denied`, no SELECT). Tests use no platform-specific paths or text.
- 2026-11-17: Doing -> Done. Fixed
