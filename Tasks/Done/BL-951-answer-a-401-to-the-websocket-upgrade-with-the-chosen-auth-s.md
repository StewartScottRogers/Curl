---
id: BL-951
title: Answer a 401 to the WebSocket upgrade with the chosen auth scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-842]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-951 — Answer a 401 to the WebSocket upgrade with the chosen auth scheme

## Goal

A `ws://` or `wss://` upgrade answered with `401` and a `WWW-Authenticate` challenge is sent again with the answer the authenticator gives - Digest, NTLM's legs, Negotiate's continuation, `--anyauth` - as curl 8.21.0 does, instead of failing at once with exit 22.

## Context

- `WsProtocolHandler` sends the upgrade with the pre-emptive `Authorization` from `IHttpAuthenticator.CreateAuthorizationAsync` (ADR-0227, BL-842) and fails any status but 101 with `Refused WebSocket upgrade: <code>` (ADR-0128).
- curl drives the upgrade through its HTTP code, so a 401 is answered as HTTP answers it: `CreateAuthorizationAsync` with the challenges when the request sent nothing, `ContinueAuthorizationAsync` when it sent a value (ADR-0181, ADR-0227), on the same connection when it stays open.
- Measure curl 8.21.0 with `Record-CurlExchange.ps1 -Script` (401 Digest then 101; 401 NTLM Type 2 then 101; 401 with no answerable challenge) before pinning bytes and exit codes.

## Acceptance criteria

The criteria as filed assumed curl answers the 401; measured curl 8.21.0 sends the upgrade once and fails with exit 22 (ADR-0228), so they are pinned as the measured behaviour. The original wording is kept under Notes.

- [x] A `Curl.Protocol.Ws.UnitTests` test answers a 401 Digest challenge and pins both upgrade requests and the 101 that follows: `ExecuteAsync_DigestChallengeThen101_SendsTheUpgradeOnceAndFailsWithExit22` pins the one request curl sends, exit 22 `Refused WebSocket upgrade: 401`, and the 101 behind it left unread.
- [x] A `Curl.Protocol.Ws.UnitTests` test pins a two-leg handshake through `ContinueAuthorizationAsync`: `ExecuteAsync_NtlmType2Challenge_AsksNoContinuationAndFailsWithExit22` pins the Type 1 request, no continuation asked for, and exit 22.
- [x] A 401 the authenticator does not answer still fails with exit 22 and the measured message (`ExecuteAsync_StatusOtherThan101_RefusesTheUpgradeWithExit22`, and the two tests above).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed (`Curl.Protocol.Ws.UnitLibrary`: 100/100, 86 members, 0 failing, worst CRAP 10).

## Notes

- **Measured 2026-09-29**, curl 8.21.0 mingw/Schannel, `Record-CurlExchange.ps1` against `ws://127.0.0.1:18951/c`, `-v -u u:p`: `--digest` (`-Script`, 401 Digest then 101 on the same connection), `--anyauth` (Digest, NTLM and Basic challenges), `--ntlm` (bare `NTLM`, and `NTLM <Type 2>`), `--basic` - every one sends one request and ends with exit 22 `Refused WebSocket upgrade: 401`. curl never re-sends the upgrade, on the same connection or a new one.
- **Decision (ADR-0228, decided by Claude under Stewart's delegation):** match the measured curl; the handler keeps refusing a 401 and never calls `ContinueAuthorizationAsync`. No production behaviour changed; the handler's doc comment now says so, and the tests pin it.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0228 and its README row; no task in `Doing` names it.
- The measurement also showed `* Server auth using <Scheme> with user 'u'` and `* Basic authentication problem, ignoring.` in curl's `-v`, which no handler writes yet: filed as BL-952 (depends on BL-848, the HTTP twin).
- Original criteria, as filed: a test answering a 401 Digest challenge pinning both upgrade requests and the 101; a test pinning a two-leg handshake through `ContinueAuthorizationAsync`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A 401 to a ws:// upgrade is refused with exit 22 after one request, as measured curl 8.21.0 does (ADR-0228), pinned for Digest and NTLM
