---
id: BL-2706
title: Give the IMAP handler a connect-only perform that runs CAPABILITY and login and keeps the connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2706 — Give the IMAP handler a connect-only perform that runs CAPABILITY and login and keeps the connection

## Goal

`ImapProtocolHandler` has a connect-only entry point (as `WsProtocolHandler.ConnectAndUpgradeOnlyAsync` is for WebSocket) that runs only IMAP's connect phase - greeting, `A001 CAPABILITY`, `STARTTLS` when required, and the login or SASL authentication only when credentials are given - and hands back the open connection without selecting a mailbox, as libcurl 8.22.0's `CURLOPT_CONNECT_ONLY` 1 does for `imap://` (`imap_connect`).

## Context

Found by BL-2623 (upstream case 660, lib597 on `imap://%HOSTIP:%IMAPPORT/660`): Curl's `CURLOPT_CONNECT_ONLY` 1 is a raw TCP connect for every scheme (`ManagedEasyTransfer.IsRawConnectOnly`), so the server sees nothing; upstream expects exactly `A001 CAPABILITY` on the wire (no credentials, so no login). The engine wiring that calls this entry point is BL-2623's (Curl.Engine.UnitLibrary). Reuse `ImapSession` / `ImapAuthentication` so the commands, tags and failures match a normal transfer's connect phase.

## Acceptance criteria

- [x] A public `ImapProtocolHandler` method runs the connect phase on an `ITransferContext` and returns the open connection (or the transfer's failure), with XML doc comments.
- [x] A test in `Curl.Protocol.Imap.UnitTests` over a scripted connection with no credentials pins the bytes sent as `A001 CAPABILITY\r\n` and nothing after, and the connection is left open.
- [x] A test with credentials pins the login command after `CAPABILITY`, as a normal transfer sends it.
- [x] Curl.Protocol.Imap.UnitLibrary keeps 100% line and branch coverage and no method past the complexity gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `ImapProtocolHandler.ConnectOnlyAsync(ITransferContext)` returns `ImapConnectOutcome(Result, Connection)`, shaped like `WsUpgradeOutcome`. It runs `ImapSession.ConnectOnlyAsync` (greeting, CAPABILITY, STARTTLS, login only when `ImapAuthentication` finds credentials) and hands back the STARTTLS-secured connection when there is one (`ImapSession.TakeConnection`). A failure closes the connection through `ImapConnectOnlyLease` and writes the usual `-v` end lines.
- Default taken: the connect-only connection is never pooled (no `PoolScheme`) and never resumes a kept one. A CONNECT_ONLY handle owns its connection, and a kept, logged-in connection would skip the CAPABILITY that test 660 expects.
- Measured: Measure-CodeQuality -Library Curl.Protocol.Imap.UnitLibrary gives 100% line, 100% branch, worst CRAP 10. A first version with try/finally reached complexity 12 and was split.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: cab46f6c 0ba49ab3 40ad031d 9a161677 3d836d5e.
- 2026-10-17: Doing -> Done. ImapProtocolHandler.ConnectOnlyAsync runs IMAP's connect phase (CAPABILITY, STARTTLS, login when credentialed) and hands back the open connection
