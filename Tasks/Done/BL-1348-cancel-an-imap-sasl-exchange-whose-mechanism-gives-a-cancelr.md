---
id: BL-1348
title: Cancel an IMAP SASL exchange whose mechanism gives a CancelReason, writing it as a -v line first
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1336]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1348 — Cancel an IMAP SASL exchange whose mechanism gives a CancelReason, writing it as a -v line first

## Goal

When `ISaslExchange.RespondAsync` returns `null` with `ISaslExchange.CancelReason` set (BL-1335; the GSSAPI security-layer failures of BL-1336), the IMAP handler reports the reason as a `-v` info line and then cancels the exchange with `*` exactly as it already does for a challenge that is not base64, instead of failing with exit 67 `Login denied`.

## Context

- Today `Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` `AnswerAsync` (line 374) treats any `null` answer from `RespondAsync` as `ExchangeOutcome.Refused`, which `Finish` turns into exit 67 `Login denied`. Its cancel path (`CancelAsync`, `ExchangeOutcome` "cancelled", type remarks lines 22-28, BL-1220) sends `*`, reads the reply whatever it is, drops the mechanism and lets the authenticator choose again; none left is `LOGIN` when allowed, else exit 67 `Authentication cancelled`.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/curl_sasl.c` lines 789-793: a mechanism step returning `CURLE_BAD_CONTENT_ENCODING` calls the protocol's `cancelauth` (IMAP sends `*`) and moves to `SASL_CANCEL`, which (lines 773-779) drops the mechanism and starts the next. The GSSAPI step writes its `infof` line (`GSSAPI handshake failure (...)`, `lib/vauth/krb5_sspi.c` lines 268-319) before returning that code, so the line comes before `> *` in `-v`.
- A `null` answer with a `null` `CancelReason` keeps today's exit 67 `Login denied`.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Imap.UnitTests` uses a fake exchange whose `RespondAsync` returns `null` with `CancelReason` `GSSAPI handshake failure (invalid security layer)` and asserts: the reason is reported as an info line, then `*` is sent, the reply is read, and the handler goes on as for an undecodable challenge (with no other mechanism and no `LOGIN` allowed, exit 67 `Authentication cancelled`).
- [x] A test pins that with another offered mechanism the handler starts it after the cancel, and that a `null` answer with no `CancelReason` still ends with exit 67 `Login denied` and no `*`.
- [x] `dotnet build Curl.Protocol.Imap.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Imap.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports no failing member.

## Notes

- `ImapAuthentication.AnswerAsync` now ends a `null` answer through `RefuseOrCancelAsync`: with a `CancelReason` it writes the reason with `ITransferEvents.ReportInfo` (a `-v` `*` line, as curl's `infof`) and then runs the existing `CancelAsync` (`*`, reply read, mechanism dropped, next chosen; none left is `LOGIN` or exit 67 `Authentication cancelled`); without one it stays `Refused` (exit 67 `Login denied`, no `*`). No new curl measurement: the order and wording come from curl 8.21.0's source cited in Context and the reason text from BL-1336.
- Test fake: `RankedSaslAuthenticator.CancelReasons` gives a mechanism's exchange a `CancelReason` once its scripted answers run out. Tests in `ImapProtocolHandlerSaslCancelTests`.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reported one failing member that was already failing before this task, `ImapSessionMessages.IsWrittenByVerbose` (78% branch, complexity 32, from the compiler's expansion of a string `is not (... or ...)` pattern). It is inside `touches`, so it now looks the message up in a `FrozenSet`: the library measures 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An IMAP SASL exchange whose mechanism gives a CancelReason writes it as a -v line, then cancels with * and tries the next mechanism
