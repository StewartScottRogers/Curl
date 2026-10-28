---
id: BL-3874
title: Windows Digest: count nc=00000002 in the first answer after a 401 that closed the connection, as SSPI curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3864]
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitTests/DigestResentOnFreshConnectionTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpAuthRequest.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.DigestResentOnFreshConnection.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3874 — Windows Digest: count nc=00000002 in the first answer after a 401 that closed the connection, as SSPI curl does

## Goal

On Windows, Curl's first Digest answer after a 401 whose connection the server closed carries `nc=00000002`, with the response hashed for that count, as curl 8.22.0 (Schannel) sends it; on a kept connection it stays `nc=00000001`.

## Context

Found by BL-3606 (ADR-0924), measured 2026-10-26 with `Record-CurlExchange.ps1` and curl 8.22.0 from Git for Windows' ucrt64: with `-Response` 401 (`WWW-Authenticate: Digest realm="testrealm", nonce="abc123", qop="auth"`) then 200, where the script closes each connection, real curl reconnects and sends `nc=00000002` (its response verifies with nc 2); with a `-Script` that keeps one connection it sends `nc=00000001`. Curl sends `nc=00000001` in both. `DigestAuthenticator` keeps no state (ADR-0014), so the engine must tell it the answer goes on a new connection (find where the HTTP transfer redials after a 401 and calls `CreateAuthorization`); widen `touches` to that Engine file before editing. Applies only where `matchesSspiBuild` is set.

## Acceptance criteria

- [x] Both measured exchanges, run through real curl and Curl on Windows, send the same `nc` and a response that verifies for it; the commands and results are recorded in Notes.
- [x] A focused unit test in `Curl.Authentication.UnitTests` (and the engine's, if the engine changes) fails before the fix and passes after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28: The handler (HttpProtocolHandler.RetryWithAuthorizationAsync, ~line 2860, builds the HttpAuthRequest for the 401 retry) knows whether the 401 closed the connection, so the fix is: add an init property to HttpAuthRequest (e.g. PreviousConnectionClosed), set it there on the retry, and have DigestAuthenticator use nc=2 when matchesSspiBuild and it is set. HttpProtocolHandler.cs is held by BL-3864 (Doing), so requeued with depends-on. No code was edited.
- 2026-10-28 (lane 11): Measured with `-v` the mechanism differs from the plan above: the 401 does not announce a close (HTTP/1.1, no `Connection: close`); curl reuses the connection, sends `nc=00000001`, the connection dies before any response (`Connection died, retrying a fresh connect (retry count: 1)`), and the resend on a fresh connection carries the same cnonce with `nc=00000002`. Curl already resent, but with the value unchanged. Fix (ADR-0948): `HttpProtocolHandler.CountedOnIfResent` gives a request resent after its connection died the value `RepeatAuthorization` makes from the one sent (`HttpRequestPlan.WithAuthorizationSentAgain`, which keeps `UploadRewindFailure`). It applies on every platform, because curl's own digest.c also counts on after each answer. DigestAuthenticator.cs and HttpAuthRequest.cs needed no change. The test files are named for what they pin (`DigestResentOnFreshConnectionTests.cs`, `HttpProtocolHandlerTests.DigestResentOnFreshConnection.cs`), not the planned DigestSspiReconnect names; touches updated to match.
- Closed connection: `Record-CurlExchange.ps1 -Response <401 Digest realm="testrealm", nonce="abc123", qop="auth", Content-Length 0>,<200 Content-Length 0> -CurlArgs --digest,-u,user:pass,http://127.0.0.1:<port>/x,-v`. Real curl 8.22.0 sends nc=00000001 then nc=00000002 (response d811cfc2a8056c066b25e8140930d014 for cnonce 5d74647e52f26ab1028dfd2675a4e28e). Curl sent nc=00000001 twice before the fix and nc=00000001 then nc=00000002 after it.
- Kept connection: the same with `-Script` (read, send 401, read, send 200, close). Both send one answer with nc=00000001.
- Responses verify: `DigestResentOnFreshConnectionTests` feeds real curl's measured nc=1 header to the SSPI-build `RepeatAuthorization` and gets real curl's measured nc=2 header byte for byte. That test passes both before and after, because the authenticator did not change. The test that fails before the fix and passes after it is the handler's `ExecuteAsync_AnswerSentOnAConnectionThatDied_SendsItAgainCountedOnOnTheFreshConnection`: with the fix disabled it failed and sent `Digest nc=00000001` as sent.
- Gates: `dotnet build` clean. The fast tests passed in every project except one Curl.Protocol.Http.UnitTests failure from the first version of the fix (the HTTP/3 refused-stream resend lost its upload-rewind failure). That was fixed, and Curl.Protocol.Http.UnitTests then passed (2496 passed, 19 skipped). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members. The change is platform-neutral (no path, OS error or certificate), which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 9f476610 d7461e94 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs and HttpAuthRequest.cs, held by BL-3864 (depends-on BL-3874 -> BL-3864).
- 2026-10-28: Backlog -> Doing. Criteria at claim: 9f476610 d7461e94 779790c0.
- 2026-10-28: Doing -> Done. A request resent on a fresh connection after its reused one died counts its Digest nonce on (nc=00000002), as curl 8.22.0 does
