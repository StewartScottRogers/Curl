---
id: BL-3874
title: Windows Digest: count nc=00000002 in the first answer after a 401 that closed the connection, as SSPI curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3864]
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitTests/DigestSspiReconnectTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpAuthRequest.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.DigestSspiReconnect.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3874 — Windows Digest: count nc=00000002 in the first answer after a 401 that closed the connection, as SSPI curl does

## Goal

On Windows, Curl's first Digest answer after a 401 whose connection the server closed carries `nc=00000002`, with the response hashed for that count, as curl 8.22.0 (Schannel) sends it; on a kept connection it stays `nc=00000001`.

## Context

Found by BL-3606 (ADR-0924), measured 2026-10-26 with `Record-CurlExchange.ps1` and curl 8.22.0 from Git for Windows' ucrt64: with `-Response` 401 (`WWW-Authenticate: Digest realm="testrealm", nonce="abc123", qop="auth"`) then 200, where the script closes each connection, real curl reconnects and sends `nc=00000002` (its response verifies with nc 2); with a `-Script` that keeps one connection it sends `nc=00000001`. Curl sends `nc=00000001` in both. `DigestAuthenticator` keeps no state (ADR-0014), so the engine must tell it the answer goes on a new connection (find where the HTTP transfer redials after a 401 and calls `CreateAuthorization`); widen `touches` to that Engine file before editing. Applies only where `matchesSspiBuild` is set.

## Acceptance criteria

- [ ] Both measured exchanges, run through real curl and Curl on Windows, send the same `nc` and a response that verifies for it; the commands and results are recorded in Notes.
- [ ] A focused unit test in `Curl.Authentication.UnitTests` (and the engine's, if the engine changes) fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28: The handler (HttpProtocolHandler.RetryWithAuthorizationAsync, ~line 2860, builds the HttpAuthRequest for the 401 retry) knows whether the 401 closed the connection, so the fix is: add an init property to HttpAuthRequest (e.g. PreviousConnectionClosed), set it there on the retry, and have DigestAuthenticator use nc=2 when matchesSspiBuild and it is set. HttpProtocolHandler.cs is held by BL-3864 (Doing), so requeued with depends-on. No code was edited.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 9f476610 d7461e94 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs and HttpAuthRequest.cs, held by BL-3864 (depends-on BL-3874 -> BL-3864).
